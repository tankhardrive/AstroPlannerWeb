namespace AstroPlannerWeb.Models;

public class WeatherHour
{
    public DateTime UtcTime { get; set; }

    public double? CloudCoverPercent { get; set; }
    public double? WindSpeedMph { get; set; }
    public double? WindChillF { get; set; }
    public double? HumidityPercent { get; set; }
    public double? PrecipProbabilityPercent { get; set; }
    public double? VisibilityMiles { get; set; }

    public int? Seeing { get; set; }
    public int? Transparency { get; set; }

    public double MoonAltitudeDeg { get; set; }
    public double MoonIlluminationPercent { get; set; }
    public bool MoonIsUp => MoonAltitudeDeg > 0;
    public List<string> VisiblePlanetNames { get; set; } = [];

    public int Score { get; set; }
}
