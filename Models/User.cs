using Microsoft.AspNetCore.Identity;

namespace SubiteAPI.Models;

public class User : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string? Bio { get; set; }
    public bool IsVerified { get; set; } = false;
    public bool IsDriver { get; set; } = false;
    public decimal Rating { get; set; } = 0;
    public int ReviewsCount { get; set; } = 0;
    public int TripsAsDriver { get; set; } = 0;
    public int TripsAsPassenger { get; set; } = 0;
    public string? FcmToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navegación
    public Vehicle? Vehicle { get; set; }
    public ICollection<Ride> RidesAsDriver { get; set; } = new List<Ride>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Verification> Verifications { get; set; } = new List<Verification>();
    public ICollection<DiditSession> DiditSessions { get; set; } = new List<DiditSession>();
    public ICollection<Review> ReviewsReceived { get; set; } = new List<Review>();
    public ICollection<Review> ReviewsGiven { get; set; } = new List<Review>();
}
