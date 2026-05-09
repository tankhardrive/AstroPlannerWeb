using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

/// <summary>
/// Computes horizon-aware visibility windows for celestial objects over a single night.
/// Caches the twilight calculation so batch computations against the same date/site are fast.
/// </summary>
public class VisibilityService
{
    private (DateOnly Date, string SiteKey, DateTime DarkStart, DateTime DarkEnd)? _twilightCache;

    private (DateTime DarkStart, DateTime DarkEnd) GetDarkness(DateOnly date, ObservationSite site)
    {
        string key = $"{site.LatitudeDegrees:F4},{site.LongitudeDegrees:F4},{site.TimeZoneId}";
        if (_twilightCache.HasValue
            && _twilightCache.Value.Date == date
            && _twilightCache.Value.SiteKey == key)
        {
            return (_twilightCache.Value.DarkStart, _twilightCache.Value.DarkEnd);
        }
        var result = AstronomyService.GetAstronomicalDarkness(date, site);
        _twilightCache = (date, key, result.DarkStart, result.DarkEnd);
        return result;
    }

    public VisibilityWindow ComputeDso(
        DeepSkyObject obj,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            _ => (obj.RaDegrees, obj.DecDegrees),
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    public VisibilityWindow ComputeSolarSystem(
        SolarSystemObject obj,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            t => AstronomyService.GetPlanetPosition(obj.BodyType, t),
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    public VisibilityWindow ComputeComet(
        CometObject comet,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            t => AstronomyService.GetCometPosition(comet, t),
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    private VisibilityWindow Compute(
        Func<DateTime, (double Ra, double Dec)> getPosition,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        var (darkStart, darkEnd) = GetDarkness(observingDate, site);

        if (darkEnd <= darkStart)
            return VisibilityWindow.NeverVisible;

        double totalDarkMinutes = (darkEnd - darkStart).TotalMinutes;
        double visibleMinutes = 0;
        double peakAlt = double.MinValue;
        double peakAz = 0;
        double peakClearance = double.MinValue;
        DateTime peakTime = darkStart;
        double peakRa = 0, peakDec = 0;
        double visibleAltSum = 0;
        int visibleAltCount = 0;

        var steps = new List<(DateTime Time, double Alt, double HorizAlt)>();
        var t = darkStart;

        while (t <= darkEnd)
        {
            var (ra, dec) = getPosition(t);
            var (alt, az) = AstronomyService.EquatorialToHorizontal(ra, dec, t,
                site.LatitudeDegrees, site.LongitudeDegrees);
            double horizAlt = horizon.GetAltitudeAt(az);

            steps.Add((t, alt, horizAlt));

            if (alt > horizAlt)
            {
                visibleMinutes += stepMinutes;
                visibleAltSum += alt;
                visibleAltCount++;
                double clearance = alt - horizAlt;
                if (clearance > peakClearance || alt > peakAlt)
                {
                    if (alt > peakAlt)
                    {
                        peakAlt = alt;
                        peakAz = az;
                        peakTime = t;
                        peakRa = ra;
                        peakDec = dec;
                    }
                    peakClearance = Math.Max(peakClearance, clearance);
                }
            }

            t = t.AddMinutes(stepMinutes);
        }

        if (visibleMinutes <= 0)
            return VisibilityWindow.NeverVisible;

        double moonSep = AstronomyService.AngularSeparationDeg(
            peakRa, peakDec, moonInfo.RaDeg, moonInfo.DecDeg);

        DateTime? riseTime = null, setTime = null;
        bool riseDetected = false;

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Alt > steps[i].HorizAlt)
            {
                if (!riseDetected)
                {
                    riseDetected = true;
                    riseTime = steps[i].Time == darkStart ? null : steps[i].Time;
                }
                setTime = steps[i].Time == darkEnd ? null : steps[i].Time;
            }
        }

        return new VisibilityWindow
        {
            IsComputed = true,
            DarkWindowStart = darkStart,
            DarkWindowEnd = darkEnd,
            RiseTime = riseTime,
            SetTime = setTime,
            Duration = TimeSpan.FromMinutes(visibleMinutes),
            AverageAltitudeDegrees = visibleAltCount > 0 ? visibleAltSum / visibleAltCount : 0,
            PeakAltitudeDegrees = peakAlt,
            PeakAzimuthDegrees = peakAz,
            PeakClearanceDegrees = Math.Max(0, peakClearance),
            PeakTime = peakTime,
            MoonSeparationDegrees = moonSep,
            VisibilityFraction = visibleMinutes / totalDarkMinutes,
        };
    }

    /// <summary>
    /// Fast batch path for DSOs: uses pre-computed time steps and LST values to avoid
    /// recomputing GAST for each object. Call ComputeLstHours once per night/site,
    /// then call this for every object in the catalog.
    /// </summary>
    public VisibilityWindow ComputeDsoFast(
        DeepSkyObject obj,
        DateTime darkStart,
        DateTime darkEnd,
        DateTime[] timeSteps,
        double[] lstHours,
        double latDeg,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        if (darkEnd <= darkStart || timeSteps.Length == 0)
            return VisibilityWindow.NeverVisible;

        double totalDarkMinutes = (darkEnd - darkStart).TotalMinutes;
        double visibleMinutes   = 0;
        double peakAlt          = double.MinValue;
        double peakAz           = 0;
        double peakClearance    = double.MinValue;
        DateTime peakTime       = darkStart;
        double visibleAltSum    = 0;
        int    visibleAltCount  = 0;
        bool   riseDetected     = false;
        DateTime? riseTime = null, setTime = null;

        for (int i = 0; i < timeSteps.Length; i++)
        {
            var (alt, az) = AstronomyService.EquatorialToHorizontalFromLst(
                obj.RaDegrees, obj.DecDegrees, lstHours[i], latDeg);
            double horizAlt = horizon.GetAltitudeAt(az);

            if (alt > horizAlt)
            {
                visibleMinutes += stepMinutes;
                visibleAltSum  += alt;
                visibleAltCount++;
                double clearance = alt - horizAlt;

                if (alt > peakAlt)
                {
                    peakAlt      = alt;
                    peakAz       = az;
                    peakTime     = timeSteps[i];
                    peakClearance = clearance;
                }
                else if (clearance > peakClearance)
                {
                    peakClearance = clearance;
                }

                if (!riseDetected)
                {
                    riseDetected = true;
                    riseTime = timeSteps[i] == darkStart ? null : timeSteps[i];
                }
                setTime = timeSteps[i] == darkEnd ? null : timeSteps[i];
            }
        }

        if (visibleMinutes <= 0)
            return VisibilityWindow.NeverVisible;

        double moonSep = AstronomyService.AngularSeparationDeg(
            obj.RaDegrees, obj.DecDegrees, moonInfo.RaDeg, moonInfo.DecDeg);

        return new VisibilityWindow
        {
            IsComputed             = true,
            DarkWindowStart        = darkStart,
            DarkWindowEnd          = darkEnd,
            RiseTime               = riseTime,
            SetTime                = setTime,
            Duration               = TimeSpan.FromMinutes(visibleMinutes),
            AverageAltitudeDegrees = visibleAltCount > 0 ? visibleAltSum / visibleAltCount : 0,
            PeakAltitudeDegrees    = peakAlt,
            PeakAzimuthDegrees     = peakAz,
            PeakClearanceDegrees   = Math.Max(0, peakClearance),
            PeakTime               = peakTime,
            MoonSeparationDegrees  = moonSep,
            VisibilityFraction     = visibleMinutes / totalDarkMinutes,
        };
    }

    /// <summary>
    /// Returns altitude samples for a fixed-coordinate or moving object over the night.
    /// Used for the altitude plot component.
    /// </summary>
    public double[] ComputeYearlyScores(DeepSkyObject obj, ObservationLocation location, int year)
    {
        var site    = location.ToSite();
        var scores  = new double[12];
        const int step = 30;

        for (int month = 1; month <= 12; month++)
        {
            var date = new DateOnly(year, month, 15);
            var (darkStart, darkEnd) = GetDarkness(date, site);
            if (darkEnd <= darkStart) continue;

            var midNight = darkStart + (darkEnd - darkStart) / 2;
            var (moonRa, moonDec, moonIllum) = AstronomyService.GetMoonPosition(midNight);

            var steps = new List<DateTime>();
            for (var t = darkStart; t <= darkEnd; t = t.AddMinutes(step))
                steps.Add(t);
            var arr = steps.ToArray();
            var lst = AstronomyService.ComputeLstHours(arr, site.LongitudeDegrees);

            var vis = ComputeDsoFast(obj, darkStart, darkEnd, arr, lst,
                site.LatitudeDegrees, location.Horizon, step, (moonRa, moonDec, moonIllum));

            scores[month - 1] = VisibilityScorer.ComputeScore(obj, vis);
        }

        return scores;
    }

    public List<(DateTime Time, double Alt, double HorizAlt, double Az)> GetAltitudeSamples(
        Func<DateTime, (double Ra, double Dec)> getPosition,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes = 10)
    {
        var (darkStart, darkEnd) = GetDarkness(observingDate, site);
        var samples = new List<(DateTime, double, double, double)>();
        var t = darkStart;

        while (t <= darkEnd)
        {
            var (ra, dec) = getPosition(t);
            var (alt, az) = AstronomyService.EquatorialToHorizontal(ra, dec, t,
                site.LatitudeDegrees, site.LongitudeDegrees);
            samples.Add((t, alt, horizon.GetAltitudeAt(az), az));
            t = t.AddMinutes(stepMinutes);
        }

        return samples;
    }
}
