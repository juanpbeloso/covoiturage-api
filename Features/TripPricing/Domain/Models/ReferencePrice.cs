namespace SubiteAPI.Features.TripPricing.Domain.Models;

public class ReferencePrice
{
    public Guid Id { get; set; }
    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string TransportMode { get; set; } = "bus_semi_cama";
    public string Label { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
