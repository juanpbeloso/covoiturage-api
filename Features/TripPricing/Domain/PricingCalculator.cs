using SubiteAPI.Features.TripPricing.Domain.Models;

namespace SubiteAPI.Features.TripPricing.Domain;

public class PricingCalculator
{
    public TripPricingResult Calculate(
        TripPricingRequest request,
        PricingConfig config,
        ReferencePrice? reference)
    {
        var liters = request.DistanceKm / config.KmPerLiter;
        var fuelCost = (decimal)liters * config.FuelPricePerLiter;
        var tollCost = request.TollCostTotal;
        var wearCost = (decimal)request.DistanceKm * config.WearCostPerKm;
        var totalCost = fuelCost + tollCost + wearCost;

        var divisor = request.PassengerCount + (request.DriverPaysShare ? 1 : 0);
        if (divisor <= 0) divisor = 1;

        var pricePerPassenger = totalCost / divisor;
        var refPrice = reference?.Price ?? 0;
        var exceedsRef = refPrice > 0 &&
                         pricePerPassenger > refPrice * config.MaxPriceRatioVsReference;

        return new TripPricingResult
        {
            PricePerPassenger = Math.Round(pricePerPassenger, 0),
            TotalTripCost = Math.Round(totalCost, 0),
            FuelCost = Math.Round(fuelCost, 0),
            TollCost = Math.Round(tollCost, 0),
            WearCost = Math.Round(wearCost, 0),
            ReferencePriceUsed = refPrice,
            ReferenceLabel = reference?.Label ?? "Sin referencia",
            ExceedsReferencePrice = exceedsRef,
            PassengerSavingsVsReference = Math.Round(refPrice - pricePerPassenger, 0),
            DivisorCount = divisor,
            ConfigName = config.Name,
            FuelPricePerLiter = config.FuelPricePerLiter,
            KmPerLiter = config.KmPerLiter,
            WearCostPerKm = config.WearCostPerKm,
            MaxPriceRatioVsReference = config.MaxPriceRatioVsReference
        };
    }
}
