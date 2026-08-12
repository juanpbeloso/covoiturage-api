using SubiteAPI.Exceptions;
using SubiteAPI.Features.TripPricing.Domain.Models;
using SubiteAPI.Features.TripPricing.Infrastructure.Repositories;

namespace SubiteAPI.Features.TripPricing.Services;

public interface IReferencePriceService
{
    Task<IReadOnlyList<ReferencePrice>> SearchAsync(string? origin, string? destination);
    Task<ReferencePrice> CreateAsync(ReferencePrice price);
    Task<ReferencePrice> UpdateAsync(Guid id, ReferencePrice update);
    Task SoftDeleteAsync(Guid id);
}

public class ReferencePriceService : IReferencePriceService
{
    private readonly IReferencePriceRepository _repo;

    public ReferencePriceService(IReferencePriceRepository repo) => _repo = repo;

    public Task<IReadOnlyList<ReferencePrice>> SearchAsync(string? origin, string? destination) =>
        _repo.SearchAsync(origin, destination);

    public async Task<ReferencePrice> CreateAsync(ReferencePrice price)
    {
        price.Id = Guid.NewGuid();
        price.UpdatedAt = DateTime.UtcNow;
        if (price.ValidFrom == default)
        {
            price.ValidFrom = DateTime.UtcNow;
        }
        return await _repo.AddAsync(price).ConfigureAwait(false);
    }

    public async Task<ReferencePrice> UpdateAsync(Guid id, ReferencePrice update)
    {
        var existing = await _repo.GetByIdAsync(id).ConfigureAwait(false)
            ?? throw new BusinessException("PRICING_006", "Precio de referencia no encontrado.");

        existing.OriginCity = update.OriginCity;
        existing.DestinationCity = update.DestinationCity;
        existing.TransportMode = update.TransportMode;
        existing.Label = update.Label;
        existing.Price = update.Price;
        existing.ValidFrom = update.ValidFrom;
        existing.ValidTo = update.ValidTo;
        existing.Source = update.Source;
        existing.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(existing).ConfigureAwait(false);
        return existing;
    }

    public async Task SoftDeleteAsync(Guid id)
    {
        var existing = await _repo.GetByIdAsync(id).ConfigureAwait(false)
            ?? throw new BusinessException("PRICING_006", "Precio de referencia no encontrado.");

        existing.ValidTo = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(existing).ConfigureAwait(false);
    }
}
