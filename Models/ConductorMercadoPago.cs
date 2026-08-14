using System.ComponentModel.DataAnnotations;

namespace SubiteAPI.Models;

/// <summary>Cuenta MercadoPago OAuth de un conductor (vendedor en Split 1:1).</summary>
public class ConductorMercadoPago
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConductorId { get; set; }
    public User Conductor { get; set; } = null!;

    /// <summary>User ID de MercadoPago del vendedor (collector).</summary>
    [MaxLength(64)]
    public string MpUserId { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string AccessToken { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime TokenExpiraEn { get; set; }

    public DateTime ConectadoEn { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
