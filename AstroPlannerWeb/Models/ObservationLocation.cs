namespace AstroPlannerWeb.Models;

/// <summary>
/// A named observing location bundling site coordinates, timezone, and horizon profile.
/// </summary>
public class ObservationLocation
{
    public string Name { get; set; } = "My Location";
    public double LatitudeDegrees { get; set; } = 0;
    public double LongitudeDegrees { get; set; } = 0;
    public double ElevationMeters { get; set; } = 0;

    /// <summary>IANA timezone ID, e.g. "America/New_York".</summary>
    public string TimeZoneId { get; set; } = "UTC";

    public HorizonProfile Horizon { get; set; } = HorizonProfile.Flat();

    /// <summary>
    /// Bortle class (1–9). Null = not fetched.
    /// Currently unused in the web app (no HTTP lookup for now).
    /// </summary>
    public int? BortleClass { get; set; }

    public ObservationSite ToSite() => new()
    {
        Name = Name,
        LatitudeDegrees = LatitudeDegrees,
        LongitudeDegrees = LongitudeDegrees,
        ElevationMeters = ElevationMeters,
        TimeZoneId = TimeZoneId,
    };

    public TimeZoneInfo GetTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch { return TimeZoneInfo.Utc; }
    }
}
