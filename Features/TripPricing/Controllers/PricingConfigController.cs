using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Features.TripPricing.Services;
using SubiteAPI.Models;

namespace SubiteAPI.Features.TripPricing.Controllers;

/// <summary>Configuración de parámetros de pricing (combustible, desgaste, etc.).</summary>
[ApiController]
[Route("api/pricing-config")]
[Tags("Trip Pricing")]
[Produces("application/json")]
public class PricingConfigController : ControllerBase
{
    private readonly IPricingConfigService _service;

    public PricingConfigController(IPricingConfigService service) => _service = service;

    /// <summary>Lista todas las configuraciones de pricing.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PricingConfig>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PricingConfig>>> GetAll()
    {
        var items = await _service.GetAllAsync().ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>Devuelve la configuración activa del sistema.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(PricingConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingConfig>> GetActive()
    {
        var config = await _service.GetActiveAsync().ConfigureAwait(false);
        if (config == null) return NotFound();
        return Ok(config);
    }

    /// <summary>Crea una nueva configuración de pricing.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(PricingConfig), StatusCodes.Status201Created)]
    public async Task<ActionResult<PricingConfig>> Create([FromBody] PricingConfig config)
    {
        var created = await _service.CreateAsync(config).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    /// <summary>Actualiza una configuración existente.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(PricingConfig), StatusCodes.Status200OK)]
    public async Task<ActionResult<PricingConfig>> Update(Guid id, [FromBody] PricingConfig config)
    {
        var updated = await _service.UpdateAsync(id, config).ConfigureAwait(false);
        return Ok(updated);
    }

    /// <summary>Activa una configuración (desactiva las demás).</summary>
    [HttpPut("{id:guid}/activate")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _service.ActivateAsync(id).ConfigureAwait(false);
        return NoContent();
    }
}
