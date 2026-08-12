using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[Authorize]
[Route("api/verification/didit")]
[Tags("Didit")]
public class DiditVerificationController : ApiControllerBase
{
    private readonly IDiditService _didit;

    public DiditVerificationController(IDiditService didit) => _didit = didit;

    /// <summary>Crea (o reutiliza) una sesión KYC de Didit para el usuario autenticado.</summary>
    [HttpPost("session")]
    [ProducesResponseType(typeof(DiditSessionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DiditSessionResultDto>> CreateSession()
    {
        var result = await _didit.CreateSessionAsync(CurrentUserId).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Estado de verificación de identidad del usuario.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(DiditStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DiditStatusDto>> GetStatus()
    {
        var status = await _didit.GetStatusAsync(CurrentUserId).ConfigureAwait(false);
        return Ok(status);
    }
}

[Route("api/webhooks/didit")]
[Tags("Didit")]
[ApiController]
public class DiditWebhookController : ControllerBase
{
    private readonly IDiditService _didit;

    public DiditWebhookController(IDiditService didit) => _didit = didit;

    /// <summary>Webhook IPN de Didit (status.updated).</summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync().ConfigureAwait(false);
        Request.Body.Position = 0;

        var signatureV2 = Request.Headers["X-Signature-V2"].FirstOrDefault();
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var timestamp = Request.Headers["X-Timestamp"].FirstOrDefault();

        await _didit.ProcessWebhookAsync(rawBody, signatureV2, signature, timestamp).ConfigureAwait(false);
        return Ok();
    }
}
