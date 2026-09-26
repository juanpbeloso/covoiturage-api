using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/viajes")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminViajesController : ControllerBase
{
    private readonly IAdminPanelService _admin;

    public AdminViajesController(IAdminPanelService admin) => _admin = admin;

    [HttpGet]
    public async Task<ActionResult<AdminPagedResultDto<AdminViajeDto>>> List(
        [FromQuery] string? query,
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await _admin.GetViajesAsync(query, estado, page, pageSize).ConfigureAwait(false));
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] AdminCancelarViajeDto? dto)
    {
        await _admin.CancelarViajeAsync(id, dto?.Reason).ConfigureAwait(false);
        return NoContent();
    }
}
