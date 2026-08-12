using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.Features.TripPricing.Domain.Models;

namespace SubiteAPI.Features.TripPricing.Infrastructure.Repositories;

public interface IPricingConfigRepository
{
    Task<IReadOnlyList<PricingConfig>> GetAllAsync();
    Task<PricingConfig?> GetByIdAsync(Guid id);
    Task<PricingConfig?> GetActiveAsync();
    Task<PricingConfig> AddAsync(PricingConfig config);
    Task UpdateAsync(PricingConfig config);
    Task DeactivateAllAsync();
}

public class PricingConfigRepository : IPricingConfigRepository
{
    private readonly AppDbContext _db;

    public PricingConfigRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PricingConfig>> GetAllAsync() =>
        await _db.PricingConfigs.AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.UpdatedAt)
            .ToListAsync()
            .ConfigureAwait(false);

    public async Task<PricingConfig?> GetByIdAsync(Guid id) =>
        await _db.PricingConfigs.FirstOrDefaultAsync(c => c.Id == id).ConfigureAwait(false);

    public async Task<PricingConfig?> GetActiveAsync() =>
        await _db.PricingConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive)
            .ConfigureAwait(false);

    public async Task<PricingConfig> AddAsync(PricingConfig config)
    {
        _db.PricingConfigs.Add(config);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        return config;
    }

    public async Task UpdateAsync(PricingConfig config)
    {
        _db.PricingConfigs.Update(config);
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task DeactivateAllAsync()
    {
        await _db.PricingConfigs
            .Where(c => c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false))
            .ConfigureAwait(false);
    }
}
