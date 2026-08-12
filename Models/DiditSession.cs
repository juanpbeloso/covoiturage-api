namespace SubiteAPI.Models;

public class DiditSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>UUID de sesión en Didit.</summary>
    public string SessionId { get; set; } = string.Empty;

    public string Status { get; set; } = "Not Started";
    public string? VerificationUrl { get; set; }
    public string? LastEventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
