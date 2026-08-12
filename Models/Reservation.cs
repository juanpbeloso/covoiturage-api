namespace SubiteAPI.Models;

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed,
    Refunded
}

public class Reservation
{
    public Guid Id { get; set; }
    public Guid RideId { get; set; }
    public Guid PassengerId { get; set; }
    public int SeatsReserved { get; set; } = 1;
    public decimal TotalPrice { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    /// <summary>Ciudad donde sube el pasajero (puede ser origen o una parada).</summary>
    public string? BoardingCity { get; set; }

    /// <summary>Ciudad donde baja el pasajero (puede ser destino o una parada).</summary>
    public string? AlightingCity { get; set; }

    public int? BoardingSequence { get; set; }
    public int? AlightingSequence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Navegación
    public Ride Ride { get; set; } = null!;
    public User Passenger { get; set; } = null!;
    public Payment? Payment { get; set; }
}
