using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

/// <summary>Pagos con MercadoPago Checkout Pro.</summary>
[Route("api/payments")]
[Tags("Payments")]
public class PaymentsController : ApiControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService) => _paymentService = paymentService;

    /// <summary>Crea reserva + preferencia de pago y devuelve la URL de checkout.</summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutPaymentResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckoutPaymentResultDto>> Checkout([FromBody] CheckoutPaymentDto dto)
    {
        var result = await _paymentService.CheckoutAsync(CurrentUserId, dto).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Retorno de MercadoPago tras pago aprobado (cierra el browser en la app).</summary>
    [HttpGet("return/success")]
    [AllowAnonymous]
    [Produces("text/html")]
    public ContentResult ReturnSuccess() =>
        PaymentReturnPage("Pago recibido", "Volviendo a Subite…", "success");

    [HttpGet("return/failure")]
    [AllowAnonymous]
    [Produces("text/html")]
    public ContentResult ReturnFailure() =>
        PaymentReturnPage("Pago no completado", "Volviendo a Subite…", "failure");

    [HttpGet("return/pending")]
    [AllowAnonymous]
    [Produces("text/html")]
    public ContentResult ReturnPending() =>
        PaymentReturnPage("Pago pendiente", "Volviendo a Subite…", "pending");

    /// <summary>Cancela reserva pendiente si el pago no se completó.</summary>
    [HttpPost("reservation/{reservationId:guid}/abandon")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusDto>> AbandonCheckout(Guid reservationId)
    {
        var status = await _paymentService.AbandonCheckoutAsync(CurrentUserId, reservationId).ConfigureAwait(false);
        return Ok(status);
    }

    private static ContentResult PaymentReturnPage(string title, string message, string outcome) =>
        new()
        {
            Content = $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <meta http-equiv="refresh" content="0;url=subite://payments/return/{{outcome}}" />
              <title>{{title}} · Subite</title>
              <style>
                body { font-family: system-ui, sans-serif; margin: 0; padding: 2rem; background: #f5f5f5; color: #111; }
                main { max-width: 28rem; margin: 0 auto; background: #fff; border-radius: 12px; padding: 1.5rem; }
                h1 { font-size: 1.25rem; margin: 0 0 0.75rem; }
                p { margin: 0; line-height: 1.5; color: #444; }
              </style>
            </head>
            <body>
              <main>
                <h1>{{title}}</h1>
                <p>{{message}}</p>
              </main>
              <script>
                window.location.replace("subite://payments/return/{{outcome}}");
                setTimeout(function () {
                  window.location.href = "subite://payments/return/{{outcome}}";
                }, 300);
              </script>
            </body>
            </html>
            """,
            ContentType = "text/html"
        };

    /// <summary>Estado del pago de una reserva (desde nuestra BD).</summary>
    [HttpGet("reservation/{reservationId:guid}/status")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusDto>> GetStatus(Guid reservationId)
    {
        var status = await _paymentService.GetStatusAsync(CurrentUserId, reservationId).ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>Sincroniza el estado consultando MercadoPago (útil sin webhook en local).</summary>
    [HttpPost("reservation/{reservationId:guid}/sync")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentStatusDto>> SyncStatus(Guid reservationId)
    {
        var status = await _paymentService.SyncStatusAsync(CurrentUserId, reservationId).ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>Webhook IPN de MercadoPago.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Webhook(
        [FromQuery] string? topic,
        [FromQuery] string? id,
        [FromQuery(Name = "data.id")] string? dataId)
    {
        long? paymentId = null;

        if (string.Equals(topic, "payment", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(id, out var queryId))
        {
            paymentId = queryId;
        }

        if (paymentId == null)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var notification = JsonSerializer.Deserialize<MercadoPagoWebhookNotificationDto>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (notification != null &&
                    string.Equals(notification.Type, "payment", StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(notification.Data?.Id, out var bodyId))
                {
                    paymentId = bodyId;
                }
            }
        }

        if (paymentId == null && long.TryParse(dataId, out var altId))
        {
            paymentId = altId;
        }

        if (paymentId != null)
        {
            await _paymentService.ProcessWebhookPaymentAsync(paymentId.Value).ConfigureAwait(false);
        }

        return Ok();
    }
}
