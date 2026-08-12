using SubiteAPI.DTOs;

namespace SubiteAPI.Services;

public interface IVehicleService
{
    Task<VehicleDto?> GetMyVehicleAsync(Guid userId);
    Task<VehicleDto> UpsertMyVehicleAsync(Guid userId, UpsertVehicleDto dto);
}
