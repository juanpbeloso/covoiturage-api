using System.ComponentModel.DataAnnotations;

namespace SubiteAPI.DTOs;

public class UpsertVehicleDto
{
    [Required]
    public string Brand { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = string.Empty;

    [Required]
    public string Color { get; set; } = string.Empty;

    [Required]
    public string LicensePlate { get; set; } = string.Empty;

    [Range(1950, 2100)]
    public int Year { get; set; }

    public string? ImageUrl { get; set; }
}

public class VehicleDto
{
    public Guid Id { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? ImageUrl { get; set; }
}
