using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record YearlyRow(DeepSkyObject Object, ObjectAnnotation? Annotation, double[] Scores);

file record MonthData(DateTime DarkStart, DateTime DarkEnd, DateTime[] TimeSteps, double[] LstHours,
    (double RaDeg, double DecDeg, double IllumPct) MoonInfo);

/// <summary>
/// Holds the last completed yearly heatmap computation so navigating back doesn't
/// recompute. Can also be pre-warmed by the Planner after it finishes its nightly run.
/// </summary>
public class YearlyStateService
{
    private int    _year;
    private string _locationName = "";
    private List<YearlyRow> _rows = [];

    private int    _computingYear;
    private string _computingLocation = "";
    private CancellationTokenSource? _cts;

    public bool IsComputing    { get; private set; }
    public int  ProcessedCount { get; private set; }
    public int  TotalCount     { get; private set; }

    /// <summary>Fires after each batch (progress) and on completion.</summary>
    public event Action? OnChanged;

    public bool IsValid(int year, string locationName)
        => _rows.Count > 0 && _year == year && _locationName == locationName;

    public List<YearlyRow> Rows => _rows;

    public void UpdateAnnotation(string objectName, ObjectAnnotation? ann)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Object.Name != objectName) continue;
            _rows[i] = _rows[i] with { Annotation = ann };
            break;
        }
    }

    public void Invalidate()
    {
        _cts?.Cancel();
        _rows = [];
        IsComputing = false;
        OnChanged?.Invoke();
    }

    public async Task EnsureComputedAsync(
        int year, string locationName,
        AppSettings settings,
        CatalogService catalogService,
        VisibilityService visibilityService)
    {
        if (IsValid(year, locationName)) return;

        // Same computation already running — caller will be notified via OnChanged
        if (IsComputing && _computingYear == year && _computingLocation == locationName)
            return;

        // Cancel any previous/different computation and restart
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _computingYear     = year;
        _computingLocation = locationName;
        IsComputing        = true;
        _rows              = [];
        ProcessedCount     = 0;

        try
        {
            var location = settings.GetActiveLocation();
            if (location == null) { IsComputing = false; return; }

            var site    = location.ToSite();
            var horizon = location.Horizon;
            const int step = 30;
            var catalog = catalogService.GetAll();
            TotalCount = catalog.Count;
            int? bortle = location.BortleClass;

            // Precompute night data for the 15th of each month
            var monthData = new MonthData[12];
            for (int m = 0; m < 12; m++)
            {
                ct.ThrowIfCancellationRequested();
                var date = new DateOnly(year, m + 1, 15);
                var (ds, de) = AstronomyService.GetAstronomicalDarkness(date, site);
                if (ds >= de) { monthData[m] = new MonthData(ds, de, [], [], (0, 0, 1)); continue; }
                var steps = BuildSteps(ds, de, step);
                var lst   = AstronomyService.ComputeLstHours(steps, site.LongitudeDegrees);
                var mid   = ds + (de - ds) / 2;
                var (mRa, mDec, mIll) = AstronomyService.GetMoonPosition(mid);
                monthData[m] = new MonthData(ds, de, steps, lst, (mRa, mDec, mIll));
            }

            const int batchSize = 200;
            var rows = new List<YearlyRow>(catalog.Count);

            for (int i = 0; i < catalog.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                int end = Math.Min(i + batchSize, catalog.Count);

                for (int j = i; j < end; j++)
                {
                    var obj    = catalog[j];
                    var scores = new double[12];
                    for (int m = 0; m < 12; m++)
                    {
                        var md = monthData[m];
                        if (md.TimeSteps.Length == 0) continue;
                        var w = visibilityService.ComputeDsoFast(
                            obj, md.DarkStart, md.DarkEnd, md.TimeSteps, md.LstHours,
                            site.LatitudeDegrees, horizon, step, md.MoonInfo);
                        scores[m] = VisibilityScorer.ComputeScore(obj, w, bortle);
                    }
                    settings.Annotations.TryGetValue(obj.Name, out var ann);
                    rows.Add(new YearlyRow(obj, ann, scores));
                }

                ProcessedCount = end;
                await Task.Yield();
                OnChanged?.Invoke();
            }

            _year         = year;
            _locationName = locationName;
            _rows         = rows;
            IsComputing   = false;
            OnChanged?.Invoke();
        }
        catch (OperationCanceledException)
        {
            IsComputing = false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Yearly] Error: {ex.Message}");
            IsComputing = false;
            OnChanged?.Invoke();
        }
    }

    private static DateTime[] BuildSteps(DateTime start, DateTime end, int stepMinutes)
    {
        var list = new List<DateTime>();
        for (var t = start; t <= end; t = t.AddMinutes(stepMinutes))
            list.Add(t);
        return [.. list];
    }
}
