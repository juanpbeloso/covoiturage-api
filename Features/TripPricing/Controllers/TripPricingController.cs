using Microsoft.AspNetCore.Mvc;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Features.TripPricing.Services;

namespace SubiteAPI.Features.TripPricing.Controllers;

/// <summary>Cálculo de precio por pasajero según gastos reales y referencia de transporte público.</summary>
[ApiController]
[Route("api/trip-pricing")]
[Tags("Trip Pricing")]
[Produces("application/json")]
public class TripPricingController : ControllerBase
{
    private readonly ITripPricingService _pricingService;

    public TripPricingController(ITripPricingService pricingService) => _pricingService = pricingService;

    /// <summary>Calcula el precio sugerido por pasajero para un viaje.</summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(TripPricingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripPricingResult>> Calculate([FromBody] TripPricingRequest request)
    {
        var result = await _pricingService.CalculateAsync(request).ConfigureAwait(false);
        return Ok(result);
    }
}
