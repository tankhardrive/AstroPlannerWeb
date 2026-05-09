using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record SetupFovResult(
    ImagingSetup Setup,
    double WidthArcmin,
    double HeightArcmin,
    double PlateScaleArcsecPx,
    double? FillPercent,   // null when object size is unknown
    bool IsBest)
{
    public string GearLabel =>
        !string.IsNullOrEmpty(Setup.TelescopeName) && !string.IsNullOrEmpty(Setup.CameraName)
            ? $"{Setup.TelescopeName} + {Setup.CameraName}"
            : Setup.Name;

    public string FovDisplay =>
        $"{FovCalculator.FormatArcmin(WidthArcmin)} × {FovCalculator.FormatArcmin(HeightArcmin)}";

    public string PlateScaleDisplay => $"{PlateScaleArcsecPx:F2}\"/px";
    public string FillDisplay => FillPercent.HasValue ? $"{FillPercent.Value:F0}%" : "–";
    public bool FitsInFrame => !FillPercent.HasValue || FillPercent.Value <= 100;

    /// <param name="objectMajorArcmin">Pass null when object size is unknown — setups still shown without fill or best-fit ranking.</param>
    public static List<SetupFovResult> Compute(
        IReadOnlyList<ImagingSetup> setups, double? objectMajorArcmin)
    {
        var usable = setups.Where(FovCalculator.IsUsable).ToList();
        if (usable.Count == 0) return [];

        ImagingSetup? best = objectMajorArcmin > 0
            ? FovCalculator.BestSetup(usable, objectMajorArcmin.Value)
            : null;
        double? target = objectMajorArcmin > 0
            ? FovCalculator.TargetFillPct(objectMajorArcmin.Value)
            : null;

        var results = usable.Select(s =>
        {
            var (w, h) = FovCalculator.FovArcmin(s);
            double ps   = FovCalculator.PlateScaleArcsecPx(s.FocalLengthMm, s.PixelSizeMicrons);
            double? fill = objectMajorArcmin > 0
                ? FovCalculator.FillPercent(s, objectMajorArcmin.Value)
                : null;
            return new SetupFovResult(s, w, h, ps, fill, s == best);
        });

        if (target.HasValue)
            return results
                .OrderByDescending(r => r.IsBest)
                .ThenBy(r => Math.Abs(r.FillPercent!.Value - target.Value))
                .ToList();

        return results.ToList();
    }
}
