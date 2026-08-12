using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[Route("api/[controller]")]
[Authorize]
public class VehiclesController : ApiControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    /// <summary>Obtener el vehículo del usuario autenticado</summary>
    [HttpGet("me")]
    public async Task<ActionResult<VehicleDto>> GetMine()
    {
        var vehicle = await _vehicleService.GetMyVehicleAsync(CurrentUserId).ConfigureAwait(false);
        if (vehicle == null) return NoContent();
        return Ok(vehicle);
    }

    /// <summary>Crear o actualizar el vehículo del usuario autenticado</summary>
    [HttpPut("me")]
    public async Task<ActionResult<VehicleDto>> UpsertMine([FromBody] UpsertVehicleDto dto)
    {
        var vehicle = await _vehicleService.UpsertMyVehicleAsync(CurrentUserId, dto).ConfigureAwait(false);
        return Ok(vehicle);
    }
}
