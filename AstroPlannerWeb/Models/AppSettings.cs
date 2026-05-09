namespace AstroPlannerWeb.Models;

public enum SnrPreset { Community, Conservative, Custom }

public class AppSettings
{
    public List<ObservationLocation> Locations { get; set; } = [];
    public string ActiveLocationName { get; set; } = "";
    public int VisibilityStepMinutes { get; set; } = 15;
    public List<ImagingSetup> ImagingSetups { get; set; } = [];
    public WeatherThresholds WeatherThresholds { get; set; } = new();
    public Dictionary<string, ObjectAnnotation> Annotations { get; set; } = [];
    public bool ApplySkyQualityToScore { get; set; } = false;
    public bool Use12HourTime { get; set; } = false;

    public SnrPreset SnrPreset { get; set; } = SnrPreset.Community;
    public double SnrMinimum { get; set; } = 5;
    public double SnrDecent { get; set; } = 15;
    public double SnrGood { get; set; } = 30;
    public double SnrExcellent { get; set; } = 50;

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
