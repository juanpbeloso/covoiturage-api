using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("api/maps")]
[Authorize]
public class MapsController : ControllerBase
{
    private readonly IMapsDirectionsService _maps;

    public MapsController(IMapsDirectionsService maps) => _maps = maps;

    [HttpPost("directions")]
    public async Task<ActionResult<MapsDirectionsResultDto>> Directions([FromBody] MapsDirectionsRequestDto dto)
    {
        var result = await _maps.GetRouteAsync(dto).ConfigureAwait(false);
        return Ok(result);
    }
}
