namespace SubiteAPI.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public Guid RideId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    // Navegación
    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
    public Ride Ride { get; set; } = null!;
}
