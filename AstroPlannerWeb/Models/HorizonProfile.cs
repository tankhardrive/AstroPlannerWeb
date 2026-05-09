using System.Globalization;

namespace AstroPlannerWeb.Models;

public class HorizonPoint
{
    public double Az { get; set; }
    public double Alt { get; set; }
    public HorizonPoint() { }
    public HorizonPoint(double az, double alt) { Az = az; Alt = alt; }
}

public class HorizonProfile
{
    public string Name { get; set; } = "Default";

    /// <summary>Sorted list of (Az, Alt) pairs. Az in [0, 360), Alt in degrees.</summary>
    public List<HorizonPoint> Points { get; set; } = [];

    /// <summary>Returns interpolated horizon altitude for a given azimuth.</summary>
    public double GetAltitudeAt(double azimuth)
    {
        if (Points.Count == 0) return 0.0;
        if (Points.Count == 1) return Points[0].Alt;

        azimuth = ((azimuth % 360) + 360) % 360;
        int n = Points.Count;

        int upperIdx = Points.FindIndex(p => p.Az > azimuth);

        if (upperIdx == -1)
        {
            var p1 = Points[n - 1];
            var p2 = Points[0];
            double az2 = p2.Az + 360.0;
            double t = (az2 - p1.Az) < 1e-9 ? 0 : (azimuth - p1.Az) / (az2 - p1.Az);
            return p1.Alt + t * (p2.Alt - p1.Alt);
        }

        if (upperIdx == 0)
        {
            var p1 = Points[n - 1];
            var p2 = Points[0];
            double az1 = p1.Az - 360.0;
            double t = (p2.Az - az1) < 1e-9 ? 0 : (azimuth - az1) / (p2.Az - az1);
            return p1.Alt + t * (p2.Alt - p1.Alt);
        }

        var lower = Points[upperIdx - 1];
        var upper = Points[upperIdx];
        double fraction = (upper.Az - lower.Az) < 1e-9 ? 0 : (azimuth - lower.Az) / (upper.Az - lower.Az);
        return lower.Alt + fraction * (upper.Alt - lower.Alt);
    }

    /// <summary>
    /// Parse Stellarium horizon format.
    /// Lines starting with # are comments. Data lines: "AZ ALT" (degrees, space-separated).
    /// </summary>
    public static HorizonProfile ParseStellariumFormat(string name, string text)
    {
        var profile = new HorizonProfile { Name = name };

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double az)) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double alt)) continue;

            profile.Points.Add(new HorizonPoint(((az % 360) + 360) % 360, Math.Clamp(alt, -90, 90)));
        }

        profile.Points.Sort((a, b) => a.Az.CompareTo(b.Az));
        return profile;
    }

    public static HorizonProfile Flat(string name = "Flat (0°)") =>
        new() { Name = name, Points = [new(0, 0), new(180, 0), new(359.9, 0)] };
}
