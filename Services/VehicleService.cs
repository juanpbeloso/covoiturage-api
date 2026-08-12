using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Models;

namespace SubiteAPI.Services;

public class VehicleService : IVehicleService
{
    private readonly AppDbContext _db;

    public VehicleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<VehicleDto?> GetMyVehicleAsync(Guid userId)
    {
        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId)
            .ConfigureAwait(false);

        return vehicle == null ? null : Map(vehicle);
    }

    public async Task<VehicleDto> UpsertMyVehicleAsync(Guid userId, UpsertVehicleDto dto)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.UserId == userId)
            .ConfigureAwait(false);

        if (vehicle == null)
        {
            vehicle = new Vehicle { UserId = userId };
            _db.Vehicles.Add(vehicle);
        }

        vehicle.Brand = dto.Brand;
        vehicle.Model = dto.Model;
        vehicle.Color = dto.Color;
        vehicle.LicensePlate = dto.LicensePlate;
        vehicle.Year = dto.Year;
        vehicle.ImageUrl = dto.ImageUrl;

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return Map(vehicle);
    }

    private static VehicleDto Map(Vehicle v) => new()
    {
        Id = v.Id,
        Brand = v.Brand,
        Model = v.Model,
        Color = v.Color,
        LicensePlate = v.LicensePlate,
        Year = v.Year,
        ImageUrl = v.ImageUrl
    };
}
