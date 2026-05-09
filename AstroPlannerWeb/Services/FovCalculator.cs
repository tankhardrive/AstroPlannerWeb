using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public static class FovCalculator
{
    public static double PlateScaleArcsecPx(double focalLengthMm, double pixelSizeMicrons)
        => focalLengthMm > 0 ? 206.265 * pixelSizeMicrons / focalLengthMm : 0;

    public static (double WidthArcmin, double HeightArcmin) FovArcmin(ImagingSetup s)
    {
        double ps = PlateScaleArcsecPx(s.FocalLengthMm, s.PixelSizeMicrons);
        return (ps * s.SensorWidthPixels / 60.0, ps * s.SensorHeightPixels / 60.0);
    }

    public static double FillPercent(ImagingSetup s, double objectMajorArcmin)
    {
        var (w, h) = FovArcmin(s);
        double longSide = Math.Max(w, h);
        return longSide > 0 ? objectMajorArcmin / longSide * 100 : 0;
    }

    public static double TargetFillPct(double majorArcmin)
    {
        if (majorArcmin < 5)   return 40;
        if (majorArcmin < 20)  return 50;
        if (majorArcmin < 60)  return 65;
        if (majorArcmin < 120) return 80;
        return 90;
    }

    public static bool IsUsable(ImagingSetup s)
        => s.FocalLengthMm > 0
        && s.PixelSizeMicrons > 0
        && s.SensorWidthPixels > 0
        && s.SensorHeightPixels > 0;

    public static ImagingSetup? BestSetup(IReadOnlyList<ImagingSetup> setups, double objectMajorArcmin)
    {
        if (setups.Count == 0 || objectMajorArcmin <= 0) return null;
        double target = TargetFillPct(objectMajorArcmin);
        return setups
            .Where(IsUsable)
            .OrderBy(s => Math.Abs(FillPercent(s, objectMajorArcmin) - target))
            .FirstOrDefault();
    }

    public static string FormatArcmin(double arcmin)
        => arcmin >= 60 ? $"{arcmin / 60.0:F1}°" : $"{arcmin:F0}′";
}
