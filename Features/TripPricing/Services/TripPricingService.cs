using SubiteAPI.Exceptions;
using SubiteAPI.Features.TripPricing.Domain;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Features.TripPricing.Infrastructure.Repositories;

namespace SubiteAPI.Features.TripPricing.Services;

public interface ITripPricingService
{
    Task<TripPricingResult> CalculateAsync(TripPricingRequest request);
}

public class TripPricingService : ITripPricingService
{
    private readonly IPricingConfigRepository _configRepo;
    private readonly IReferencePriceRepository _referenceRepo;
    private readonly PricingCalculator _calculator;

    public TripPricingService(
        IPricingConfigRepository configRepo,
        IReferencePriceRepository referenceRepo,
        PricingCalculator calculator)
    {
        _configRepo = configRepo;
        _referenceRepo = referenceRepo;
        _calculator = calculator;
    }

    public async Task<TripPricingResult> CalculateAsync(TripPricingRequest request)
    {
        if (request.PassengerCount < 1)
        {
            throw new BusinessException("PRICING_001", "PassengerCount debe ser al menos 1.");
        }

        if (request.DistanceKm <= 0)
        {
            throw new BusinessException("PRICING_002", "DistanceKm debe ser mayor a 0.");
        }

        PricingConfig? config;
        if (!string.IsNullOrWhiteSpace(request.ConfigOverrideId) &&
            Guid.TryParse(request.ConfigOverrideId, out var configId))
        {
            config = await _configRepo.GetByIdAsync(configId).ConfigureAwait(false);
            if (config == null)
            {
                throw new BusinessException("PRICING_003", "Configuración de pricing no encontrada.");
            }
        }
        else
        {
            config = await _configRepo.GetActiveAsync().ConfigureAwait(false);
            if (config == null)
            {
                throw new BusinessException("PRICING_004", "No hay configuración de pricing activa.");
            }
        }

        var transportMode = string.IsNullOrWhiteSpace(request.ReferenceTransportMode)
            ? "bus_semi_cama"
            : request.ReferenceTransportMode.Trim();

        var reference = await _referenceRepo.FindActiveAsync(
            request.OriginCity,
            request.DestinationCity,
            transportMode).ConfigureAwait(false);

        return _calculator.Calculate(request, config, reference);
    }
}
