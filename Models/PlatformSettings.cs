namespace SubiteAPI.Models;

/// <summary>Configuración global editable desde el panel admin (singleton, Id = 1).</summary>
public class PlatformSettings
{
    public int Id { get; set; } = 1;
    public decimal PlatformCommissionRate { get; set; } = 0.125m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
