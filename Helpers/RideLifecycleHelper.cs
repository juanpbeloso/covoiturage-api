using SubiteAPI.Models;

namespace SubiteAPI.Helpers;

public static class RideLifecycleHelper
{
    public static DateTime ResolveArrivalUtc(Ride ride)
    {
        if (ride.ArrivalDateTime.HasValue)
        {
            return DateTime.SpecifyKind(ride.ArrivalDateTime.Value, DateTimeKind.Utc);
        }

        var departure = DateTime.SpecifyKind(ride.DepartureDateTime, DateTimeKind.Utc);
        return ride.EstimatedDurationMinutes > 0
            ? departure.AddMinutes(ride.EstimatedDurationMinutes)
            : departure;
    }

    /// <summary>
    /// Active/Full → InProgress al salir; → Completed al llegar.
    /// Cancelled y Completed no cambian.
    /// </summary>
    public static RideStatus EffectiveStatus(Ride ride, DateTime utcNow)
    {
        if (ride.Status is RideStatus.Cancelled or RideStatus.Completed)
        {
            return ride.Status;
        }

        var arrival = ResolveArrivalUtc(ride);
        if (utcNow >= arrival)
        {
            return RideStatus.Completed;
        }

        if (utcNow >= ride.DepartureDateTime)
        {
            return RideStatus.InProgress;
        }

        return ride.Status;
    }
}
