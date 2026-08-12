using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Registrar nuevo usuario con email y contraseña
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Login con email y contraseña
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Login con Google Sign-In
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponseDto>> GoogleLogin([FromBody] SocialLoginDto dto)
    {
        var result = await _authService.GoogleLoginAsync(dto.IdToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Login con Apple Sign-In
    /// </summary>
    [HttpPost("apple")]
    public async Task<ActionResult<AuthResponseDto>> AppleLogin([FromBody] SocialLoginDto dto)
    {
        var result = await _authService.AppleLoginAsync(dto.IdToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Refrescar access token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Verificar si el token actual es válido
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    public IActionResult VerifyToken()
    {
        return Ok(new { valid = true, message = "Token válido" });
    }

    /// <summary>
    /// Obtener el perfil del usuario autenticado
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetProfile()
    {
        var result = await _authService.GetProfileAsync(GetUserId()).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Actualizar el perfil del usuario autenticado
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var result = await _authService.UpdateProfileAsync(GetUserId(), dto).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Subir o reemplazar la foto de perfil
    /// </summary>
    [HttpPost("me/avatar")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<UserDto>> UploadAvatar(IFormFile file)
    {
        var result = await _authService.UploadAvatarAsync(GetUserId(), file).ConfigureAwait(false);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId))
        {
            throw new InvalidTokenException("AccessToken");
        }
        return userId;
    }
}
