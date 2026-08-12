using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ApiControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    /// <summary>Reservar asiento(s) en un viaje</summary>
    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create([FromBody] CreateReservationDto dto)
    {
        var reservation = await _reservationService.CreateAsync(CurrentUserId, dto).ConfigureAwait(false);
        return Ok(reservation);
    }

    /// <summary>Reservas del usuario autenticado (como pasajero)</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> GetMine()
    {
        var reservations = await _reservationService.GetMyReservationsAsync(CurrentUserId).ConfigureAwait(false);
        return Ok(reservations);
    }

    /// <summary>Reservas de un viaje propio (solo el conductor)</summary>
    [HttpGet("ride/{rideId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> GetForRide(Guid rideId)
    {
        var reservations = await _reservationService.GetForRideAsync(CurrentUserId, rideId).ConfigureAwait(false);
        return Ok(reservations);
    }

    /// <summary>Confirmar una reserva (solo el conductor del viaje)</summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ReservationDto>> Confirm(Guid id)
    {
        var reservation = await _reservationService.ConfirmAsync(CurrentUserId, id).ConfigureAwait(false);
        return Ok(reservation);
    }

    /// <summary>Cancelar una reserva (pasajero o conductor)</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ReservationDto>> Cancel(Guid id, [FromBody] CancelReservationDto? dto)
    {
        var reservation = await _reservationService.CancelAsync(CurrentUserId, id, dto?.Reason).ConfigureAwait(false);
        return Ok(reservation);
    }
}
