using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/settings")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminSettingsController : ControllerBase
{
    private readonly IPlatformSettingsService _settings;
    private readonly IMapsDirectionsService _maps;

    public AdminSettingsController(IPlatformSettingsService settings, IMapsDirectionsService maps)
    {
        _settings = settings;
        _maps = maps;
    }

    [HttpGet]
    public async Task<ActionResult<PlatformSettingsDto>> Get()
    {
        var settings = await _settings.GetAsync().ConfigureAwait(false);
        return Ok(settings);
    }

    [HttpPut("commission")]
    public async Task<ActionResult<PlatformSettingsDto>> UpdateCommission([FromBody] UpdateCommissionDto dto)
    {
        var settings = await _settings.UpdateCommissionPercentAsync(dto.PlatformCommissionPercent)
            .ConfigureAwait(false);
        return Ok(settings);
    }

    [HttpGet("apis")]
    public async Task<ActionResult<ExternalApiBudgetDto>> GetApis()
    {
        var budget = await _maps.GetBudgetAsync().ConfigureAwait(false);
        return Ok(budget);
    }

    [HttpPut("apis")]
    public async Task<ActionResult<ExternalApiBudgetDto>> UpdateApis([FromBody] UpdateExternalApiBudgetDto dto)
    {
        var budget = await _maps.UpdateBudgetAsync(dto).ConfigureAwait(false);
        return Ok(budget);
    }
}
