using System.ComponentModel.DataAnnotations;

namespace SubiteAPI.DTOs;

public class CreateReservationDto
{
    [Required]
    public Guid RideId { get; set; }

    [Range(1, 8)]
    public int SeatsReserved { get; set; } = 1;

    /// <summary>Ciudad de subida (origen del tramo). Si no viene, se usa el origen del viaje.</summary>
    public string? BoardingCity { get; set; }

    /// <summary>Ciudad de bajada (destino del tramo). Si no viene, se usa el destino del viaje.</summary>
    public string? AlightingCity { get; set; }
}

public class CancelReservationDto
{
    public string? Reason { get; set; }
}

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid RideId { get; set; }
    public Guid PassengerId { get; set; }
    public int SeatsReserved { get; set; }
    public decimal TotalPrice { get; set; }
    public string? BoardingCity { get; set; }
    public string? AlightingCity { get; set; }
    public int? BoardingSequence { get; set; }
    public int? AlightingSequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public ReservationPassengerDto Passenger { get; set; } = new();
    public RideDto? Ride { get; set; }
}

public class ReservationPassengerDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public decimal Rating { get; set; }
}
