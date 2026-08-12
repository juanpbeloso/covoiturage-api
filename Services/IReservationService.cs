using SubiteAPI.DTOs;

namespace SubiteAPI.Services;

public interface IReservationService
{
    Task<ReservationDto> CreateAsync(Guid passengerId, CreateReservationDto dto, bool notifyDriver = true);
    Task<ReservationDto> ConfirmAsync(Guid driverId, Guid reservationId);
    Task<ReservationDto> CancelAsync(Guid userId, Guid reservationId, string? reason, bool notify = true);    Task<IReadOnlyList<ReservationDto>> GetMyReservationsAsync(Guid passengerId);
    Task<IReadOnlyList<ReservationDto>> GetForRideAsync(Guid driverId, Guid rideId);
}
