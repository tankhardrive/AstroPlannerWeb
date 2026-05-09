using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record YearlyRow(DeepSkyObject Object, ObjectAnnotation? Annotation, double[] Scores);

file record MonthData(
    DateTime DarkStart, DateTime DarkEnd, double[] LstHours,
    (double RaDeg, double DecDeg, double IllumPct) MoonInfo,
    double TotalDarkMinutes);

/// <summary>
/// Caches the yearly heatmap computation so navigating back is instant.
/// Can be pre-warmed by the Planner after it finishes its nightly run.
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
        CatalogService catalogService)
    {
        if (IsValid(year, locationName)) return;
        if (IsComputing && _computingYear == year && _computingLocation == locationName) return;

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
            await catalogService.EnsureLoadedAsync();
            var catalog = catalogService.GetAll();
            TotalCount  = catalog.Count;
            int? bortle = location.BortleClass;
            const int step = 30;

            // Constants
            const double d2r        = Math.PI / 180.0;
            const double r2d        = 180.0 / Math.PI;
            const double haConv     = Math.PI / 12.0;  // hours → radians

            // Location trig — constant for all objects
            double sinLat = Math.Sin(site.LatitudeDegrees * d2r);
            double cosLat = Math.Cos(site.LatitudeDegrees * d2r);
            double latDeg = site.LatitudeDegrees;

            // Sky factor — constant for this location/bortle setting
            double skyFactor = bortle.HasValue
                ? Math.Clamp(1.0 - (bortle.Value - 1) * 0.056, 0.5, 1.0)
                : 0.75;

            // Precompute 12 months of night data
            var monthData = new MonthData?[12];
            for (int m = 0; m < 12; m++)
            {
                ct.ThrowIfCancellationRequested();
                var date = new DateOnly(year, m + 1, 15);
                var (ds, de) = AstronomyService.GetAstronomicalDarkness(date, site);
                if (ds >= de) continue;
                var steps = BuildSteps(ds, de, step);
                var lst   = AstronomyService.ComputeLstHours(steps, site.LongitudeDegrees);
                var mid   = ds + (de - ds) / 2;
                var (mRa, mDec, mIll) = AstronomyService.GetMoonPosition(mid);
                monthData[m] = new MonthData(ds, de, lst, (mRa, mDec, mIll), (de - ds).TotalMinutes);
            }

            const int batchSize = 200;
            var rows = new List<YearlyRow>(catalog.Count);

            for (int i = 0; i < catalog.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                int end = Math.Min(i + batchSize, catalog.Count);

                for (int j = i; j < end; j++)
                {
                    var obj = catalog[j];

                    // ── Geometric pre-filter ──────────────────────────────────────────
                    // Objects that can never rise above the flat horizon at this latitude.
                    // maxAlt = 90° − |lat − dec|; skip if ≤ 0.
                    if (90.0 - Math.Abs(latDeg - obj.DecDegrees) <= 0.0) continue;

                    // ── Per-object trig (constant across all months + time steps) ─────
                    double sinDec  = Math.Sin(obj.DecDegrees * d2r);
                    double cosDec  = Math.Cos(obj.DecDegrees * d2r);
                    double raHours = obj.RaDegrees / 15.0;

                    // ── Per-object scoring constants ──────────────────────────────────
                    double? mag   = obj.DisplayMagnitude < 99 ? obj.DisplayMagnitude : null;
                    double bright = mag.HasValue
                        ? Math.Clamp((15.0 - mag.Value) / 15.0, 0, 1) * 15 * skyFactor
                        : 7.5 * skyFactor;
                    double size = obj.MajorAxisArcmin is double ax && ax > 0
                        ? Math.Clamp(Math.Log10(Math.Max(ax, 1)) / Math.Log10(30), 0, 1) * 10
                        : 0.0;

                    var scores = new double[12];
                    for (int m = 0; m < 12; m++)
                    {
                        var md = monthData[m];
                        if (md is null) continue;

                        // ── Altitude-only inner loop (no azimuth, flat 0° horizon) ────
                        // cos(HA) is even so no HA normalization needed for altitude.
                        double visibleMinutes = 0;
                        double peakAlt        = double.MinValue;
                        double altSum         = 0;
                        int    visibleCount   = 0;

                        for (int k = 0; k < md.LstHours.Length; k++)
                        {
                            double haRad = (md.LstHours[k] - raHours) * haConv;
                            double sinAlt = sinLat * sinDec + cosLat * cosDec * Math.Cos(haRad);
                            double alt    = Math.Asin(Math.Clamp(sinAlt, -1.0, 1.0)) * r2d;

                            if (alt > 0.0)
                            {
                                visibleMinutes += step;
                                altSum         += alt;
                                visibleCount++;
                                if (alt > peakAlt) peakAlt = alt;
                            }
                        }

                        if (visibleMinutes <= 0) continue;

                        double moonSep = AstronomyService.AngularSeparationDeg(
                            obj.RaDegrees, obj.DecDegrees, md.MoonInfo.RaDeg, md.MoonInfo.DecDeg);

                        // ── Inline score (mirrors VisibilityScorer.ComputeScore) ──────
                        double avgAlt  = altSum / visibleCount;
                        double frac    = visibleMinutes / md.TotalDarkMinutes * 15;
                        double dur     = Math.Clamp(visibleMinutes / 300.0, 0, 1) * 10;
                        double altPts  = Math.Min(avgAlt / 45.0, 1.0) * 20;
                        double moonPts = Math.Min(moonSep / 90.0, 1.0) * 20;
                        double peak    = peakAlt < 10 ? 0
                            : peakAlt < 20 ? (peakAlt - 10) / 10.0 * 3
                            : Math.Min((peakAlt - 20) / 70.0, 1.0) * 7 + 3;

                        scores[m] = Math.Round(
                            Math.Max(frac + dur + altPts + moonPts + bright + size + peak, 0), 1);
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
