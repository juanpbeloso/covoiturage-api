using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Options;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

/// <summary>OAuth MercadoPago para conductores (Split marketplace 1:1).</summary>
[ApiController]
[Route("api/conductores/mercadopago")]
[Tags("Conductores MercadoPago")]
public class ConductorMercadoPagoController : ApiControllerBase
{
    private readonly IMercadoPagoTokenService _tokens;
    private readonly AppDbContext _db;
    private readonly AppOptions _appOptions;

    public ConductorMercadoPagoController(
        IMercadoPagoTokenService tokens,
        AppDbContext db,
        IOptions<AppOptions> appOptions)
    {
        _tokens = tokens;
        _db = db;
        _appOptions = appOptions.Value;
    }

    /// <summary>
    /// Arma la URL de autorización OAuth.
    /// Con Accept: application/json (o ?format=json) responde JSON; si no, redirige al browser de MP.
    /// </summary>
    [HttpGet("conectar")]
    [Authorize]
    [ProducesResponseType(typeof(MercadoPagoConnectResponseDto), StatusCodes.Status200OK)]
    public IActionResult Conectar([FromQuery] string? format)
    {
        var url = _tokens.BuildAuthorizationUrl(CurrentUserId);
        var wantsJson =
            string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

        if (wantsJson)
        {
            return Ok(new MercadoPagoConnectResponseDto { AuthorizationUrl = url });
        }

        return Redirect(url);
    }

    /// <summary>Callback OAuth de MercadoPago. Intercambia code por tokens y vuelve a la app.</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription)
    {
        var frontend = string.IsNullOrWhiteSpace(_appOptions.FrontendUrl)
            ? "subite://"
            : _appOptions.FrontendUrl.Trim();
        string DeepLink(string path)
        {
            path = path.TrimStart('/');
            if (frontend.EndsWith("://", StringComparison.Ordinal))
            {
                return $"{frontend}{path}";
            }
            return $"{frontend.TrimEnd('/')}/{path}";
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            var msg = Uri.EscapeDataString(errorDescription ?? error);
            return Redirect(DeepLink($"mercadopago/callback?status=error&message={msg}"));
        }

        try
        {
            await _tokens.CompleteOAuthAsync(code ?? "", state ?? "").ConfigureAwait(false);
            return Redirect(DeepLink("mercadopago/callback?status=success"));
        }
        catch (Exception ex)
        {
            var msg = Uri.EscapeDataString(ex.Message);
            return Redirect(DeepLink($"mercadopago/callback?status=error&message={msg}"));
        }
    }

    /// <summary>Estado de conexión MP del conductor autenticado.</summary>
    [HttpGet("estado")]
    [Authorize]
    [ProducesResponseType(typeof(MercadoPagoConnectionStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MercadoPagoConnectionStatusDto>> Estado()
    {
        var status = await _tokens.GetStatusAsync(CurrentUserId).ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>Desconecta la cuenta MP del conductor (para re-autorizar).</summary>
    [HttpDelete("desconectar")]
    [Authorize]
    public async Task<IActionResult> Desconectar()
    {
        var row = await _db.ConductorMercadoPagos
            .FirstOrDefaultAsync(c => c.ConductorId == CurrentUserId)
            .ConfigureAwait(false);

        if (row != null)
        {
            _db.ConductorMercadoPagos.Remove(row);
            await _db.SaveChangesAsync().ConfigureAwait(false);
        }

        return NoContent();
    }
}
