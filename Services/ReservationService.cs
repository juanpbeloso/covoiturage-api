using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Helpers;
using SubiteAPI.Models;

namespace SubiteAPI.Services;

public class ReservationService : IReservationService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public ReservationService(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<ReservationDto> CreateAsync(
        Guid passengerId,
        CreateReservationDto dto,
        bool notifyDriver = true)
    {
        await using var tx = await _db.Database.BeginTransactionAsync().ConfigureAwait(false);

        var ride = await _db.Rides
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == dto.RideId)
            .ConfigureAwait(false)
            ?? throw new RideNotFoundException(dto.RideId);

        if (ride.DriverId == passengerId)
        {
            throw new CannotReserveOwnRideException();
        }

        if (ride.Status != RideStatus.Active || ride.DepartureDateTime <= DateTime.UtcNow)
        {
            throw new RideAlreadyDepartedException();
        }

        var alreadyReserved = await _db.Reservations.AnyAsync(r =>
            r.RideId == dto.RideId &&
            r.PassengerId == passengerId &&
            (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .ConfigureAwait(false);

        if (alreadyReserved)
        {
            throw new ReservationAlreadyExistsException();
        }

        if (dto.SeatsReserved > ride.AvailableSeats)
        {
            throw new RideFullException(ride.Id);
        }

        var points = RideRouteHelper.BuildRoutePoints(ride);
        var boardCity = string.IsNullOrWhiteSpace(dto.BoardingCity) ? ride.OriginCity : dto.BoardingCity.Trim();
        var alightCity = string.IsNullOrWhiteSpace(dto.AlightingCity) ? ride.DestinationCity : dto.AlightingCity.Trim();
        var boardIndex = RideRouteHelper.FindPointIndex(points, boardCity)
            ?? throw new BusinessException("RESV_010", $"No encontramos \"{boardCity}\" en la ruta de este viaje.", 400);
        var alightIndex = RideRouteHelper.FindPointIndex(points, alightCity)
            ?? throw new BusinessException("RESV_011", $"No encontramos \"{alightCity}\" en la ruta de este viaje.", 400);

        if (alightIndex <= boardIndex)
        {
            throw new BusinessException(
                "RESV_012",
                "El destino del tramo debe estar después del origen en la ruta del viaje.",
                400);
        }

        var pricePerSeat = RideRouteHelper.ComputeSegmentPricePerSeat(
            ride.PricePerSeat, points, boardIndex, alightIndex);

        ride.AvailableSeats -= dto.SeatsReserved;
        if (ride.AvailableSeats == 0)
        {
            ride.Status = RideStatus.Full;
        }

        var reservation = new Reservation
        {
            RideId = ride.Id,
            PassengerId = passengerId,
            SeatsReserved = dto.SeatsReserved,
            TotalPrice = dto.SeatsReserved * pricePerSeat,
            BoardingCity = points[boardIndex].City,
            AlightingCity = points[alightIndex].City,
            BoardingSequence = points[boardIndex].Sequence,
            AlightingSequence = points[alightIndex].Sequence,
            Status = ReservationStatus.Pending
        };

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        await tx.CommitAsync().ConfigureAwait(false);

        var mapped = await GetByIdMappedAsync(reservation.Id).ConfigureAwait(false);

        // Solo notificar si no hay checkout de por medio (pago pendiente).
        // Con MercadoPago el conductor se notifica al aprobar el pago.
        if (notifyDriver)
        {
            var passengerName = mapped.Passenger?.FullName ?? "Un pasajero";
            await _notifications.NotifyUserAsync(
                ride.DriverId,
                "booking",
                "Nueva reserva",
                $"{passengerName} reservó {dto.SeatsReserved} asiento(s) en tu viaje {ride.OriginCity} → {ride.DestinationCity}.",
                actionUrl: $"/rides/reservation-detail?id={reservation.Id}",
                data: new { reservationId = reservation.Id, rideId = ride.Id }).ConfigureAwait(false);
        }

        return mapped;
    }

    public async Task<ReservationDto> ConfirmAsync(Guid driverId, Guid reservationId)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Ride)
            .FirstOrDefaultAsync(r => r.Id == reservationId)
            .ConfigureAwait(false)
            ?? throw new ReservationNotFoundException(reservationId);

        if (reservation.Ride.DriverId != driverId)
        {
            throw new BusinessException("RESV_004", "Solo el conductor del viaje puede confirmar la reserva.", 403);
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new ReservationNotCancellableException();
        }

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ConfirmedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync().ConfigureAwait(false);

        var mapped = await GetByIdMappedAsync(reservation.Id).ConfigureAwait(false);
        var route = mapped.Ride != null
            ? $"{mapped.Ride.OriginCity} → {mapped.Ride.DestinationCity}"
            : "tu viaje";
        await _notifications.NotifyUserAsync(
            reservation.PassengerId,
            "booking",
            "Reserva confirmada",
            $"El conductor confirmó tu reserva para {route}.",
            actionUrl: $"/rides/reservation-detail?id={reservation.Id}",
            data: new { reservationId = reservation.Id }).ConfigureAwait(false);

        return mapped;
    }

    public async Task<ReservationDto> CancelAsync(
        Guid userId,
        Guid reservationId,
        string? reason,
        bool notify = true)
    {
        await using var tx = await _db.Database.BeginTransactionAsync().ConfigureAwait(false);

        var reservation = await _db.Reservations
            .Include(r => r.Ride)
            .FirstOrDefaultAsync(r => r.Id == reservationId)
            .ConfigureAwait(false)
            ?? throw new ReservationNotFoundException(reservationId);

        var isPassenger = reservation.PassengerId == userId;
        var isDriver = reservation.Ride.DriverId == userId;
        if (!isPassenger && !isDriver)
        {
            throw new BusinessException("RESV_005", "No tenés permiso para cancelar esta reserva.", 403);
        }

        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
        {
            throw new ReservationNotCancellableException();
        }

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAt = DateTime.UtcNow;
        reservation.CancellationReason = reason;

        // Liberar los asientos en el viaje.
        reservation.Ride.AvailableSeats += reservation.SeatsReserved;
        if (reservation.Ride.Status == RideStatus.Full &&
            reservation.Ride.AvailableSeats > 0 &&
            reservation.Ride.DepartureDateTime > DateTime.UtcNow)
        {
            reservation.Ride.Status = RideStatus.Active;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        await tx.CommitAsync().ConfigureAwait(false);

        var mapped = await GetByIdMappedAsync(reservation.Id).ConfigureAwait(false);

        if (notify)
        {
            var notifyUserId = isPassenger ? reservation.Ride.DriverId : reservation.PassengerId;
            var who = isPassenger ? "El pasajero" : "El conductor";
            var route = mapped.Ride != null
                ? $"{mapped.Ride.OriginCity} → {mapped.Ride.DestinationCity}"
                : "un viaje";
            await _notifications.NotifyUserAsync(
                notifyUserId,
                "booking",
                "Reserva cancelada",
                $"{who} canceló la reserva de {route}.",
                actionUrl: $"/rides/reservation-detail?id={reservation.Id}",
                data: new { reservationId = reservation.Id }).ConfigureAwait(false);
        }

        return mapped;
    }

    public async Task<IReadOnlyList<ReservationDto>> GetMyReservationsAsync(Guid passengerId)
    {
        var reservations = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Passenger)
            .Include(r => r.Ride).ThenInclude(ride => ride.Driver)
            .Include(r => r.Ride).ThenInclude(ride => ride.Vehicle)
            .Include(r => r.Ride).ThenInclude(ride => ride.Stops)
            .Where(r => r.PassengerId == passengerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);

        return reservations.Select(r => Map(r, includeRide: true)).ToList();
    }

    public async Task<IReadOnlyList<ReservationDto>> GetForRideAsync(Guid driverId, Guid rideId)
    {
        var ride = await _db.Rides
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rideId)
            .ConfigureAwait(false)
            ?? throw new RideNotFoundException(rideId);

        if (ride.DriverId != driverId)
        {
            throw new BusinessException("RESV_006", "Solo el conductor puede ver las reservas del viaje.", 403);
        }

        var reservations = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Passenger)
            .Where(r => r.RideId == rideId && r.Status != ReservationStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);

        return reservations.Select(r => Map(r, includeRide: false)).ToList();
    }

    private async Task<ReservationDto> GetByIdMappedAsync(Guid reservationId)
    {
        var reservation = await _db.Reservations
            .AsNoTracking()
            .Include(r => r.Passenger)
            .Include(r => r.Ride).ThenInclude(ride => ride.Driver)
            .Include(r => r.Ride).ThenInclude(ride => ride.Vehicle)
            .Include(r => r.Ride).ThenInclude(ride => ride.Stops)
            .FirstAsync(r => r.Id == reservationId)
            .ConfigureAwait(false);

        return Map(reservation, includeRide: true);
    }

    private static ReservationDto Map(Reservation r, bool includeRide) => new()
    {
        Id = r.Id,
        RideId = r.RideId,
        PassengerId = r.PassengerId,
        SeatsReserved = r.SeatsReserved,
        TotalPrice = r.TotalPrice,
        BoardingCity = r.BoardingCity,
        AlightingCity = r.AlightingCity,
        BoardingSequence = r.BoardingSequence,
        AlightingSequence = r.AlightingSequence,
        Status = r.Status.ToString(),
        CreatedAt = r.CreatedAt,
        ConfirmedAt = r.ConfirmedAt,
        CancelledAt = r.CancelledAt,
        CancellationReason = r.CancellationReason,
        Passenger = r.Passenger == null ? new ReservationPassengerDto() : new ReservationPassengerDto
        {
            Id = r.Passenger.Id,
            FullName = r.Passenger.FullName,
            ProfileImageUrl = r.Passenger.ProfileImageUrl,
            Rating = r.Passenger.Rating
        },
        Ride = includeRide && r.Ride != null ? RideService.Map(r.Ride) : null
    };
}
