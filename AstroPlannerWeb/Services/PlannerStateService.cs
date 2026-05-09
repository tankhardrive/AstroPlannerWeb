using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record PlannerRow
{
    public DeepSkyObject?      Object      { get; init; }
    public SolarSystemObject?  SolarObject { get; init; }
    public VisibilityWindow    Window      { get; init; } = VisibilityWindow.NeverVisible;
    public double              Score       { get; init; }
    public ObjectAnnotation?   Annotation  { get; init; }
    public double?             BestFillPct { get; init; }

    public bool   IsSolarSystem => SolarObject != null;
    public string RowName       => SolarObject?.Name ?? Object?.Name ?? "";
    public string DisplayName   => SolarObject?.Name ?? Object?.DisplayName ?? "";
}

public record NightInfo(DateTime DarkStart, DateTime DarkEnd, double MoonIllum);

/// <summary>
/// Holds the last completed Planner computation so navigating away and back
/// doesn't trigger a full recompute when the date and location haven't changed.
/// </summary>
public class PlannerStateService
{
    private DateOnly _date;
    private string _locationName = "";
    private List<PlannerRow> _rows = [];
    private NightInfo? _night;

    public bool IsValid(DateOnly date, string locationName)
        => _rows.Count > 0 && _date == date && _locationName == locationName;

    public (List<PlannerRow> Rows, NightInfo? Night) Get() => (_rows, _night);

    public void Store(DateOnly date, string locationName, List<PlannerRow> rows, NightInfo? night)
    {
        _date = date;
        _locationName = locationName;
        _rows = rows;
        _night = night;
    }

    public void Invalidate() => _rows = [];

    public string SortColumn    { get; set; } = "Score";
    public bool   SortAscending { get; set; } = false;

    public DateOnly? CachedDate => _rows.Count > 0 ? _date : null;

    public PlannerRow? GetRow(string objectName)
        => _rows.FirstOrDefault(r =>
            (r.Object?.Name == objectName) ||
            (r.SolarObject?.Name == objectName));
}
