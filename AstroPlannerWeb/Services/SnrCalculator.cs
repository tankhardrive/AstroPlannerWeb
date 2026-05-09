using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record SnrTimeEstimate(double Minimum, double Decent, double Good, double Excellent, bool IsBrightCore);

public static class SnrCalculator
{
    // V-band zero-point: ~8.93e9 photons/s/m² integrated over ~89 nm bandwidth.
    // Catalog SB and Bortle-derived sky SB are both in V-band equivalents, so this is internally consistent.
    private const double F0 = 8.93e9;

    private static double BortleToSkyBrightness(int bortle) => bortle switch
    {
        1 => 22.0,
        2 => 21.5,
        3 => 21.0,
        4 => 20.5,
        5 => 19.5,
        6 => 18.5,
        7 => 18.0,
        8 => 17.5,
        _ => 17.0,
    };

    // Returns (sb, isBrightCore) or null if the object type can't be estimated at all.
    private static (double Sb, bool IsBrightCore)? EffectiveSb(DeepSkyObject obj)
    {
        if (obj.Type == ObjectType.OpenCluster) return null;

        double? sb = obj.SurfaceBrightness;

        if (sb == null)
        {
            double? mag = obj.MagnitudeV ?? obj.MagnitudeB;
            if (mag == null || mag >= 99) return null;
            if (!obj.MajorAxisArcmin.HasValue || obj.MajorAxisArcmin.Value <= 0) return null;

            double aArcsec = obj.MajorAxisArcmin.Value * 60.0;
            double bArcsec = (obj.MinorAxisArcmin ?? obj.MajorAxisArcmin.Value) * 60.0;
            double areaArcsec2 = Math.PI * (aArcsec / 2.0) * (bArcsec / 2.0);
            if (areaArcsec2 <= 0) return null;

            sb = mag.Value + 2.5 * Math.Log10(areaArcsec2);
        }

        if (sb < 10) return null;

        // SB < 18 means catalog size likely reflects the bright inner region, not faint extensions.
        // Times will be correct for the core but not for capturing the full object.
        return (sb.Value, sb < 18);
    }

    public static SnrTimeEstimate? Compute(ImagingSetup setup, DeepSkyObject obj, int? bortle, AppSettings settings)
    {
        if (!bortle.HasValue) return null;

        var sbResult = EffectiveSb(obj);
        if (sbResult == null) return null;
        var (sbObj, isBrightCore) = sbResult.Value;

        if (setup.ApertureMm <= 0 || setup.FocalLengthMm <= 0 || setup.PixelSizeMicrons <= 0)
            return null;

        double plateScale      = 206.265 * setup.PixelSizeMicrons / setup.FocalLengthMm;
        double pixelSolidAngle = plateScale * plateScale;
        double apertureM2      = Math.PI * Math.Pow(setup.ApertureMm / 2000.0, 2);
        double qe              = Math.Clamp(setup.QePercent, 1, 100) / 100.0;
        double R               = Math.Max(setup.ReadNoiseElectrons, 0);
        double tSub            = setup.SubExposureSeconds > 0 ? setup.SubExposureSeconds : 300.0;

        double sbSky      = BortleToSkyBrightness(bortle.Value);
        double signalRate = F0 * Math.Pow(10, -sbObj / 2.5) * apertureM2 * qe * pixelSolidAngle;
        double skyRate    = F0 * Math.Pow(10, -sbSky  / 2.5) * apertureM2 * qe * pixelSolidAngle;

        if (signalRate <= 0) return null;

        // Full CCD equation: SNR = S·sqrt(T) / sqrt(S + B + R²/t_sub)
        // Solved for T: T = SNR² · (S + B + R²/t_sub) / S²
        double noiseFactor = signalRate + skyRate + (R * R) / tSub;
        double Solve(double snr) => snr * snr * noiseFactor / (signalRate * signalRate) / 3600.0;

        double minimum = Solve(settings.SnrMinimum);

        // If even the minimum tier is < 30 min, flag as bright core regardless of SB threshold
        return new SnrTimeEstimate(
            Minimum:      minimum,
            Decent:       Solve(settings.SnrDecent),
            Good:         Solve(settings.SnrGood),
            Excellent:    Solve(settings.SnrExcellent),
            IsBrightCore: isBrightCore || minimum < 0.5);
    }

    public static string FormatHours(double hours)
    {
        if (hours < 1.0 / 60.0) return "<1m";
        if (hours < 1.0) return $"{(int)Math.Round(hours * 60)}m";
        if (hours > 500) return ">500h";
        return hours < 10 ? $"{hours:F1}h" : $"{(int)Math.Round(hours)}h";
    }
}
