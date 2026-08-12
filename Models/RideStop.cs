namespace SubiteAPI.Models;

/// <summary>Parada intermedia entre origen y destino de un viaje.</summary>
public class RideStop
{
    public Guid Id { get; set; }
    public Guid RideId { get; set; }

    /// <summary>Orden en la ruta (1 = primera parada después del origen).</summary>
    public int Sequence { get; set; }

    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    /// <summary>Km acumulados desde el origen del viaje (aprox.).</summary>
    public double DistanceFromOriginKm { get; set; }

    public Ride Ride { get; set; } = null!;
}
