namespace SubiteAPI.Features.TripPricing.Domain.Models;

public class TripPricingRequest
{
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public decimal TollCostTotal { get; set; }
    public int PassengerCount { get; set; }
    public bool DriverPaysShare { get; set; }
    public string? ConfigOverrideId { get; set; }
    public string? ReferenceTransportMode { get; set; }
}
