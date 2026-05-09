using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record ScoreBreakdown(
    double FracEarned, double DurEarned, double AltEarned,
    double MoonEarned, double BrightEarned, double SizeEarned, double PeakEarned)
{
    public double Total => Math.Round(
        FracEarned + DurEarned + AltEarned + MoonEarned + BrightEarned + SizeEarned + PeakEarned, 1);
}

/// <summary>
/// Computes a 0–100 composite score for a DSO on a given night.
/// Logic ported from the desktop YearlyVisibilityService.ComputeScore.
/// </summary>
public static class VisibilityScorer
{
    /// <summary>
    /// Sky quality factor from Bortle class: 1.0 (Bortle 1, pristine) → 0.5 (Bortle 9, inner city).
    /// Applied to the brightness component so faint objects score lower in light-polluted skies.
    /// </summary>
    private static double SkyFactor(int? bortleClass) =>
        bortleClass.HasValue
            ? Math.Clamp(1.0 - (bortleClass.Value - 1) * 0.056, 0.5, 1.0)
            : 0.75; // default to mid-range when not configured

    public static double ComputeScore(DeepSkyObject obj, VisibilityWindow vis, int? bortleClass = null)
    {
        if (!vis.IsVisible) return 0;

        double frac   = vis.VisibilityFraction * 15;
        double dur    = Math.Clamp(vis.Duration.TotalHours / 5.0, 0, 1) * 10;
        double alt    = Math.Min(vis.AverageAltitudeDegrees / 45.0, 1.0) * 20;
        double moon   = Math.Min(vis.MoonSeparationDegrees / 90.0, 1.0) * 20;
        double? mag   = obj.DisplayMagnitude < 99 ? obj.DisplayMagnitude : null;
        double sky    = SkyFactor(bortleClass);
        double bright = mag.HasValue ? Math.Clamp((15.0 - mag.Value) / 15.0, 0, 1) * 15 * sky : 7.5 * sky;
        double? arcmin = obj.MajorAxisArcmin;
        double size   = arcmin is double s && s > 0
            ? Math.Clamp(Math.Log10(Math.Max(s, 1)) / Math.Log10(30), 0, 1) * 10 : 0;
        double peakDeg = vis.PeakAltitudeDegrees;
        double peak   = peakDeg < 10 ? 0
            : peakDeg < 20 ? (peakDeg - 10) / 10.0 * 3
            : Math.Min((peakDeg - 20) / 70.0, 1.0) * 7 + 3;

        return Math.Round(Math.Max(frac + dur + alt + moon + bright + size + peak, 0), 1);
    }

    public static ScoreBreakdown? ComputeBreakdown(DeepSkyObject obj, VisibilityWindow vis, int? bortleClass = null)
    {
        if (!vis.IsVisible) return null;

        double frac    = vis.VisibilityFraction * 15;
        double dur     = Math.Clamp(vis.Duration.TotalHours / 5.0, 0, 1) * 10;
        double alt     = Math.Min(vis.AverageAltitudeDegrees / 45.0, 1.0) * 20;
        double moon    = Math.Min(vis.MoonSeparationDegrees / 90.0, 1.0) * 20;
        double? mag    = obj.DisplayMagnitude < 99 ? obj.DisplayMagnitude : null;
        double sky     = SkyFactor(bortleClass);
        double bright  = mag.HasValue ? Math.Clamp((15.0 - mag.Value) / 15.0, 0, 1) * 15 * sky : 7.5 * sky;
        double? arcmin = obj.MajorAxisArcmin;
        double size    = arcmin is double s && s > 0
            ? Math.Clamp(Math.Log10(Math.Max(s, 1)) / Math.Log10(30), 0, 1) * 10 : 0;
        double peakDeg = vis.PeakAltitudeDegrees;
        double peak    = peakDeg < 10 ? 0
            : peakDeg < 20 ? (peakDeg - 10) / 10.0 * 3
            : Math.Min((peakDeg - 20) / 70.0, 1.0) * 7 + 3;

        return new ScoreBreakdown(
            Math.Round(frac, 1), Math.Round(dur, 1), Math.Round(alt, 1),
            Math.Round(moon, 1), Math.Round(bright, 1), Math.Round(size, 1), Math.Round(peak, 1));
    }

    /// <summary>Planet/Moon score: 0–100 based purely on altitude and window, no brightness/size factors.</summary>
    public static double ComputePlanetScore(VisibilityWindow vis)
    {
        if (!vis.IsVisible) return 0;
        double frac = vis.VisibilityFraction * 30;
        double alt  = Math.Min(vis.AverageAltitudeDegrees / 45.0, 1.0) * 40;
        double peak = Math.Min(vis.PeakAltitudeDegrees / 60.0, 1.0) * 30;
        return Math.Round(Math.Max(frac + alt + peak, 0), 1);
    }

    public static string ScoreDisplay(double score) =>
        score <= 0 ? "–" : score.ToString("F0");

    public static string ScoreCssClass(double score) => score switch
    {
        <= 0  => "score-none",
        < 20  => "score-poor",
        < 40  => "score-fair",
        < 60  => "score-good",
        < 80  => "score-great",
        _     => "score-excellent",
    };
}
