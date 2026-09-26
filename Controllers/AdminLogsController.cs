using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/logs")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminLogsController : ControllerBase
{
    private readonly IAdminPanelService _admin;

    public AdminLogsController(IAdminPanelService admin) => _admin = admin;

    [HttpGet]
    public async Task<ActionResult<AdminPagedResultDto<AdminLogDto>>> List(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await _admin.GetLogsAsync(query, page, pageSize).ConfigureAwait(false));
    }
}
