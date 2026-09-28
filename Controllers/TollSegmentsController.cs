using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("api/toll-segments")]
[Tags("Trip Pricing")]
public class TollSegmentsController : ControllerBase
{
    private readonly ITollSegmentService _tolls;

    public TollSegmentsController(ITollSegmentService tolls) => _tolls = tolls;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TollSegment>>> List()
    {
        return Ok(await _tolls.ListAsync().ConfigureAwait(false));
    }
}

[ApiController]
[Route("admin/toll-segments")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminTollSegmentsController : ControllerBase
{
    private readonly ITollSegmentService _tolls;

    public AdminTollSegmentsController(ITollSegmentService tolls) => _tolls = tolls;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TollSegment>>> List()
    {
        return Ok(await _tolls.ListAsync().ConfigureAwait(false));
    }

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<TollSegment>>> Replace([FromBody] List<TollSegment> items)
    {
        return Ok(await _tolls.ReplaceAsync(items ?? new List<TollSegment>()).ConfigureAwait(false));
    }
}
