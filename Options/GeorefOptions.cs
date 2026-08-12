namespace SubiteAPI.Options;

public class GeorefOptions
{
    public const string SectionName = "Georef";

    public string BaseUrl { get; set; } = "https://apis.datos.gob.ar/georef/api";

    public CorridorOptions Corridor { get; set; } = new();
}

public class CorridorOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Distancia máxima (km) desde el corredor para incluir una localidad.</summary>
    public double RadiusKm { get; set; } = 60;

    public string DefaultProvincia { get; set; } = "Buenos Aires";

    public double FromLat { get; set; } = -34.5838;
    public double FromLng { get; set; } = -60.9433;
    public string FromName { get; set; } = "Junín";

    public double ToLat { get; set; } = -34.595;
    public double ToLng { get; set; } = -58.373;
    public string ToName { get; set; } = "Retiro";
}
