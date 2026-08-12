using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[Route("api/[controller]")]
[AllowAnonymous]
public class LocationsController : ControllerBase
{
    private readonly IGeorefService _georefService;

    public LocationsController(IGeorefService georefService)
    {
        _georefService = georefService;
    }

    /// <summary>
    /// Busca localidades argentinas vía Georef (datos.gob.ar).
    /// Por defecto filtra al corredor Junín ↔ Retiro (~265 km, radio perpendicular configurable).
    /// Incluye CABA y GBA (Retiro, Nuñez, Vicente López).
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<LocalityDto>>> Search([FromQuery] LocalitySearchDto search)
    {
        var results = await _georefService.SearchLocalitiesAsync(search).ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Normaliza direcciones específicas (calle + localidad) vía Georef.
    /// Útil para puntos concretos: terminal, estación, dirección exacta.
    /// </summary>
    [HttpGet("addresses")]
    public async Task<ActionResult<IReadOnlyList<NormalizedAddressDto>>> SearchAddresses(
        [FromQuery] AddressSearchDto search)
    {
        var results = await _georefService.SearchAddressesAsync(search).ConfigureAwait(false);
        return Ok(results);
    }
}
