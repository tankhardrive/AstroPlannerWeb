using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public record SnrTimeEstimate(double? Minimum, double? Decent, double? Good, double? Excellent);

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

    private static double? EffectiveSb(DeepSkyObject obj)
    {
        if (obj.Type == ObjectType.OpenCluster) return null;

        if (obj.SurfaceBrightness.HasValue)
            return obj.SurfaceBrightness.Value;

        double? mag = obj.MagnitudeV ?? obj.MagnitudeB;
        if (mag == null || mag >= 99) return null;
        if (!obj.MajorAxisArcmin.HasValue || obj.MajorAxisArcmin.Value <= 0) return null;

        double aArcsec = obj.MajorAxisArcmin.Value * 60.0;
        double bArcsec = (obj.MinorAxisArcmin ?? obj.MajorAxisArcmin.Value) * 60.0;
        double areaArcsec2 = Math.PI * (aArcsec / 2.0) * (bArcsec / 2.0);
        if (areaArcsec2 <= 0) return null;

        double sb = mag.Value + 2.5 * Math.Log10(areaArcsec2);
        // Guard against point-source-like objects where derived SB would be unrealistically bright
        return sb >= 10 ? sb : null;
    }

    public static SnrTimeEstimate? Compute(ImagingSetup setup, DeepSkyObject obj, int? bortle, AppSettings settings)
    {
        if (!bortle.HasValue) return null;

        double? sbObj = EffectiveSb(obj);
        if (sbObj == null) return null;

        if (setup.ApertureMm <= 0 || setup.FocalLengthMm <= 0 || setup.PixelSizeMicrons <= 0)
            return null;

        double plateScale    = 206.265 * setup.PixelSizeMicrons / setup.FocalLengthMm;
        double pixelSolidAngle = plateScale * plateScale;
        double apertureM2   = Math.PI * Math.Pow(setup.ApertureMm / 2000.0, 2);
        double qe            = Math.Clamp(setup.QePercent, 1, 100) / 100.0;
        double R             = Math.Max(setup.ReadNoiseElectrons, 0);
        double tSub          = setup.SubExposureSeconds > 0 ? setup.SubExposureSeconds : 300.0;

        double sbSky     = BortleToSkyBrightness(bortle.Value);
        double signalRate = F0 * Math.Pow(10, -sbObj.Value / 2.5) * apertureM2 * qe * pixelSolidAngle;
        double skyRate    = F0 * Math.Pow(10, -sbSky        / 2.5) * apertureM2 * qe * pixelSolidAngle;

        if (signalRate <= 0) return null;

        // Full CCD equation: SNR = S·sqrt(T) / sqrt(S + B + R²/t_sub)
        // Solved for T: T = SNR² · (S + B + R²/t_sub) / S²
        double noiseFactor = signalRate + skyRate + (R * R) / tSub;

        double Solve(double snr) => snr * snr * noiseFactor / (signalRate * signalRate) / 3600.0;

        return new SnrTimeEstimate(
            Minimum:   Solve(settings.SnrMinimum),
            Decent:    Solve(settings.SnrDecent),
            Good:      Solve(settings.SnrGood),
            Excellent: Solve(settings.SnrExcellent));
    }

    public static string FormatHours(double? hours)
    {
        if (hours == null) return "–";
        double h = hours.Value;
        if (h < 1.0 / 60.0) return "<1m";
        if (h < 1.0) return $"{(int)Math.Round(h * 60)}m";
        if (h > 500) return ">500h";
        return h < 10 ? $"{h:F1}h" : $"{(int)Math.Round(h)}h";
    }
}
