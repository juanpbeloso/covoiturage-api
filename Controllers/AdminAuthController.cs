using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("admin/auth")]
[Tags("Admin")]
public class AdminAuthController : ApiControllerBase
{
    private readonly IAdminAuthService _adminAuth;

    public AdminAuthController(IAdminAuthService adminAuth) => _adminAuth = adminAuth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AdminLoginResponseDto>> Login([FromBody] AdminLoginDto dto)
    {
        var result = await _adminAuth.LoginAsync(dto).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<AdminMeDto>> Me()
    {
        var result = await _adminAuth.GetMeAsync(CurrentUserId).ConfigureAwait(false);
        return Ok(result);
    }
}
