namespace SubiteAPI.Features.TripPricing.Domain.Models;

public class PricingConfig
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal FuelPricePerLiter { get; set; }
    public double KmPerLiter { get; set; }
    public decimal WearCostPerKm { get; set; }
    public decimal MaxPriceRatioVsReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
