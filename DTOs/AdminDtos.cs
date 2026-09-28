using System.ComponentModel.DataAnnotations;

namespace SubiteAPI.DTOs;

public class AdminLoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AdminLoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class AdminMeDto
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class PlatformSettingsDto
{
    public decimal PlatformCommissionRate { get; set; }
    public decimal PlatformCommissionPercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateCommissionDto
{
    [Range(0, 100)]
    public decimal PlatformCommissionPercent { get; set; }
}

public class AdminPagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class AdminViajeDto
{
    public Guid Id { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public DateTime Salida { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string ConductorNombre { get; set; } = string.Empty;
    public string ConductorEmail { get; set; } = string.Empty;
    public int AsientosTotales { get; set; }
    public int AsientosDisponibles { get; set; }
    public decimal PrecioPorAsiento { get; set; }
    public int ReservasActivas { get; set; }
}

public class AdminCancelarViajeDto
{
    public string? Reason { get; set; }
}

public class AdminReservaDto
{
    public Guid Id { get; set; }
    public Guid RideId { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public string PasajeroNombre { get; set; } = string.Empty;
    public string PasajeroEmail { get; set; } = string.Empty;
    public int Asientos { get; set; }
    public decimal Monto { get; set; }
    public decimal BaseViaje { get; set; }
    public decimal Ganancia { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string EstadoPago { get; set; } = string.Empty;
    public DateTime Creada { get; set; }
}

public class AdminGananciaPagoDto
{
    public Guid Id { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal MontoPasajero { get; set; }
    public decimal BaseViaje { get; set; }
    public decimal Ganancia { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class AdminGananciaSeriePointDto
{
    public string Fecha { get; set; } = string.Empty;
    public decimal Ganancia { get; set; }
    public decimal Gmv { get; set; }
    public int Pagos { get; set; }
}

public class AdminGananciasDto
{
    public int Days { get; set; }
    public decimal TotalGanancia { get; set; }
    public decimal TotalGmv { get; set; }
    public int PagosOk { get; set; }
    public decimal GananciaPromedio { get; set; }
    public IReadOnlyList<AdminGananciaSeriePointDto> Series { get; set; } = Array.Empty<AdminGananciaSeriePointDto>();
    public IReadOnlyList<AdminGananciaPagoDto> Pagos { get; set; } = Array.Empty<AdminGananciaPagoDto>();
}

public class AdminLogDto
{
    public Guid Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}

public class AdminUsuarioDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime Creado { get; set; }
}

public class AdminDashboardKpisDto
{
    public int Usuarios { get; set; }
    public int ViajesPublicados { get; set; }
    public int Reservas { get; set; }
    public int PagosOk { get; set; }
    public int PagosFail { get; set; }
    public decimal GmvAproximado { get; set; }
}

public class AdminDashboardSeriePointDto
{
    public string Fecha { get; set; } = string.Empty;
    public int Reservas { get; set; }
}
