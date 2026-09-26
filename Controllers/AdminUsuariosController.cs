using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/usuarios")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminUsuariosController : ControllerBase
{
    private readonly IAdminPanelService _admin;

    public AdminUsuariosController(IAdminPanelService admin) => _admin = admin;

    [HttpGet]
    public async Task<ActionResult<AdminPagedResultDto<AdminUsuarioDto>>> List(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await _admin.GetUsuariosAsync(query, page, pageSize).ConfigureAwait(false));
    }

    [HttpPost("{id:guid}/bloquear")]
    public async Task<IActionResult> Bloquear(Guid id)
    {
        await _admin.BloquearUsuarioAsync(id, bloquear: true).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/desbloquear")]
    public async Task<IActionResult> Desbloquear(Guid id)
    {
        await _admin.BloquearUsuarioAsync(id, bloquear: false).ConfigureAwait(false);
        return NoContent();
    }
}
