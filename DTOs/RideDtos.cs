using System.ComponentModel.DataAnnotations;
using SubiteAPI.Models;

namespace SubiteAPI.DTOs;

public class CreateRideDto
{
    [Required]
    public string OriginCity { get; set; } = string.Empty;
    [Required]
    public string OriginAddress { get; set; } = string.Empty;
    public double? OriginLat { get; set; }
    public double? OriginLng { get; set; }

    [Required]
    public string DestinationCity { get; set; } = string.Empty;
    [Required]
    public string DestinationAddress { get; set; } = string.Empty;
    public double? DestinationLat { get; set; }
    public double? DestinationLng { get; set; }

    [Required]
    public DateTime DepartureDateTime { get; set; }
    public int EstimatedDurationMinutes { get; set; }

    [Range(1, 3)]
    public int TotalSeats { get; set; }

    [Range(0, 1000000)]
    public decimal PricePerSeat { get; set; }

    public string? Notes { get; set; }
    public bool AllowsPets { get; set; }
    public bool AllowsSmoking { get; set; }
    public bool AllowsLuggage { get; set; } = true;

    /// <summary>Distancia total del viaje en km (opcional; si falta se estima).</summary>
    public double? TotalDistanceKm { get; set; }

    /// <summary>Paradas intermedias ordenadas entre origen y destino.</summary>
    public List<CreateRideStopDto> Stops { get; set; } = new();
}

public class CreateRideStopDto
{
    [Required]
    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    /// <summary>Opcional; si no viene se calcula por haversine desde el origen.</summary>
    public double? DistanceFromOriginKm { get; set; }
}

public class UpdateRideDto
{
    public string? Notes { get; set; }
    public decimal? PricePerSeat { get; set; }
    public DateTime? DepartureDateTime { get; set; }
    public bool? AllowsPets { get; set; }
    public bool? AllowsSmoking { get; set; }
    public bool? AllowsLuggage { get; set; }
}

/// <summary>Filtros para la búsqueda de viajes.</summary>
public class RideSearchDto
{
    public string? OriginCity { get; set; }
    public string? DestinationCity { get; set; }
    public DateTime? Date { get; set; }
    public int? MinSeats { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? AllowsPets { get; set; }
    public bool? AllowsLuggage { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class RideDto
{
    public Guid Id { get; set; }
    public string OriginCity { get; set; } = string.Empty;
    public string OriginAddress { get; set; } = string.Empty;
    public double? OriginLat { get; set; }
    public double? OriginLng { get; set; }
    public string DestinationCity { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public double? DestinationLat { get; set; }
    public double? DestinationLng { get; set; }
    public DateTime DepartureDateTime { get; set; }
    public DateTime? ArrivalDateTime { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public decimal PricePerSeat { get; set; }
    public double? TotalDistanceKm { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool AllowsPets { get; set; }
    public bool AllowsSmoking { get; set; }
    public bool AllowsLuggage { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RideStopDto> Stops { get; set; } = new();

    /// <summary>
    /// Precio del tramo buscado (origen→destino del pasajero).
    /// Si no hay búsqueda de tramo, coincide con PricePerSeat.
    /// </summary>
    public decimal SegmentPricePerSeat { get; set; }

    public string? MatchedBoardingCity { get; set; }
    public string? MatchedAlightingCity { get; set; }
    public int? MatchedBoardingSequence { get; set; }
    public int? MatchedAlightingSequence { get; set; }

    public RideDriverDto Driver { get; set; } = new();
    public VehicleDto? Vehicle { get; set; }
}

public class RideStopDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public double DistanceFromOriginKm { get; set; }
}

public class RideDriverDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
    public bool IsVerified { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}
