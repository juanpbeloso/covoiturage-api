namespace SubiteAPI.Features.TripPricing.Domain.Models;

/// <summary>Peaje o tramo de peaje sobre un corredor (ej. RN7).</summary>
public class TollSegment
{
    public Guid Id { get; set; }
    public string Corridor { get; set; } = "RN7";
    public string FromCity { get; set; } = string.Empty;
    public string ToCity { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Sequence { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
