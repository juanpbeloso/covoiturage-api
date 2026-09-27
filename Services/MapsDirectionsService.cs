using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Models;
using SubiteAPI.Options;

namespace SubiteAPI.Services;

public interface IMapsDirectionsService
{
    Task<ExternalApiBudgetDto> GetBudgetAsync();
    Task<ExternalApiBudgetDto> UpdateBudgetAsync(UpdateExternalApiBudgetDto dto);
    Task<MapsDirectionsResultDto> GetRouteAsync(MapsDirectionsRequestDto dto);
}

public class MapsDirectionsService : IMapsDirectionsService
{
    public const string Provider = "google_maps";
    public const string Sku = "directions";
    public const int GoogleFreeCap = 10_000;

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly GoogleMapsOptions _maps;
    private readonly IPlatformSettingsService _settings;

    public MapsDirectionsService(
        AppDbContext db,
        IHttpClientFactory http,
        IOptions<GoogleMapsOptions> maps,
        IPlatformSettingsService settings)
    {
        _db = db;
        _http = http;
        _maps = maps.Value;
        _settings = settings;
    }

    public async Task<ExternalApiBudgetDto> GetBudgetAsync()
    {
        var entity = await GetSettingsEntityAsync().ConfigureAwait(false);
        return await MapBudgetAsync(entity).ConfigureAwait(false);
    }

    public async Task<ExternalApiBudgetDto> UpdateBudgetAsync(UpdateExternalApiBudgetDto dto)
    {
        var entity = await GetSettingsEntityAsync().ConfigureAwait(false);
        entity.MapsDirectionsEnabled = dto.MapsDirectionsEnabled;
        entity.MapsMonthlyRequestCap = dto.MapsMonthlyRequestCap;
        entity.MapsPricePerThousandUsd = dto.MapsPricePerThousandUsd;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync().ConfigureAwait(false);
        return await MapBudgetAsync(entity).ConfigureAwait(false);
    }

    public async Task<MapsDirectionsResultDto> GetRouteAsync(MapsDirectionsRequestDto dto)
    {
        var settings = await GetSettingsEntityAsync().ConfigureAwait(false);
        if (!settings.MapsDirectionsEnabled)
        {
            throw new BusinessException(
                "MAPS_DISABLED",
                "Las rutas de Google están desactivadas desde el panel admin.",
                403);
        }

        if (string.IsNullOrWhiteSpace(_maps.ApiKey))
        {
            throw new BusinessException(
                "MAPS_NO_KEY",
                "Falta GoogleMaps:ApiKey en el servidor.",
                503);
        }

        var (from, to) = MonthRangeUtc();
        var successful = await _db.ExternalApiUsages.AsNoTracking()
            .CountAsync(u =>
                u.Provider == Provider &&
                u.Sku == Sku &&
                u.Success &&
                u.CreatedAt >= from &&
                u.CreatedAt < to)
            .ConfigureAwait(false);

        if (settings.MapsMonthlyRequestCap > 0 && successful >= settings.MapsMonthlyRequestCap)
        {
            throw new BusinessException(
                "MAPS_CAP",
                "Se alcanzó el tope mensual de Google Directions configurado en el admin.",
                429);
        }

        var waypoints = dto.Waypoints ?? new List<MapsLatLngDto>();
        var origin = FormatLatLng(dto.Origin);
        var dest = FormatLatLng(dto.Destination);
        var waypointStr = waypoints.Count == 0
            ? ""
            : "&waypoints=" + Uri.EscapeDataString(
                string.Join("|", waypoints.Select(FormatLatLng)));

        var url =
            "https://maps.googleapis.com/maps/api/directions/json" +
            $"?origin={Uri.EscapeDataString(origin)}" +
            $"&destination={Uri.EscapeDataString(dest)}" +
            waypointStr +
            $"&mode=driving&language=es&key={_maps.ApiKey}";

        var client = _http.CreateClient("GoogleMaps");
        using var response = await client.GetAsync(url).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        GoogleDirectionsResponse? parsed = null;
        try
        {
            parsed = JsonSerializer.Deserialize<GoogleDirectionsResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            // se registra como fallo
        }

        var ok = response.IsSuccessStatusCode &&
                 string.Equals(parsed?.Status, "OK", StringComparison.OrdinalIgnoreCase) &&
                 parsed?.Routes is { Count: > 0 };

        var billableThisMonth = successful + (ok ? 1 : 0);
        var cost = ok && billableThisMonth > GoogleFreeCap
            ? Math.Round(settings.MapsPricePerThousandUsd / 1000m, 6)
            : 0m;

        _db.ExternalApiUsages.Add(new ExternalApiUsage
        {
            Id = Guid.NewGuid(),
            Provider = Provider,
            Sku = Sku,
            Success = ok,
            EstimatedCostUsd = cost,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync().ConfigureAwait(false);

        if (!ok)
        {
            throw new BusinessException(
                "MAPS_NO_ROUTE",
                parsed?.ErrorMessage ?? "Google Directions no devolvió una ruta.",
                502);
        }

        var route = parsed?.Routes?[0]
            ?? throw new BusinessException("MAPS_NO_ROUTE", "Google Directions no devolvió una ruta.", 502);
        var encoded = route.OverviewPolyline?.Points;
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new BusinessException("MAPS_NO_ROUTE", "La ruta no tiene polyline.", 502);
        }

        var coords = DecodePolyline(encoded);
        var distanceM = route.Legs?.Sum(l => l.Distance?.Value ?? 0) ?? 0;
        var durationS = route.Legs?.Sum(l => l.Duration?.Value ?? 0) ?? 0;

        return new MapsDirectionsResultDto
        {
            Coordinates = coords,
            DistanceKm = (int)Math.Round(distanceM / 1000d),
            DurationMinutes = (int)Math.Round(durationS / 60d)
        };
    }

    private async Task<PlatformSettings> GetSettingsEntityAsync()
    {
        await _settings.GetAsync().ConfigureAwait(false);
        return await _db.PlatformSettings.FirstAsync(s => s.Id == 1).ConfigureAwait(false);
    }

    private async Task<ExternalApiBudgetDto> MapBudgetAsync(PlatformSettings entity)
    {
        var (from, to) = MonthRangeUtc();
        var monthUsages = await _db.ExternalApiUsages.AsNoTracking()
            .Where(u =>
                u.Provider == Provider &&
                u.Sku == Sku &&
                u.CreatedAt >= from &&
                u.CreatedAt < to)
            .ToListAsync()
            .ConfigureAwait(false);

        var successful = monthUsages.Count(u => u.Success);
        var failed = monthUsages.Count - successful;
        var cap = entity.MapsMonthlyRequestCap;
        var remaining = cap <= 0 ? int.MaxValue : Math.Max(0, cap - successful);
        var billableOverFree = Math.Max(0, successful - GoogleFreeCap);
        var estimated = Math.Round(billableOverFree * entity.MapsPricePerThousandUsd / 1000m, 2);

        return new ExternalApiBudgetDto
        {
            MapsDirectionsEnabled = entity.MapsDirectionsEnabled,
            MapsMonthlyRequestCap = entity.MapsMonthlyRequestCap,
            MapsPricePerThousandUsd = entity.MapsPricePerThousandUsd,
            KeyConfigured = !string.IsNullOrWhiteSpace(_maps.ApiKey),
            MonthLabel = from.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            RequestsThisMonth = monthUsages.Count,
            SuccessfulThisMonth = successful,
            FailedThisMonth = failed,
            CapRemaining = remaining == int.MaxValue ? -1 : remaining,
            EstimatedCostUsdThisMonth = estimated,
            GoogleFreeCap = GoogleFreeCap
        };
    }

    private static (DateTime From, DateTime To) MonthRangeUtc()
    {
        var now = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(1));
    }

    private static string FormatLatLng(MapsLatLngDto p) =>
        string.Create(CultureInfo.InvariantCulture, $"{p.Latitude},{p.Longitude}");

    private static List<MapsLatLngDto> DecodePolyline(string encoded)
    {
        var coordinates = new List<MapsLatLngDto>();
        var index = 0;
        var lat = 0;
        var lng = 0;

        while (index < encoded.Length)
        {
            var result = 0;
            var shift = 0;
            int b;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            lat += (result & 1) != 0 ? ~(result >> 1) : result >> 1;

            result = 0;
            shift = 0;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            lng += (result & 1) != 0 ? ~(result >> 1) : result >> 1;

            coordinates.Add(new MapsLatLngDto
            {
                Latitude = lat / 1e5,
                Longitude = lng / 1e5
            });
        }

        return coordinates;
    }

    private sealed class GoogleDirectionsResponse
    {
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
        public List<GoogleRoute>? Routes { get; set; }
    }

    private sealed class GoogleRoute
    {
        public GooglePolyline? OverviewPolyline { get; set; }
        public List<GoogleLeg>? Legs { get; set; }
    }

    private sealed class GooglePolyline
    {
        public string? Points { get; set; }
    }

    private sealed class GoogleLeg
    {
        public GoogleMetric? Distance { get; set; }
        public GoogleMetric? Duration { get; set; }
    }

    private sealed class GoogleMetric
    {
        public int Value { get; set; }
    }
}
