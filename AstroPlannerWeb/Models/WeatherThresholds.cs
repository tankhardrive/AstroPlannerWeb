namespace AstroPlannerWeb.Models;

public class WeatherThresholds
{
    public int MaxCloudCoverPercent { get; set; } = 20;
    public int MaxWindSpeedMph { get; set; } = 20;
    public int MinWindChillF { get; set; } = 30;
    public int MaxHumidityPercent { get; set; } = 85;
    public int MaxPrecipProbabilityPercent { get; set; } = 10;
    public int MinVisibilityMiles { get; set; } = 5;
    public int MaxSeeing { get; set; } = 3;
    public int MinTransparency { get; set; } = 4;
    public int MaxMoonIlluminationPercent { get; set; } = 50;
}
