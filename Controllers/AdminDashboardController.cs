using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/dashboard")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminPanelService _admin;

    public AdminDashboardController(IAdminPanelService admin) => _admin = admin;

    [HttpGet("kpis")]
    public async Task<ActionResult<AdminDashboardKpisDto>> Kpis(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        return Ok(await _admin.GetKpisAsync(desde, hasta).ConfigureAwait(false));
    }

    [HttpGet("series")]
    public async Task<ActionResult<IReadOnlyList<AdminDashboardSeriePointDto>>> Series(
        [FromQuery] int days = 7)
    {
        return Ok(await _admin.GetSeriesAsync(days).ConfigureAwait(false));
    }
}
