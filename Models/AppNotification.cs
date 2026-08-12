namespace SubiteAPI.Models;

public class AppNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>booking | payment | reminder | update | system</summary>
    public string Type { get; set; } = "system";

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Deep link o ruta relativa, ej. /rides/reservation-detail?id=...</summary>
    public string? ActionUrl { get; set; }

    /// <summary>JSON opcional con ids de dominio (reservationId, rideId, ...).</summary>
    public string? DataJson { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public User User { get; set; } = null!;
}
