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
    private readonly IPasswordResetService _passwordReset;
    private readonly IEmailVerificationService _emailVerification;

    public AuthController(
        IAuthService authService,
        IPasswordResetService passwordReset,
        IEmailVerificationService emailVerification)
    {
        _authService = authService;
        _passwordReset = passwordReset;
        _emailVerification = emailVerification;
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
        var result = await _authService.AppleLoginAsync(dto.IdToken, dto.FullName).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Solicita un código de 6 dígitos por email para resetear la contraseña.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        await _passwordReset.RequestCodeAsync(dto).ConfigureAwait(false);
        return Ok(new
        {
            success = true,
            message = "Si el correo existe, te enviamos un código para restablecer la contraseña."
        });
    }

    /// <summary>Valida el código recibido por email (sin cambiar la contraseña todavía).</summary>
    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDto dto)
    {
        await _passwordReset.VerifyCodeAsync(dto).ConfigureAwait(false);
        return Ok(new { success = true, message = "Código válido." });
    }

    /// <summary>Cambia la contraseña usando email + código válidos.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        await _passwordReset.ResetPasswordAsync(dto).ConfigureAwait(false);
        return Ok(new { success = true, message = "Contraseña actualizada. Ya podés iniciar sesión." });
    }

    /// <summary>Valida el código de 6 dígitos enviado al registrarse.</summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
    {
        await _emailVerification.VerifyCodeAsync(dto).ConfigureAwait(false);
        return Ok(new { success = true, message = "Email verificado." });
    }

    /// <summary>Reenvía el código de verificación de email.</summary>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
    {
        await _emailVerification.ResendCodeAsync(dto).ConfigureAwait(false);
        return Ok(new
        {
            success = true,
            message = "Si el correo existe, te enviamos un nuevo código."
        });
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
