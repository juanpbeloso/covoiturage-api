using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/reservas")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminReservasController : ControllerBase
{
    private readonly IAdminPanelService _admin;

    public AdminReservasController(IAdminPanelService admin) => _admin = admin;

    [HttpGet]
    public async Task<ActionResult<AdminPagedResultDto<AdminReservaDto>>> List(
        [FromQuery] string? query,
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await _admin.GetReservasAsync(query, estado, page, pageSize).ConfigureAwait(false));
    }
}
