using System.ComponentModel.DataAnnotations;

namespace SubiteAPI.DTOs;

public class MapsLatLngDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class MapsDirectionsRequestDto
{
    [Required]
    public MapsLatLngDto Origin { get; set; } = new();

    [Required]
    public MapsLatLngDto Destination { get; set; } = new();

    public List<MapsLatLngDto>? Waypoints { get; set; }
}

public class MapsDirectionsResultDto
{
    public IReadOnlyList<MapsLatLngDto> Coordinates { get; set; } = Array.Empty<MapsLatLngDto>();
    public int DistanceKm { get; set; }
    public int DurationMinutes { get; set; }
}

public class ExternalApiBudgetDto
{
    public bool MapsDirectionsEnabled { get; set; }
    public int MapsMonthlyRequestCap { get; set; }
    public decimal MapsPricePerThousandUsd { get; set; }
    public bool KeyConfigured { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public int RequestsThisMonth { get; set; }
    public int SuccessfulThisMonth { get; set; }
    public int FailedThisMonth { get; set; }
    public int CapRemaining { get; set; }
    public decimal EstimatedCostUsdThisMonth { get; set; }
    public int GoogleFreeCap { get; set; } = 10000;
}

public class UpdateExternalApiBudgetDto
{
    public bool MapsDirectionsEnabled { get; set; }

    [Range(0, 10_000_000)]
    public int MapsMonthlyRequestCap { get; set; }

    [Range(0, 100)]
    public decimal MapsPricePerThousandUsd { get; set; }
}
