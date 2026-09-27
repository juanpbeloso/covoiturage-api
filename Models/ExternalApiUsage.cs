namespace SubiteAPI.Models;

/// <summary>Registro de llamadas a APIs de terceros (Directions, etc.).</summary>
public class ExternalApiUsage
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "google_maps";
    public string Sku { get; set; } = "directions";
    public bool Success { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
