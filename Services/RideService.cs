using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Helpers;
using SubiteAPI.Models;

namespace SubiteAPI.Services;

public class RideService : IRideService
{
    private readonly AppDbContext _db;

    public RideService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RideDto> CreateAsync(Guid driverId, CreateRideDto dto)
    {
        if (dto.DepartureDateTime <= DateTime.UtcNow)
        {
            throw new BusinessException("RIDE_005", "La fecha de salida debe ser futura.", 400);
        }

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.UserId == driverId)
            .ConfigureAwait(false)
            ?? throw new BusinessException(
                "RIDE_006",
                "Necesitás cargar tu vehículo antes de publicar un viaje.",
                409);

        var stops = NormalizeStops(dto);

        var ride = new Ride
        {
            DriverId = driverId,
            VehicleId = vehicle.Id,
            OriginCity = dto.OriginCity.Trim(),
            OriginAddress = dto.OriginAddress.Trim(),
            OriginLat = dto.OriginLat,
            OriginLng = dto.OriginLng,
            DestinationCity = dto.DestinationCity.Trim(),
            DestinationAddress = dto.DestinationAddress.Trim(),
            DestinationLat = dto.DestinationLat,
            DestinationLng = dto.DestinationLng,
            DepartureDateTime = DateTimeHelper.NormalizeToUtc(dto.DepartureDateTime),
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            ArrivalDateTime = dto.EstimatedDurationMinutes > 0
                ? DateTimeHelper.NormalizeToUtc(dto.DepartureDateTime).AddMinutes(dto.EstimatedDurationMinutes)
                : null,
            TotalSeats = dto.TotalSeats,
            AvailableSeats = dto.TotalSeats,
            PricePerSeat = dto.PricePerSeat,
            TotalDistanceKm = dto.TotalDistanceKm > 0
                ? dto.TotalDistanceKm
                : EstimateRideDistanceKm(dto, stops),
            Notes = dto.Notes,
            AllowsPets = dto.AllowsPets,
            AllowsSmoking = dto.AllowsSmoking,
            AllowsLuggage = dto.AllowsLuggage,
            Status = RideStatus.Active,
            Stops = stops
        };

        _db.Rides.Add(ride);

        var driver = await _db.Users.FirstOrDefaultAsync(u => u.Id == driverId).ConfigureAwait(false);
        if (driver != null && !driver.IsDriver)
        {
            driver.IsDriver = true;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);

        return await GetByIdAsync(ride.Id).ConfigureAwait(false);
    }

    public async Task<RideDto> GetByIdAsync(Guid rideId)
    {
        var ride = await _db.Rides
            .AsNoTracking()
            .Include(r => r.Driver)
            .Include(r => r.Vehicle)
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == rideId)
            .ConfigureAwait(false)
            ?? throw new RideNotFoundException(rideId);

        return Map(ride)!;
    }

    public async Task<PagedResult<RideDto>> SearchAsync(RideSearchDto filters)
    {
        var query = _db.Rides
            .AsNoTracking()
            .Include(r => r.Driver)
            .Include(r => r.Vehicle)
            .Include(r => r.Stops)
            .Where(r => r.Status == RideStatus.Active && r.DepartureDateTime >= DateTime.UtcNow);

        var originFilter = filters.OriginCity?.Trim();
        var destFilter = filters.DestinationCity?.Trim();

        if (filters.Date.HasValue)
        {
            var (dayStart, dayEnd) = DateTimeHelper.GetArgentinaDayUtcRange(filters.Date.Value);
            query = query.Where(r => r.DepartureDateTime >= dayStart && r.DepartureDateTime < dayEnd);
        }

        if (filters.MinSeats.HasValue)
        {
            query = query.Where(r => r.AvailableSeats >= filters.MinSeats.Value);
        }

        if (filters.AllowsPets == true)
        {
            query = query.Where(r => r.AllowsPets);
        }

        if (filters.AllowsLuggage == true)
        {
            query = query.Where(r => r.AllowsLuggage);
        }

        var candidates = await query
            .OrderBy(r => r.DepartureDateTime)
            .Take(500)
            .ToListAsync()
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(originFilter))
        {
            candidates = candidates
                .Where(r =>
                    TextNormalize.ContainsFolded(r.OriginCity, originFilter) ||
                    r.Stops.Any(s => TextNormalize.ContainsFolded(s.City, originFilter)))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(destFilter))
        {
            candidates = candidates
                .Where(r =>
                    TextNormalize.ContainsFolded(r.DestinationCity, destFilter) ||
                    r.Stops.Any(s => TextNormalize.ContainsFolded(s.City, destFilter)) ||
                    TextNormalize.ContainsFolded(r.OriginCity, destFilter))
                .ToList();
        }

        var mapped = new List<RideDto>();
        foreach (var ride in candidates)
        {
            var dto = Map(ride, originFilter, destFilter);
            if (dto == null) continue;

            if (filters.MaxPrice.HasValue && dto.SegmentPricePerSeat > filters.MaxPrice.Value)
            {
                continue;
            }

            mapped.Add(dto);
        }

        var page = filters.Page < 1 ? 1 : filters.Page;
        var pageSize = filters.PageSize is < 1 or > 100 ? 20 : filters.PageSize;
        var total = mapped.Count;
        var items = mapped
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<RideDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<IReadOnlyList<RideDto>> GetMyRidesAsDriverAsync(Guid driverId)
    {
        var rides = await _db.Rides
            .AsNoTracking()
            .Include(r => r.Driver)
            .Include(r => r.Vehicle)
            .Include(r => r.Stops)
            .Where(r => r.DriverId == driverId)
            .OrderByDescending(r => r.DepartureDateTime)
            .ToListAsync()
            .ConfigureAwait(false);

        return rides.Select(r => Map(r)!).ToList();
    }

    public async Task<RideDto> UpdateAsync(Guid driverId, Guid rideId, UpdateRideDto dto)
    {
        var ride = await _db.Rides
            .FirstOrDefaultAsync(r => r.Id == rideId)
            .ConfigureAwait(false)
            ?? throw new RideNotFoundException(rideId);

        if (ride.DriverId != driverId)
        {
            throw new BusinessException("RIDE_007", "No podés modificar un viaje que no es tuyo.", 403);
        }

        if (ride.Status is RideStatus.Completed or RideStatus.Cancelled or RideStatus.InProgress)
        {
            throw new BusinessException("RIDE_008", "El viaje no puede modificarse en su estado actual.", 409);
        }

        if (dto.Notes != null) ride.Notes = dto.Notes;
        if (dto.PricePerSeat.HasValue) ride.PricePerSeat = dto.PricePerSeat.Value;
        if (dto.AllowsPets.HasValue) ride.AllowsPets = dto.AllowsPets.Value;
        if (dto.AllowsSmoking.HasValue) ride.AllowsSmoking = dto.AllowsSmoking.Value;
        if (dto.AllowsLuggage.HasValue) ride.AllowsLuggage = dto.AllowsLuggage.Value;
        if (dto.DepartureDateTime.HasValue)
        {
            if (dto.DepartureDateTime.Value <= DateTime.UtcNow)
            {
                throw new BusinessException("RIDE_005", "La fecha de salida debe ser futura.", 400);
            }
            ride.DepartureDateTime = DateTimeHelper.NormalizeToUtc(dto.DepartureDateTime.Value);
            ride.ArrivalDateTime = ride.EstimatedDurationMinutes > 0
                ? ride.DepartureDateTime.AddMinutes(ride.EstimatedDurationMinutes)
                : null;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return await GetByIdAsync(ride.Id).ConfigureAwait(false);
    }

    public async Task CancelAsync(Guid driverId, Guid rideId)
    {
        var ride = await _db.Rides
            .FirstOrDefaultAsync(r => r.Id == rideId)
            .ConfigureAwait(false)
            ?? throw new RideNotFoundException(rideId);

        if (ride.DriverId != driverId)
        {
            throw new BusinessException("RIDE_007", "No podés cancelar un viaje que no es tuyo.", 403);
        }

        if (ride.Status is RideStatus.Completed or RideStatus.Cancelled)
        {
            throw new BusinessException("RIDE_008", "El viaje ya está finalizado o cancelado.", 409);
        }

        ride.Status = RideStatus.Cancelled;

        var reservations = await _db.Reservations
            .Where(r => r.RideId == rideId &&
                        (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelledAt = DateTime.UtcNow;
            reservation.CancellationReason = "El conductor canceló el viaje.";
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static List<RideStop> NormalizeStops(CreateRideDto dto)
    {
        var raw = dto.Stops ?? new List<CreateRideStopDto>();
        if (raw.Count == 0) return new List<RideStop>();

        if (dto.OriginLat is not double oLat || dto.OriginLng is not double oLng ||
            dto.DestinationLat is not double dLat || dto.DestinationLng is not double dLng)
        {
            throw new BusinessException(
                "RIDE_010",
                "Para agregar paradas necesitás origen y destino con ubicación (mapa o ciudad con coordenadas).",
                400);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RideStop>();
        var sequence = 1;
        double previousKm = 0;

        foreach (var stop in raw)
        {
            var city = stop.City?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(city)) continue;

            if (city.Equals(dto.OriginCity.Trim(), StringComparison.OrdinalIgnoreCase) ||
                city.Equals(dto.DestinationCity.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(
                    "RIDE_011",
                    $"La parada \"{city}\" no puede ser igual al origen o destino.",
                    400);
            }

            if (!seen.Add(city))
            {
                throw new BusinessException("RIDE_012", $"La parada \"{city}\" está duplicada.", 400);
            }

            if (stop.Lat is double sLat && stop.Lng is double sLng)
            {
                if (!RideRouteHelper.IsRoughlyOnRoute(oLat, oLng, dLat, dLng, sLat, sLng))
                {
                    throw new BusinessException(
                        "RIDE_013",
                        $"\"{city}\" no parece estar en la ruta entre {dto.OriginCity} y {dto.DestinationCity}.",
                        400);
                }
            }

            var km = stop.DistanceFromOriginKm is > 0
                ? stop.DistanceFromOriginKm.Value
                : RideRouteHelper.EstimateDistanceFromOrigin(oLat, oLng, stop.Lat, stop.Lng, previousKm + 20);

            if (km < previousKm)
            {
                km = previousKm + 5;
            }

            previousKm = km;
            result.Add(new RideStop
            {
                Sequence = sequence++,
                City = city,
                Address = string.IsNullOrWhiteSpace(stop.Address) ? null : stop.Address.Trim(),
                Lat = stop.Lat,
                Lng = stop.Lng,
                DistanceFromOriginKm = Math.Round(km, 1)
            });
        }

        return result;
    }

    private static double? EstimateRideDistanceKm(CreateRideDto dto, List<RideStop> stops)
    {
        if (dto.OriginLat is not double oLat || dto.OriginLng is not double oLng ||
            dto.DestinationLat is not double dLat || dto.DestinationLng is not double dLng)
        {
            return null;
        }

        if (stops.Count == 0)
        {
            return Math.Round(RideRouteHelper.HaversineKm(oLat, oLng, dLat, dLng), 1);
        }

        double total = 0;
        double prevLat = oLat;
        double prevLng = oLng;
        foreach (var stop in stops.OrderBy(s => s.Sequence))
        {
            if (stop.Lat is double sLat && stop.Lng is double sLng)
            {
                total += RideRouteHelper.HaversineKm(prevLat, prevLng, sLat, sLng);
                prevLat = sLat;
                prevLng = sLng;
            }
        }

        total += RideRouteHelper.HaversineKm(prevLat, prevLng, dLat, dLng);
        return Math.Round(total, 1);
    }

    /// <summary>
    /// Mapea un viaje. Si hay filtros de origen/destino, exige un tramo válido
    /// (subida antes que bajada en la ruta) o retorna null.
    /// </summary>
    internal static RideDto? Map(Ride r, string? searchOrigin = null, string? searchDest = null)
    {
        var points = RideRouteHelper.BuildRoutePoints(r);
        int boardIndex = 0;
        int alightIndex = points.Count - 1;

        if (!string.IsNullOrWhiteSpace(searchOrigin) || !string.IsNullOrWhiteSpace(searchDest))
        {
            if (!string.IsNullOrWhiteSpace(searchOrigin))
            {
                var found = RideRouteHelper.FindPointIndex(points, searchOrigin);
                if (found == null) return null;
                boardIndex = found.Value;
            }

            if (!string.IsNullOrWhiteSpace(searchDest))
            {
                var found = RideRouteHelper.FindPointIndex(points, searchDest);
                if (found == null) return null;
                alightIndex = found.Value;
            }

            if (alightIndex <= boardIndex) return null;
        }

        var segmentPrice = RideRouteHelper.ComputeSegmentPricePerSeat(
            r.PricePerSeat, points, boardIndex, alightIndex);

        return new RideDto
        {
            Id = r.Id,
            OriginCity = r.OriginCity,
            OriginAddress = r.OriginAddress,
            OriginLat = r.OriginLat,
            OriginLng = r.OriginLng,
            DestinationCity = r.DestinationCity,
            DestinationAddress = r.DestinationAddress,
            DestinationLat = r.DestinationLat,
            DestinationLng = r.DestinationLng,
            DepartureDateTime = r.DepartureDateTime,
            ArrivalDateTime = r.ArrivalDateTime,
            EstimatedDurationMinutes = r.EstimatedDurationMinutes,
            TotalSeats = r.TotalSeats,
            AvailableSeats = r.AvailableSeats,
            PricePerSeat = r.PricePerSeat,
            TotalDistanceKm = r.TotalDistanceKm,
            Status = r.Status.ToString(),
            Notes = r.Notes,
            AllowsPets = r.AllowsPets,
            AllowsSmoking = r.AllowsSmoking,
            AllowsLuggage = r.AllowsLuggage,
            CreatedAt = r.CreatedAt,
            Stops = r.Stops
                .OrderBy(s => s.Sequence)
                .Select(s => new RideStopDto
                {
                    Id = s.Id,
                    Sequence = s.Sequence,
                    City = s.City,
                    Address = s.Address,
                    Lat = s.Lat,
                    Lng = s.Lng,
                    DistanceFromOriginKm = s.DistanceFromOriginKm
                })
                .ToList(),
            SegmentPricePerSeat = segmentPrice,
            MatchedBoardingCity = points[boardIndex].City,
            MatchedAlightingCity = points[alightIndex].City,
            MatchedBoardingSequence = points[boardIndex].Sequence,
            MatchedAlightingSequence = points[alightIndex].Sequence,
            Driver = r.Driver == null ? new RideDriverDto() : new RideDriverDto
            {
                Id = r.Driver.Id,
                FullName = r.Driver.FullName,
                ProfileImageUrl = r.Driver.ProfileImageUrl,
                Rating = r.Driver.Rating,
                ReviewsCount = r.Driver.ReviewsCount,
                IsVerified = r.Driver.IsVerified
            },
            Vehicle = r.Vehicle == null ? null : new VehicleDto
            {
                Id = r.Vehicle.Id,
                Brand = r.Vehicle.Brand,
                Model = r.Vehicle.Model,
                Color = r.Vehicle.Color,
                LicensePlate = r.Vehicle.LicensePlate,
                Year = r.Vehicle.Year,
                ImageUrl = r.Vehicle.ImageUrl
            }
        };
    }
}
