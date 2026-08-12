namespace SubiteAPI.Models;

public class Review
{
    public Guid Id { get; set; }
    public Guid RideId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid ReviewedUserId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public Ride Ride { get; set; } = null!;
    public User Reviewer { get; set; } = null!;
    public User ReviewedUser { get; set; } = null!;
}
