namespace SubiteAPI.DTOs;

public class PublicDriverProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? ProfileImageUrl { get; set; }
    public bool IsVerified { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
    public int TripsAsDriver { get; set; }
    public PublicDriverVehicleDto? Vehicle { get; set; }
}

public class PublicDriverVehicleDto
{
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ImageUrl { get; set; }
}
