namespace AstroPlannerWeb.Models;

public class AppSettings
{
    public List<ObservationLocation> Locations { get; set; } = [];
    public string ActiveLocationName { get; set; } = "";
    public int VisibilityStepMinutes { get; set; } = 15;
    public List<ImagingSetup> ImagingSetups { get; set; } = [];
    public WeatherThresholds WeatherThresholds { get; set; } = new();
    public Dictionary<string, ObjectAnnotation> Annotations { get; set; } = [];
    public bool ApplySkyQualityToScore { get; set; } = false;

    public ObservationLocation GetActiveLocation()
    {
        return Locations.FirstOrDefault(l => l.Name == ActiveLocationName)
               ?? Locations.FirstOrDefault()
               ?? new ObservationLocation();
    }

    /// <summary>Seeds a default location on fresh install; fixes missing ActiveLocationName.</summary>
    public void MigrateIfNeeded()
    {
        if (Locations.Count == 0)
        {
            Locations =
            [
                new ObservationLocation
                {
                    Name = "My Location",
                    TimeZoneId = "UTC",
                }
            ];
            ActiveLocationName = "My Location";
        }

        if (string.IsNullOrEmpty(ActiveLocationName))
            ActiveLocationName = Locations[0].Name;
    }
}
