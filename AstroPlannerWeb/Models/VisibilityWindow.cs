namespace AstroPlannerWeb.Models;

public class VisibilityWindow
{
    public static readonly VisibilityWindow NotComputed = new() { IsComputed = false };
    public static readonly VisibilityWindow NeverVisible = new() { IsComputed = true, Duration = TimeSpan.Zero };

    public bool IsComputed { get; init; }
    public bool IsVisible => IsComputed && Duration > TimeSpan.Zero;

    public DateTime DarkWindowStart { get; init; }
    public DateTime DarkWindowEnd { get; init; }

    /// <summary>When the object first clears the horizon. Null = already above at darkness start.</summary>
    public DateTime? RiseTime { get; init; }

    /// <summary>When the object drops below the horizon. Null = still above at darkness end.</summary>
    public DateTime? SetTime { get; init; }

    public TimeSpan Duration { get; init; }
    public double AverageAltitudeDegrees { get; init; }
    public double PeakAltitudeDegrees { get; init; }
    public double PeakAzimuthDegrees { get; init; }
    public double PeakClearanceDegrees { get; init; }
    public DateTime PeakTime { get; init; }
    public double MoonSeparationDegrees { get; init; }
    public double VisibilityFraction { get; init; }
}
