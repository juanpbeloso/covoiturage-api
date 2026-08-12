using SubiteAPI.DTOs;

namespace SubiteAPI.Services;

public interface IRideService
{
    Task<RideDto> CreateAsync(Guid driverId, CreateRideDto dto);
    Task<RideDto> GetByIdAsync(Guid rideId);
    Task<PagedResult<RideDto>> SearchAsync(RideSearchDto filters);
    Task<IReadOnlyList<RideDto>> GetMyRidesAsDriverAsync(Guid driverId);
    Task<RideDto> UpdateAsync(Guid driverId, Guid rideId, UpdateRideDto dto);
    Task CancelAsync(Guid driverId, Guid rideId);
}
