namespace SubiteAPI.Helpers;

/// <summary>
/// Normaliza fechas para PostgreSQL (timestamptz exige Kind=Utc con Npgsql 6+).
/// </summary>
public static class DateTimeHelper
{
    private static readonly TimeZoneInfo ArgentinaTz = ResolveArgentinaTimeZone();

    /// <summary>
    /// Rango UTC [inicio, fin) del día calendario en Argentina (ej. filtro ?date=2026-07-09).
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) GetArgentinaDayUtcRange(DateTime calendarDate)
    {
        var startLocal = new DateTime(
            calendarDate.Year,
            calendarDate.Month,
            calendarDate.Day,
            0,
            0,
            0,
            DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);

        return (ArgentinaLocalToUtc(startLocal), ArgentinaLocalToUtc(endLocal));
    }

    /// <summary>
    /// Convierte un instante entrante a UTC. Si viene sin zona, se asume hora Argentina.
    /// </summary>
    public static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => ArgentinaLocalToUtc(value),
        };
    }

    private static DateTime ArgentinaLocalToUtc(DateTime localUnspecified)
    {
        var offset = ArgentinaTz.GetUtcOffset(localUnspecified);
        var utc = new DateTimeOffset(localUnspecified, offset).UtcDateTime;
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    private static TimeZoneInfo ResolveArgentinaTimeZone()
    {
        foreach (var id in new[] { "America/Argentina/Buenos_Aires", "Argentina Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // probar siguiente id
            }
        }

        // UTC-3 fijo (Argentina sin DST desde 2009)
        return TimeZoneInfo.CreateCustomTimeZone(
            "Argentina",
            TimeSpan.FromHours(-3),
            "Argentina",
            "Argentina");
    }
}
