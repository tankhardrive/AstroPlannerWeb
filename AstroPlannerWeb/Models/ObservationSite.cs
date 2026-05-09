namespace AstroPlannerWeb.Models;

public class ObservationSite
{
    public string Name { get; set; } = "My Location";
    public double LatitudeDegrees { get; set; } = 0;
    public double LongitudeDegrees { get; set; } = 0;
    public double ElevationMeters { get; set; } = 0;
    public string TimeZoneId { get; set; } = "UTC";

    public TimeZoneInfo GetTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch { return TimeZoneInfo.Utc; }
    }
}
