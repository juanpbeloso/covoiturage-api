namespace SubiteAPI.Models;

public enum VerificationType
{
    DniFront,
    DniBack,
    DriverLicense,
    Selfie,
    VehicleRegistration
}

public enum VerificationStatus
{
    Pending,
    Approved,
    Rejected
}

public class Verification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public VerificationType Type { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }

    // Navegación
    public User User { get; set; } = null!;
}
