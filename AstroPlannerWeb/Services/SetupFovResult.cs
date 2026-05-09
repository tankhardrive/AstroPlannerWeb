using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record SetupFovResult(
    ImagingSetup Setup,
    double WidthArcmin,
    double HeightArcmin,
    double PlateScaleArcsecPx,
    double FillPercent,
    bool IsBest)
{
    public string GearLabel =>
        !string.IsNullOrEmpty(Setup.TelescopeName) && !string.IsNullOrEmpty(Setup.CameraName)
            ? $"{Setup.TelescopeName} + {Setup.CameraName}"
            : Setup.Name;

    public string FovDisplay =>
        $"{FovCalculator.FormatArcmin(WidthArcmin)} × {FovCalculator.FormatArcmin(HeightArcmin)}";

    public string PlateScaleDisplay => $"{PlateScaleArcsecPx:F2}\"/px";
    public string FillDisplay => $"{FillPercent:F0}%";
    public bool FitsInFrame => FillPercent <= 100;

    public static List<SetupFovResult> Compute(
        IReadOnlyList<ImagingSetup> setups, double objectMajorArcmin)
    {
        var usable = setups.Where(FovCalculator.IsUsable).ToList();
        if (usable.Count == 0 || objectMajorArcmin <= 0) return [];

        var best = FovCalculator.BestSetup(usable, objectMajorArcmin);
        double target = FovCalculator.TargetFillPct(objectMajorArcmin);

        return usable
            .Select(s =>
            {
                var (w, h) = FovCalculator.FovArcmin(s);
                double ps = FovCalculator.PlateScaleArcsecPx(s.FocalLengthMm, s.PixelSizeMicrons);
                double fill = FovCalculator.FillPercent(s, objectMajorArcmin);
                return new SetupFovResult(s, w, h, ps, fill, s == best);
            })
            .OrderByDescending(r => r.IsBest)
            .ThenBy(r => Math.Abs(r.FillPercent - target))
            .ToList();
    }
}
