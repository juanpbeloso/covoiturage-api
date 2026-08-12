using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.DTOs;
using SubiteAPI.Services;

namespace SubiteAPI.Controllers;

[Authorize]
[Route("api/notifications")]
[Tags("Notifications")]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) =>
        _notifications = notifications;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppNotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppNotificationDto>>> Mine([FromQuery] int take = 50)
    {
        var items = await _notifications.GetMineAsync(CurrentUserId, take).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount()
    {
        var count = await _notifications.GetUnreadCountAsync(CurrentUserId).ConfigureAwait(false);
        return Ok(new { count });
    }

    [HttpPut("device-token")]
    public async Task<IActionResult> RegisterToken([FromBody] RegisterPushTokenDto dto)
    {
        await _notifications.RegisterPushTokenAsync(CurrentUserId, dto.Token).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("device-token")]
    public async Task<IActionResult> ClearToken()
    {
        await _notifications.ClearPushTokenAsync(CurrentUserId).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _notifications.MarkReadAsync(CurrentUserId, id).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(CurrentUserId).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Envía una notificación de prueba al usuario autenticado (útil en desarrollo).</summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendTest([FromBody] SendTestNotificationDto? dto)
    {
        await _notifications
            .SendTestAsync(CurrentUserId, dto?.Title, dto?.Body)
            .ConfigureAwait(false);
        return Ok(new { success = true, message = "Notificación de prueba creada (y push si hay token)." });
    }
}
