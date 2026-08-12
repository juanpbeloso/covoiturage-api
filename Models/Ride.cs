namespace SubiteAPI.Models;

public enum RideStatus
{
    /// <summary>Publicado y disponible para reservas (tiene asientos).</summary>
    Active,

    /// <summary>Sin asientos libres; sigue programado.</summary>
    Full,

    /// <summary>El viaje ya comenzó (salida alcanzada). Futuro: transición automática.</summary>
    InProgress,

    /// <summary>Viaje finalizado. Futuro: transición automática al llegar.</summary>
    Completed,

    /// <summary>Cancelado por el conductor (o sistema).</summary>
    Cancelled
}

public class Ride
{
    public Guid Id { get; set; }
    public Guid DriverId { get; set; }
    public Guid VehicleId { get; set; }

    // Origen
    public string OriginCity { get; set; } = string.Empty;
    public string OriginAddress { get; set; } = string.Empty;
    public double? OriginLat { get; set; }
    public double? OriginLng { get; set; }

    // Destino
    public string DestinationCity { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public double? DestinationLat { get; set; }
    public double? DestinationLng { get; set; }

    // Horarios
    public DateTime DepartureDateTime { get; set; }
    public DateTime? ArrivalDateTime { get; set; }
    public int EstimatedDurationMinutes { get; set; }

    // Asientos y precio
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public decimal PricePerSeat { get; set; }

    /// <summary>Distancia total origen→destino (km). Usada para precio proporcional por tramo.</summary>
    public double? TotalDistanceKm { get; set; }

    // Estado
    public RideStatus Status { get; set; } = RideStatus.Active;
    public string? Notes { get; set; }
    public bool AllowsPets { get; set; } = false;
    public bool AllowsSmoking { get; set; } = false;
    public bool AllowsLuggage { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public User Driver { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public ICollection<RideStop> Stops { get; set; } = new List<RideStop>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
