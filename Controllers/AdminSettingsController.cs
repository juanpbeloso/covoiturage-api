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

    public AdminSettingsController(IPlatformSettingsService settings) => _settings = settings;

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
}
