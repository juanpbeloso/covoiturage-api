namespace SubiteAPI.Features.TripPricing.Domain.Models;

public class TripPricingResult
{
    public decimal PricePerPassenger { get; set; }
    public decimal TotalTripCost { get; set; }
    public decimal FuelCost { get; set; }
    public decimal TollCost { get; set; }
    public decimal WearCost { get; set; }
    public decimal ReferencePriceUsed { get; set; }
    public string ReferenceLabel { get; set; } = string.Empty;
    public bool ExceedsReferencePrice { get; set; }
    public decimal PassengerSavingsVsReference { get; set; }
    public int DivisorCount { get; set; }

    /// <summary>Snapshot de la config usada en el cálculo (para mostrar en UI).</summary>
    public string ConfigName { get; set; } = string.Empty;
    public decimal FuelPricePerLiter { get; set; }
    public double KmPerLiter { get; set; }
    public decimal WearCostPerKm { get; set; }
    public decimal MaxPriceRatioVsReference { get; set; }
}
