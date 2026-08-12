using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Models;

namespace SubiteAPI.Services;

public interface INotificationService
{
    Task RegisterPushTokenAsync(Guid userId, string token);
    Task ClearPushTokenAsync(Guid userId);
    Task<IReadOnlyList<AppNotificationDto>> GetMineAsync(Guid userId, int take = 50);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkReadAsync(Guid userId, Guid notificationId);
    Task MarkAllReadAsync(Guid userId);
    Task NotifyUserAsync(
        Guid userId,
        string type,
        string title,
        string body,
        string? actionUrl = null,
        object? data = null);
    Task SendTestAsync(Guid userId, string? title, string? body);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task RegisterPushTokenAsync(Guid userId, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false);
        if (user == null) return;

        var trimmed = token.Trim();
        user.FcmToken = trimmed;
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ClearPushTokenAsync(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false);
        if (user == null || string.IsNullOrEmpty(user.FcmToken)) return;
        user.FcmToken = null;
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AppNotificationDto>> GetMineAsync(Guid userId, int take = 50)
    {
        take = Math.Clamp(take, 1, 100);
        var items = await _db.AppNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync()
            .ConfigureAwait(false);

        return items.Select(Map).ToList();
    }

    public Task<int> GetUnreadCountAsync(Guid userId) =>
        _db.AppNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkReadAsync(Guid userId, Guid notificationId)
    {
        var item = await _db.AppNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId)
            .ConfigureAwait(false);
        if (item == null || item.IsRead) return;

        item.IsRead = true;
        item.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        var unread = await _db.AppNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync()
            .ConfigureAwait(false);

        if (unread.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAt = now;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task SendTestAsync(Guid userId, string? title, string? body)
    {
        await NotifyUserAsync(
            userId,
            "system",
            title ?? "Prueba Subite",
            body ?? "Si ves esto, las notificaciones están funcionando.",
            actionUrl: "/profile/notifications-list").ConfigureAwait(false);
    }

    public async Task NotifyUserAsync(
        Guid userId,
        string type,
        string title,
        string body,
        string? actionUrl = null,
        object? data = null)
    {
        var entity = new AppNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            ActionUrl = actionUrl,
            DataJson = data == null ? null : JsonSerializer.Serialize(data),
            CreatedAt = DateTime.UtcNow
        };

        _db.AppNotifications.Add(entity);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        var token = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FcmToken)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(token))
        {
            await TrySendExpoPushAsync(token, title, body, type, actionUrl, entity.Id).ConfigureAwait(false);
        }
    }

    private async Task TrySendExpoPushAsync(
        string token,
        string title,
        string body,
        string type,
        string? actionUrl,
        Guid notificationId)
    {
        // Expo Push tokens empiezan con ExponentPushToken[...].
        // También aceptamos tokens FCM crudos más adelante.
        try
        {
            var client = _httpClientFactory.CreateClient("ExpoPush");
            var payload = new
            {
                to = token,
                sound = "default",
                title,
                body,
                data = new
                {
                    type,
                    actionUrl,
                    notificationId
                }
            };

            using var response = await client
                .PostAsJsonAsync("https://exp.host/--/api/v2/push/send", payload)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning(
                    "Expo push falló ({Status}): {Body}",
                    (int)response.StatusCode,
                    text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar push Expo");
        }
    }

    private static AppNotificationDto Map(AppNotification n) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Title = n.Title,
        Body = n.Body,
        ActionUrl = n.ActionUrl,
        DataJson = n.DataJson,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt
    };
}
