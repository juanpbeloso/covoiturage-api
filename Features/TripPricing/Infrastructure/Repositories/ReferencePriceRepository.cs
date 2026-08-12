using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.Features.TripPricing.Domain.Models;

namespace SubiteAPI.Features.TripPricing.Infrastructure.Repositories;

public interface IReferencePriceRepository
{
    Task<IReadOnlyList<ReferencePrice>> SearchAsync(string? origin, string? destination);
    Task<ReferencePrice?> GetByIdAsync(Guid id);
    Task<ReferencePrice?> FindActiveAsync(string origin, string destination, string transportMode);
    Task<ReferencePrice> AddAsync(ReferencePrice price);
    Task UpdateAsync(ReferencePrice price);
}

public class ReferencePriceRepository : IReferencePriceRepository
{
    private readonly AppDbContext _db;

    public ReferencePriceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReferencePrice>> SearchAsync(string? origin, string? destination)
    {
        var query = _db.ReferencePrices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(origin))
        {
            var o = origin.Trim();
            query = query.Where(r => EF.Functions.ILike(r.OriginCity, $"%{o}%"));
        }

        if (!string.IsNullOrWhiteSpace(destination))
        {
            var d = destination.Trim();
            query = query.Where(r => EF.Functions.ILike(r.DestinationCity, $"%{d}%"));
        }

        return await query
            .OrderByDescending(r => r.ValidFrom)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<ReferencePrice?> GetByIdAsync(Guid id) =>
        await _db.ReferencePrices.FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);

    public async Task<ReferencePrice?> FindActiveAsync(string origin, string destination, string transportMode)
    {
        var now = DateTime.UtcNow;
        var o = origin.Trim();
        var d = destination.Trim();
        var originPattern = $"%{o}%";
        var destPattern = $"%{d}%";

        var baseQuery = _db.ReferencePrices.AsNoTracking()
            .Where(r =>
                r.TransportMode == transportMode &&
                r.ValidFrom <= now &&
                (r.ValidTo == null || r.ValidTo > now));

        var exact = await baseQuery
            .Where(r =>
                EF.Functions.ILike(r.OriginCity, o) &&
                EF.Functions.ILike(r.DestinationCity, d))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (exact != null) return exact;

        var partial = await baseQuery
            .Where(r =>
                EF.Functions.ILike(r.OriginCity, originPattern) &&
                EF.Functions.ILike(r.DestinationCity, destPattern))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (partial != null) return partial;

        var candidates = await baseQuery
            .Where(r => EF.Functions.ILike(r.OriginCity, originPattern))
            .OrderByDescending(r => r.ValidFrom)
            .Take(20)
            .ToListAsync()
            .ConfigureAwait(false);

        return candidates.FirstOrDefault(r =>
            d.Contains(r.DestinationCity, StringComparison.OrdinalIgnoreCase) ||
            r.DestinationCity.Contains(d, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ReferencePrice> AddAsync(ReferencePrice price)
    {
        _db.ReferencePrices.Add(price);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        return price;
    }

    public async Task UpdateAsync(ReferencePrice price)
    {
        _db.ReferencePrices.Update(price);
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }
}
