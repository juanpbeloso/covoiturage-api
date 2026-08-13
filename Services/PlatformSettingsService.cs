using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Models;
using SubiteAPI.Options;

namespace SubiteAPI.Services;

public interface IPlatformSettingsService
{
    Task<PlatformSettingsDto> GetAsync();
    Task<decimal> GetCommissionRateAsync();
    Task<PlatformSettingsDto> UpdateCommissionPercentAsync(decimal percent);
}

public class PlatformSettingsService : IPlatformSettingsService
{
    private const int SingletonId = 1;

    private readonly AppDbContext _db;
    private readonly MercadoPagoOptions _mpDefaults;

    public PlatformSettingsService(AppDbContext db, IOptions<MercadoPagoOptions> mpDefaults)
    {
        _db = db;
        _mpDefaults = mpDefaults.Value;
    }

    public async Task<PlatformSettingsDto> GetAsync()
    {
        var entity = await GetOrCreateEntityAsync().ConfigureAwait(false);
        return Map(entity);
    }

    public async Task<decimal> GetCommissionRateAsync()
    {
        var entity = await _db.PlatformSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SingletonId)
            .ConfigureAwait(false);

        return entity?.PlatformCommissionRate ?? _mpDefaults.PlatformCommissionRate;
    }

    public async Task<PlatformSettingsDto> UpdateCommissionPercentAsync(decimal percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "La comisión debe estar entre 0 y 100.");
        }

        var entity = await GetOrCreateEntityAsync().ConfigureAwait(false);
        entity.PlatformCommissionRate = percent / 100m;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync().ConfigureAwait(false);
        return Map(entity);
    }

    private async Task<PlatformSettings> GetOrCreateEntityAsync()
    {
        var entity = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Id == SingletonId)
            .ConfigureAwait(false);

        if (entity != null)
        {
            return entity;
        }

        entity = new PlatformSettings
        {
            Id = SingletonId,
            PlatformCommissionRate = _mpDefaults.PlatformCommissionRate,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PlatformSettings.Add(entity);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        return entity;
    }

    private static PlatformSettingsDto Map(PlatformSettings entity) => new()
    {
        PlatformCommissionRate = entity.PlatformCommissionRate,
        PlatformCommissionPercent = Math.Round(entity.PlatformCommissionRate * 100m, 2),
        UpdatedAt = entity.UpdatedAt
    };
}
