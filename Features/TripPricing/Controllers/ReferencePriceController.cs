using Microsoft.AspNetCore.Mvc;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Features.TripPricing.Services;

namespace SubiteAPI.Features.TripPricing.Controllers;

/// <summary>Precios de referencia de transporte público por tramo.</summary>
[ApiController]
[Route("api/reference-prices")]
[Tags("Trip Pricing")]
[Produces("application/json")]
public class ReferencePriceController : ControllerBase
{
    private readonly IReferencePriceService _service;

    public ReferencePriceController(IReferencePriceService service) => _service = service;

    /// <summary>Lista precios de referencia con filtros opcionales por origen/destino.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReferencePrice>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReferencePrice>>> Search(
        [FromQuery] string? origin,
        [FromQuery] string? destination)
    {
        var items = await _service.SearchAsync(origin, destination).ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>Carga manualmente un precio de referencia.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReferencePrice), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReferencePrice>> Create([FromBody] ReferencePrice price)
    {
        var created = await _service.CreateAsync(price).ConfigureAwait(false);
        return CreatedAtAction(nameof(Search), new { origin = created.OriginCity }, created);
    }

    /// <summary>Actualiza un precio de referencia.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ReferencePrice), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferencePrice>> Update(Guid id, [FromBody] ReferencePrice price)
    {
        var updated = await _service.UpdateAsync(id, price).ConfigureAwait(false);
        return Ok(updated);
    }

    /// <summary>Elimina (soft delete) un precio de referencia.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.SoftDeleteAsync(id).ConfigureAwait(false);
        return NoContent();
    }
}
