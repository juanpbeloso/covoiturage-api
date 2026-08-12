using SubiteAPI.Exceptions;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Features.TripPricing.Infrastructure.Repositories;

namespace SubiteAPI.Features.TripPricing.Services;

public interface IPricingConfigService
{
    Task<IReadOnlyList<PricingConfig>> GetAllAsync();
    Task<PricingConfig?> GetActiveAsync();
    Task<PricingConfig> CreateAsync(PricingConfig config);
    Task<PricingConfig> UpdateAsync(Guid id, PricingConfig update);
    Task ActivateAsync(Guid id);
}

public class PricingConfigService : IPricingConfigService
{
    private readonly IPricingConfigRepository _repo;

    public PricingConfigService(IPricingConfigRepository repo) => _repo = repo;

    public Task<IReadOnlyList<PricingConfig>> GetAllAsync() => _repo.GetAllAsync();

    public Task<PricingConfig?> GetActiveAsync() => _repo.GetActiveAsync();

    public async Task<PricingConfig> CreateAsync(PricingConfig config)
    {
        config.Id = Guid.NewGuid();
        config.CreatedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        if (config.IsActive)
        {
            await _repo.DeactivateAllAsync().ConfigureAwait(false);
        }
        return await _repo.AddAsync(config).ConfigureAwait(false);
    }

    public async Task<PricingConfig> UpdateAsync(Guid id, PricingConfig update)
    {
        var existing = await _repo.GetByIdAsync(id).ConfigureAwait(false)
            ?? throw new BusinessException("PRICING_005", "Configuración no encontrada.");

        existing.Name = update.Name;
        existing.FuelPricePerLiter = update.FuelPricePerLiter;
        existing.KmPerLiter = update.KmPerLiter;
        existing.WearCostPerKm = update.WearCostPerKm;
        existing.MaxPriceRatioVsReference = update.MaxPriceRatioVsReference;
        existing.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(existing).ConfigureAwait(false);
        return existing;
    }

    public async Task ActivateAsync(Guid id)
    {
        var config = await _repo.GetByIdAsync(id).ConfigureAwait(false)
            ?? throw new BusinessException("PRICING_005", "Configuración no encontrada.");

        await _repo.DeactivateAllAsync().ConfigureAwait(false);
        config.IsActive = true;
        config.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(config).ConfigureAwait(false);
    }
}
