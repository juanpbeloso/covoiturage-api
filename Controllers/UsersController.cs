using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

/// <summary>Perfiles públicos de usuarios.</summary>
[Route("api/users")]
[Tags("Users")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService) => _authService = authService;

    /// <summary>Perfil público de un conductor (sin datos sensibles).</summary>
    [HttpGet("{id:guid}/public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicDriverProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicDriverProfileDto>> GetPublicProfile(Guid id)
    {
        var profile = await _authService.GetPublicDriverProfileAsync(id).ConfigureAwait(false);
        return Ok(profile);
    }
}
