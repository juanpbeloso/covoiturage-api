using SubiteAPI.Models;

namespace SubiteAPI.Helpers;

public sealed record RoutePoint(
    int Sequence,
    string City,
    double? Lat,
    double? Lng,
    double DistanceFromOriginKm);

public static class RideRouteHelper
{
    private const double EarthRadiusKm = 6371;

    public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// True si el punto está razonablemente sobre el corredor origen→destino
    /// (no exige Directions API; validación suave para publicación).
    /// </summary>
    public static bool IsRoughlyOnRoute(
        double originLat, double originLng,
        double destLat, double destLng,
        double stopLat, double stopLng,
        double maxDetourRatio = 1.35)
    {
        var direct = HaversineKm(originLat, originLng, destLat, destLng);
        if (direct < 1) return true;

        var viaStop =
            HaversineKm(originLat, originLng, stopLat, stopLng) +
            HaversineKm(stopLat, stopLng, destLat, destLng);

        return viaStop <= direct * maxDetourRatio;
    }

    public static IReadOnlyList<RoutePoint> BuildRoutePoints(Ride ride)
    {
        var points = new List<RoutePoint>
        {
            new(0, ride.OriginCity, ride.OriginLat, ride.OriginLng, 0)
        };

        foreach (var stop in (ride.Stops ?? Enumerable.Empty<RideStop>()).OrderBy(s => s.Sequence))
        {
            points.Add(new RoutePoint(
                stop.Sequence,
                stop.City,
                stop.Lat,
                stop.Lng,
                stop.DistanceFromOriginKm));
        }

        var destSequence = points.Count > 0 ? points[^1].Sequence + 1 : 1;
        var destKm = ride.TotalDistanceKm ?? EstimateTotalKm(ride);
        points.Add(new(
            destSequence,
            ride.DestinationCity,
            ride.DestinationLat,
            ride.DestinationLng,
            destKm));

        return points;
    }

    public static double EstimateTotalKm(Ride ride)
    {
        if (ride.TotalDistanceKm is > 0) return ride.TotalDistanceKm.Value;

        if (ride.OriginLat is double oLat && ride.OriginLng is double oLng &&
            ride.DestinationLat is double dLat && ride.DestinationLng is double dLng)
        {
            return HaversineKm(oLat, oLng, dLat, dLng);
        }

        return 0;
    }

    public static double EstimateDistanceFromOrigin(
        double? originLat, double? originLng,
        double? pointLat, double? pointLng,
        double previousKm)
    {
        if (originLat is double oLat && originLng is double oLng &&
            pointLat is double pLat && pointLng is double pLng)
        {
            return HaversineKm(oLat, oLng, pLat, pLng);
        }

        return previousKm;
    }

    public static int? FindPointIndex(IReadOnlyList<RoutePoint> points, string? city)
    {
        if (string.IsNullOrWhiteSpace(city) || points.Count == 0) return null;
        var needle = city.Trim();

        for (var i = 0; i < points.Count; i++)
        {
            if (TextNormalize.ContainsFolded(points[i].City, needle) ||
                TextNormalize.ContainsFolded(needle, points[i].City))
            {
                return i;
            }
        }

        return null;
    }

    public static decimal ComputeSegmentPricePerSeat(
        decimal fullPricePerSeat,
        IReadOnlyList<RoutePoint> points,
        int boardIndex,
        int alightIndex)
    {
        if (boardIndex < 0 || alightIndex <= boardIndex || alightIndex >= points.Count)
        {
            return fullPricePerSeat;
        }

        var totalKm = points[^1].DistanceFromOriginKm;
        if (totalKm <= 0.1)
        {
            // Sin km: proporción por cantidad de tramos de secuencia.
            var fullSegments = points.Count - 1;
            var usedSegments = alightIndex - boardIndex;
            if (fullSegments <= 0) return fullPricePerSeat;
            return Math.Round(fullPricePerSeat * usedSegments / fullSegments, 0);
        }

        var segmentKm = points[alightIndex].DistanceFromOriginKm - points[boardIndex].DistanceFromOriginKm;
        if (segmentKm < 0) segmentKm = 0;
        var ratio = Math.Clamp(segmentKm / totalKm, 0.05, 1.0);
        return Math.Round(fullPricePerSeat * (decimal)ratio, 0);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
