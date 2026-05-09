using System.Text.Json.Serialization;

namespace AstroPlannerWeb.Models;

public class CometObject
{
    public string Designation { get; init; } = "";
    public string Name { get; init; } = "";
    public double PerihelionDistanceAu { get; init; }
    public double Eccentricity { get; init; }
    public double ArgPerihelionDeg { get; init; }
    public double LongAscNodeDeg { get; init; }
    public double InclinationDeg { get; init; }
    public double PerihelionJd { get; init; }
    public double? MagnitudeH { get; init; }
    public double? MagnitudeG { get; init; }

    [JsonIgnore] public double RaDegrees { get; set; }
    [JsonIgnore] public double DecDegrees { get; set; }
    [JsonIgnore] public double? Magnitude { get; set; }

    [JsonIgnore]
    public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : Designation;
}
