using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SubiteAPI.DTOs;
using SubiteAPI.Helpers;
using SubiteAPI.Options;

namespace SubiteAPI.Services;

public class GeorefService : IGeorefService
{
    private readonly HttpClient _http;
    private readonly GeorefOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Puntos frecuentes del corredor Junín ↔ GBA/CABA (Georef a veces no los devuelve como localidad).</summary>
    private static readonly (string Id, string Name, string Province, double Lat, double Lng)[] CorridorLandmarks =
    [
        ("corridor-junin", "Junín", "Buenos Aires", -34.5838, -60.9433),
        ("corridor-chacabuco", "Chacabuco", "Buenos Aires", -34.6417, -60.4736),
        ("corridor-alem", "Alem", "Buenos Aires", -32.0597, -62.2953),
        ("corridor-lujan", "Luján", "Buenos Aires", -34.5703, -59.1055),
        ("corridor-retiro", "Retiro", "Ciudad Autónoma de Buenos Aires", -34.5950, -58.3730),
        ("corridor-almagro", "Almagro", "Ciudad Autónoma de Buenos Aires", -34.6114, -58.4202),
        ("corridor-nunez", "Nuñez", "Ciudad Autónoma de Buenos Aires", -34.5436, -58.4634),
        ("corridor-nunez-alt", "Núñez", "Ciudad Autónoma de Buenos Aires", -34.5436, -58.4634),
        ("corridor-vicente-lopez", "Vicente López", "Buenos Aires", -34.5260, -58.4730),
        ("corridor-caba", "Buenos Aires", "Ciudad Autónoma de Buenos Aires", -34.6037, -58.3816),
    ];

    public GeorefService(HttpClient http, IOptions<GeorefOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<LocalityDto>> SearchLocalitiesAsync(LocalitySearchDto search)
    {
        var q = search.Q?.Trim() ?? string.Empty;
        var max = Math.Clamp(search.Max, 1, 50);
        var useCorridor = search.UseCorridor && _options.Corridor.Enabled;

        if (q.Length < 2)
        {
            if (useCorridor)
            {
                return CorridorLandmarks
                    .Select(l => new LocalityDto
                    {
                        Id = l.Id,
                        Name = l.Name,
                        Province = l.Province,
                        Lat = l.Lat,
                        Lng = l.Lng
                    })
                    .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(max)
                    .ToList();
            }

            return Array.Empty<LocalityDto>();
        }

        // useCorridor ya definido arriba

        // Con corredor activo buscamos a nivel nacional para incluir CABA y GBA (Retiro, Nuñez, etc.).
        var provincia = search.Provincia?.Trim();
        if (useCorridor)
        {
            provincia = null;
        }
        else if (string.IsNullOrEmpty(provincia))
        {
            provincia = _options.Corridor.DefaultProvincia;
        }

        var georefMax = useCorridor ? Math.Min(max * 6, 50) : max;

        List<LocalityDto> items;
        try
        {
            var url =
                $"{_options.BaseUrl.TrimEnd('/')}/localidades" +
                $"?nombre={Uri.EscapeDataString(q)}" +
                (string.IsNullOrEmpty(provincia) ? "" : $"&provincia={Uri.EscapeDataString(provincia)}") +
                $"&max={georefMax}&campos=basico,centroide&orden=nombre";

            using var response = await _http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<GeorefLocalidadesResponse>(stream, JsonOptions)
                .ConfigureAwait(false);

            items = payload?.Localidades?
                .Where(l => l.Centroide != null)
                .Select(MapLocality)
                .ToList() ?? new List<LocalityDto>();
        }
        catch
        {
            items = new List<LocalityDto>();
        }

        if (useCorridor)
        {
            var corridor = _options.Corridor;
            items = items
                .Where(l => DistanceToCorridorKm(l.Lat, l.Lng, corridor) <= corridor.RadiusKm)
                .ToList();

            var landmarkMatches = CorridorLandmarks
                .Where(l => TextNormalize.ContainsFolded(l.Name, q) ||
                            TextNormalize.ContainsFolded(q, l.Name))
                .Select(l => new LocalityDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    Province = l.Province,
                    Lat = l.Lat,
                    Lng = l.Lng
                });

            items = items
                .Concat(landmarkMatches)
                .GroupBy(l => $"{TextNormalize.Fold(l.Name)}|{TextNormalize.Fold(l.Province)}")
                .Select(g => g.First())
                .ToList();
        }

        return items
            .OrderBy(l => TextNormalize.StartsWithFolded(l.Name, q) ? 0 : 1)
            .ThenBy(l => TextNormalize.ContainsFolded(l.Name, q) ? 0 : 1)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    public async Task<IReadOnlyList<NormalizedAddressDto>> SearchAddressesAsync(AddressSearchDto search)
    {
        var direccion = search.Direccion?.Trim() ?? string.Empty;
        var localidad = search.Localidad?.Trim() ?? string.Empty;

        if (direccion.Length < 3 || localidad.Length < 2)
        {
            return Array.Empty<NormalizedAddressDto>();
        }

        var max = Math.Clamp(search.Max, 1, 20);
        var useCorridor = search.UseCorridor && _options.Corridor.Enabled;
        var provincia = search.Provincia?.Trim();

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/direcciones" +
            $"?direccion={Uri.EscapeDataString(direccion)}" +
            $"&localidad={Uri.EscapeDataString(localidad)}" +
            (string.IsNullOrEmpty(provincia) ? "" : $"&provincia={Uri.EscapeDataString(provincia)}") +
            $"&max={max}&campos=basico,centroide";

        using var response = await _http.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<GeorefDireccionesResponse>(stream, JsonOptions)
            .ConfigureAwait(false);

        var items = payload?.Direcciones?
            .Where(d => d.Centroide != null)
            .Select(MapAddress)
            .ToList() ?? new List<NormalizedAddressDto>();

        if (useCorridor)
        {
            var corridor = _options.Corridor;
            items = items
                .Where(a => DistanceToCorridorKm(a.Lat, a.Lng, corridor) <= corridor.RadiusKm)
                .ToList();
        }

        return items;
    }

    private static LocalityDto MapLocality(GeorefLocalidad l) => new()
    {
        Id = l.Id ?? string.Empty,
        Name = l.Nombre ?? string.Empty,
        Province = l.Provincia?.Nombre ?? string.Empty,
        Lat = l.Centroide!.Lat,
        Lng = l.Centroide.Lon
    };

    private static NormalizedAddressDto MapAddress(GeorefDireccion d)
    {
        var street = d.Nombre ?? string.Empty;
        var locality = d.LocalidadCensal?.Nombre ?? d.Departamento?.Nombre ?? string.Empty;
        var province = d.Provincia?.Nombre ?? string.Empty;
        var label = string.IsNullOrWhiteSpace(locality)
            ? street
            : $"{street}, {locality}";

        return new NormalizedAddressDto
        {
            Label = label,
            Street = street,
            Locality = locality,
            Province = province,
            Lat = d.Centroide!.Lat,
            Lng = d.Centroide.Lon
        };
    }

    private static double DistanceToCorridorKm(double lat, double lng, CorridorOptions corridor)
    {
        return DistanceToSegmentKm(
            lat, lng,
            corridor.FromLat, corridor.FromLng,
            corridor.ToLat, corridor.ToLng);
    }

    /// <summary>Distancia mínima (km) de un punto al segmento entre A y B.</summary>
    private static double DistanceToSegmentKm(
        double lat, double lng,
        double latA, double lngA,
        double latB, double lngB)
    {
        var latMid = (latA + latB) / 2.0;
        var x = lng * Math.Cos(latMid * Math.PI / 180.0);
        var y = lat;
        var xA = lngA * Math.Cos(latMid * Math.PI / 180.0);
        var yA = latA;
        var xB = lngB * Math.Cos(latMid * Math.PI / 180.0);
        var yB = latB;

        var dx = xB - xA;
        var dy = yB - yA;
        if (dx * dx + dy * dy < 1e-12)
        {
            return HaversineKm(lat, lng, latA, lngA);
        }

        var t = ((x - xA) * dx + (y - yA) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        var projX = xA + t * dx;
        var projY = yA + t * dy;
        var projLng = projX / Math.Cos(latMid * Math.PI / 180.0);

        return HaversineKm(lat, lng, projY, projLng);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private sealed class GeorefLocalidadesResponse
    {
        public List<GeorefLocalidad>? Localidades { get; set; }
    }

    private sealed class GeorefDireccionesResponse
    {
        public List<GeorefDireccion>? Direcciones { get; set; }
    }

    private sealed class GeorefLocalidad
    {
        public string? Id { get; set; }
        public string? Nombre { get; set; }
        public GeorefProvincia? Provincia { get; set; }
        public GeorefCentroide? Centroide { get; set; }
    }

    private sealed class GeorefDireccion
    {
        public string? Nombre { get; set; }
        public GeorefProvincia? Provincia { get; set; }
        public GeorefLocalidadRef? LocalidadCensal { get; set; }
        public GeorefLocalidadRef? Departamento { get; set; }
        public GeorefCentroide? Centroide { get; set; }
    }

    private sealed class GeorefLocalidadRef
    {
        public string? Nombre { get; set; }
    }

    private sealed class GeorefProvincia
    {
        public string? Nombre { get; set; }
    }

    private sealed class GeorefCentroide
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }
    }
}
