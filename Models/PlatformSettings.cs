namespace SubiteAPI.Models;

/// <summary>Configuración global editable desde el panel admin (singleton, Id = 1).</summary>
public class PlatformSettings
{
    public int Id { get; set; } = 1;
    public decimal PlatformCommissionRate { get; set; } = 0.125m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Si false, la app no llama a Google Directions (línea recta).</summary>
    public bool MapsDirectionsEnabled { get; set; } = true;

    /// <summary>Tope de requests exitosos por mes calendario (UTC). 0 = sin tope extra.</summary>
    public int MapsMonthlyRequestCap { get; set; } = 10000;

    /// <summary>Precio estimado USD cada 1000 requests (SKU Directions).</summary>
    public decimal MapsPricePerThousandUsd { get; set; } = 5.00m;
}
