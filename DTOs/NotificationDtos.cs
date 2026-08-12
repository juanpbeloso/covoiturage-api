namespace SubiteAPI.DTOs;

public class AppNotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RegisterPushTokenDto
{
    public string Token { get; set; } = string.Empty;
}

public class SendTestNotificationDto
{
    public string? Title { get; set; }
    public string? Body { get; set; }
}
