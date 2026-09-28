using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.Features.TripPricing.Domain.Models;

namespace SubiteAPI.Services;

public interface ITollSegmentService
{
    Task<IReadOnlyList<TollSegment>> ListAsync();
    Task<IReadOnlyList<TollSegment>> ReplaceAsync(IReadOnlyList<TollSegment> items);
    Task<decimal> ResolveAsync(string originCity, string destinationCity);
}

public class TollSegmentService : ITollSegmentService
{
    private readonly AppDbContext _db;

    public TollSegmentService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TollSegment>> ListAsync() =>
        await _db.TollSegments.AsNoTracking()
            .OrderBy(s => s.Corridor)
            .ThenBy(s => s.Sequence)
            .ToListAsync()
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TollSegment>> ReplaceAsync(IReadOnlyList<TollSegment> items)
    {
        var existing = await _db.TollSegments.ToListAsync().ConfigureAwait(false);
        _db.TollSegments.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var sequence = 1;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.FromCity) || string.IsNullOrWhiteSpace(item.ToCity))
            {
                continue;
            }

            _db.TollSegments.Add(new TollSegment
            {
                Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                Corridor = string.IsNullOrWhiteSpace(item.Corridor) ? "RN7" : item.Corridor.Trim(),
                FromCity = item.FromCity.Trim(),
                ToCity = item.ToCity.Trim(),
                Label = string.IsNullOrWhiteSpace(item.Label)
                    ? $"{item.FromCity.Trim()} → {item.ToCity.Trim()}"
                    : item.Label.Trim(),
                Amount = Math.Max(0, item.Amount),
                Sequence = item.Sequence > 0 ? item.Sequence : sequence,
                IsActive = item.IsActive,
                UpdatedAt = now
            });
            sequence++;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return await ListAsync().ConfigureAwait(false);
    }

    public async Task<decimal> ResolveAsync(string originCity, string destinationCity)
    {
        var segments = await _db.TollSegments.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Corridor)
            .ThenBy(s => s.Sequence)
            .ToListAsync()
            .ConfigureAwait(false);

        decimal best = 0;
        foreach (var group in segments.GroupBy(s => s.Corridor, StringComparer.OrdinalIgnoreCase))
        {
            var cost = SumCorridor(group.OrderBy(s => s.Sequence).ToList(), originCity, destinationCity);
            if (cost > best) best = cost;
        }

        return best;
    }

    private static decimal SumCorridor(
        IReadOnlyList<TollSegment> ordered,
        string originCity,
        string destinationCity)
    {
        if (ordered.Count == 0) return 0;

        var cities = new List<string> { ordered[0].FromCity };
        foreach (var segment in ordered)
        {
            if (!SameCity(cities[^1], segment.FromCity))
            {
                cities.Add(segment.FromCity);
            }

            cities.Add(segment.ToCity);
        }

        var originIndex = IndexOfCity(cities, originCity);
        var destIndex = IndexOfCity(cities, destinationCity);
        if (originIndex < 0 || destIndex < 0 || originIndex == destIndex) return 0;
        if (originIndex > destIndex)
        {
            (originIndex, destIndex) = (destIndex, originIndex);
        }

        return ordered
            .Where(segment =>
            {
                var from = IndexOfCity(cities, segment.FromCity);
                var to = IndexOfCity(cities, segment.ToCity);
                return from >= originIndex && to <= destIndex && from < to;
            })
            .Sum(s => s.Amount);
    }

    private static int IndexOfCity(IReadOnlyList<string> cities, string raw)
    {
        var needle = Normalize(raw);
        if (needle.Length == 0) return -1;
        for (var i = 0; i < cities.Count; i++)
        {
            var city = Normalize(cities[i]);
            if (city == needle || city.Contains(needle) || needle.Contains(city))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool SameCity(string a, string b) => Normalize(a) == Normalize(b);

    private static string Normalize(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        trimmed = trimmed.Replace('á', 'a').Replace('é', 'e').Replace('í', 'i')
            .Replace('ó', 'o').Replace('ú', 'u').Replace('ü', 'u').Replace('ñ', 'n');

        return trimmed switch
        {
            "caba" or "capital federal" or "buenos aires" or "bs as" or "retiro" => "retiro",
            "nunez" => "nunez",
            "vicente lopez" => "vicente lopez",
            "lujan" => "lujan",
            "junin" => "junin",
            _ => trimmed
        };
    }
}
