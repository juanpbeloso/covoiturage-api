using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[Route("api/[controller]")]
public class RidesController : ApiControllerBase
{
    private readonly IRideService _rideService;

    public RidesController(IRideService rideService)
    {
        _rideService = rideService;
    }

    /// <summary>Buscar viajes activos con filtros (público)</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<RideDto>>> Search([FromQuery] RideSearchDto filters)
    {
        var result = await _rideService.SearchAsync(filters).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Detalle de un viaje (público)</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RideDto>> GetById(Guid id)
    {
        var ride = await _rideService.GetByIdAsync(id).ConfigureAwait(false);
        return Ok(ride);
    }

    /// <summary>Viajes publicados por el usuario autenticado (como conductor)</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<RideDto>>> GetMine()
    {
        var rides = await _rideService.GetMyRidesAsDriverAsync(CurrentUserId).ConfigureAwait(false);
        return Ok(rides);
    }

    /// <summary>Publicar un nuevo viaje</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<RideDto>> Create([FromBody] CreateRideDto dto)
    {
        var ride = await _rideService.CreateAsync(CurrentUserId, dto).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = ride.Id }, ride);
    }

    /// <summary>Actualizar un viaje propio</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<RideDto>> Update(Guid id, [FromBody] UpdateRideDto dto)
    {
        var ride = await _rideService.UpdateAsync(CurrentUserId, id, dto).ConfigureAwait(false);
        return Ok(ride);
    }

    /// <summary>Cancelar un viaje propio</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await _rideService.CancelAsync(CurrentUserId, id).ConfigureAwait(false);
        return NoContent();
    }
}
