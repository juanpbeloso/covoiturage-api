using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/pagos")]
[Tags("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminPagosController : ControllerBase
{
    private readonly IAdminPanelService _admin;

    public AdminPagosController(IAdminPanelService admin) => _admin = admin;

    [HttpGet("ganancias")]
    public async Task<ActionResult<AdminGananciasDto>> Ganancias([FromQuery] int days = 30)
    {
        return Ok(await _admin.GetGananciasAsync(days).ConfigureAwait(false));
    }
}
