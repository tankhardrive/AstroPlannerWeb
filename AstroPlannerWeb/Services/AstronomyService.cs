using AASharp;
using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

/// <summary>
/// Core astronomical calculations: coordinate transforms, twilight, moon, planets.
/// All input/output in degrees unless noted. DateTimes are UTC unless noted.
/// </summary>
public static class AstronomyService
{
    // ── Julian Day ────────────────────────────────────────────────────────────

    public static DateTime FromJulianDay(double jd) =>
        new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(jd - 2451545.0);

    public static double ToJulianDay(DateTime utc)
    {
        double dayFraction = utc.Day + utc.TimeOfDay.TotalDays;
        return AASDate.DateToJD(utc.Year, utc.Month, dayFraction, true);
    }

    // ── Coordinate transform ──────────────────────────────────────────────────

    /// <summary>
    /// Convert equatorial (RA/Dec, J2000 degrees) to horizontal (Alt/Az degrees).
    /// Returns Az in [0,360) measured from North through East.
    /// </summary>
    public static (double Alt, double Az) EquatorialToHorizontal(
        double raDeg, double decDeg,
        DateTime utc,
        double latDeg, double lonDeg)
    {
        double jd = ToJulianDay(utc);
        double gast = AASSidereal.ApparentGreenwichSiderealTime(jd);
        double lst = gast + lonDeg / 15.0;
        lst = ((lst % 24) + 24) % 24;
        double haHours = lst - raDeg / 15.0;
        haHours = ((haHours % 24) + 24) % 24;
        if (haHours > 12) haHours -= 24;

        var horiz = AASCoordinateTransformation.Equatorial2Horizontal(haHours, decDeg, latDeg);
        double alt = horiz.Y;
        double az = (horiz.X + 180.0) % 360.0;
        if (az < 0) az += 360;
        return (alt, az);
    }

    // ── Sun ───────────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg) GetSunPosition(DateTime utc)
    {
        double jd = ToJulianDay(utc);
        double lon = AASSun.ApparentEclipticLongitude(jd, false);
        double lat = AASSun.ApparentEclipticLatitude(jd, false);
        double eps = AASNutation.TrueObliquityOfEcliptic(jd);
        var eq = AASCoordinateTransformation.Ecliptic2Equatorial(lon, lat, eps);
        return (eq.X * 15.0, eq.Y);
    }

    public static double GetSunAltitude(DateTime utc, double latDeg, double lonDeg)
    {
        var (ra, dec) = GetSunPosition(utc);
        var (alt, _) = EquatorialToHorizontal(ra, dec, utc, latDeg, lonDeg);
        return alt;
    }

    /// <summary>
    /// Find the start and end of astronomical darkness (Sun &lt; -18°) for a given local date.
    /// Returns UTC times.
    /// </summary>
    public static (DateTime DarkStart, DateTime DarkEnd) GetAstronomicalDarkness(
        DateOnly localDate, ObservationSite site)
    {
        var tz = site.GetTimeZone();
        var lat = site.LatitudeDegrees;
        var lon = site.LongitudeDegrees;

        var localNoon = new DateTime(localDate.Year, localDate.Month, localDate.Day, 12, 0, 0);
        var evening_hi = localNoon.AddHours(15);

        var darkStart = BinarySearchTwilight(
            TimeZoneInfo.ConvertTimeToUtc(localNoon, tz),
            TimeZoneInfo.ConvertTimeToUtc(evening_hi, tz),
            lat, lon, crossingDown: true);

        var morning_lo = localNoon.AddHours(13);
        var morning_hi = localNoon.AddHours(24);

        var darkEnd = BinarySearchTwilight(
            TimeZoneInfo.ConvertTimeToUtc(morning_lo, tz),
            TimeZoneInfo.ConvertTimeToUtc(morning_hi, tz),
            lat, lon, crossingDown: false);

        return (darkStart, darkEnd);
    }

    private static DateTime BinarySearchTwilight(
        DateTime lo, DateTime hi,
        double lat, double lon,
        bool crossingDown)
    {
        const double threshold = -18.0;

        for (int i = 0; i < 40; i++)
        {
            var mid = lo + (hi - lo) / 2;
            double alt = GetSunAltitude(mid, lat, lon);

            if (crossingDown)
            {
                if (alt > threshold) lo = mid;
                else hi = mid;
            }
            else
            {
                if (alt < threshold) lo = mid;
                else hi = mid;
            }
        }
        return lo + (hi - lo) / 2;
    }

    // ── Moon ─────────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg, double IlluminationPct) GetMoonPosition(DateTime utc)
    {
        double jd = ToJulianDay(utc);
        double lon = AASMoon.EclipticLongitude(jd);
        double lat = AASMoon.EclipticLatitude(jd);
        double eps = AASNutation.TrueObliquityOfEcliptic(jd);
        var eq = AASCoordinateTransformation.Ecliptic2Equatorial(lon, lat, eps);
        double ra = eq.X * 15.0;
        double dec = eq.Y;

        var (sunRa, sunDec) = GetSunPosition(utc);
        double illum = ComputeIllumination(ra, dec, sunRa, sunDec);
        return (ra, dec, illum * 100.0);
    }

    private static double ComputeIllumination(double moonRa, double moonDec, double sunRa, double sunDec)
    {
        double d2r = Math.PI / 180.0;
        double cosSep =
            Math.Sin(moonDec * d2r) * Math.Sin(sunDec * d2r) +
            Math.Cos(moonDec * d2r) * Math.Cos(sunDec * d2r) * Math.Cos((moonRa - sunRa) * d2r);
        cosSep = Math.Clamp(cosSep, -1, 1);
        double elongation = Math.Acos(cosSep);
        return (1.0 - Math.Cos(elongation)) / 2.0;
    }

    // ── Planets ───────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg) GetPlanetPosition(SolarSystemBodyType body, DateTime utc)
    {
        double jd = ToJulianDay(utc);
        double eps = AASNutation.TrueObliquityOfEcliptic(jd);

        double lon, lat;
        switch (body)
        {
            case SolarSystemBodyType.Mercury:
                lon = AASMercury.EclipticLongitude(jd, false);
                lat = AASMercury.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Venus:
                lon = AASVenus.EclipticLongitude(jd, false);
                lat = AASVenus.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Mars:
                lon = AASMars.EclipticLongitude(jd, false);
                lat = AASMars.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Jupiter:
                lon = AASJupiter.EclipticLongitude(jd, false);
                lat = AASJupiter.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Saturn:
                lon = AASSaturn.EclipticLongitude(jd, false);
                lat = AASSaturn.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Uranus:
                lon = AASUranus.EclipticLongitude(jd, false);
                lat = AASUranus.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Neptune:
                lon = AASNeptune.EclipticLongitude(jd, false);
                lat = AASNeptune.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Moon:
                var (mRa, mDec, _) = GetMoonPosition(utc);
                return (mRa, mDec);
            default:
                return GetSunPosition(utc);
        }

        var eq = AASCoordinateTransformation.Ecliptic2Equatorial(lon, lat, eps);
        return (eq.X * 15.0, eq.Y);
    }

    // ── Comets ────────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg) GetCometPosition(CometObject comet, DateTime utc)
    {
        double jd = ToJulianDay(utc);
        var elements = BuildNearParabolicElements(comet);
        var details = AASNearParabolic.Calculate(jd, ref elements, false);
        return (details.AstrometricGeocentricRA * 15.0, details.AstrometricGeocentricDeclination);
    }

    public static double? GetCometMagnitude(CometObject comet, DateTime utc)
    {
        if (comet.MagnitudeH == null) return null;
        double jd = ToJulianDay(utc);
        var elements = BuildNearParabolicElements(comet);
        try
        {
            var details = AASNearParabolic.Calculate(jd, ref elements, false);
            double delta = details.AstrometricGeocentricDistance;
            double v = 0, r = 0;
            AASNearParabolic.CalulateTrueAnnomalyAndRadius(jd, ref elements, ref v, ref r);
            if (delta <= 0 || r <= 0) return null;
            double G = comet.MagnitudeG ?? 4.0;
            return comet.MagnitudeH.Value + 5.0 * Math.Log10(delta) + 2.5 * G * Math.Log10(r);
        }
        catch { return null; }
    }

    private static AASNearParabolicObjectElements BuildNearParabolicElements(CometObject comet) =>
        new()
        {
            q = comet.PerihelionDistanceAu,
            e = comet.Eccentricity,
            i = comet.InclinationDeg,
            w = comet.ArgPerihelionDeg,
            omega = comet.LongAscNodeDeg,
            JDEquinox = 2451545.0,
            T = comet.PerihelionJd,
        };

    // ── Batch LST precomputation ──────────────────────────────────────────────

    /// <summary>
    /// Precomputes Local Sidereal Time (hours) for each UTC timestamp.
    /// Call once per night/site; pass the result to EquatorialToHorizontalFromLst
    /// to avoid recomputing GAST for every object at every time step.
    /// </summary>
    public static double[] ComputeLstHours(DateTime[] times, double lonDeg)
    {
        var lst = new double[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            double jd   = ToJulianDay(times[i]);
            double gast = AASSidereal.ApparentGreenwichSiderealTime(jd);
            lst[i] = ((gast + lonDeg / 15.0) % 24 + 24) % 24;
        }
        return lst;
    }

    /// <summary>
    /// Coordinate transform using a pre-computed LST value.
    /// Skips ToJulianDay + ApparentGreenwichSiderealTime — use when computing
    /// many objects at the same set of time steps.
    /// </summary>
    public static (double Alt, double Az) EquatorialToHorizontalFromLst(
        double raDeg, double decDeg, double lstHours, double latDeg)
    {
        double haHours = lstHours - raDeg / 15.0;
        haHours = ((haHours % 24) + 24) % 24;
        if (haHours > 12) haHours -= 24;
        var horiz = AASCoordinateTransformation.Equatorial2Horizontal(haHours, decDeg, latDeg);
        double alt = horiz.Y;
        double az  = (horiz.X + 180.0) % 360.0;
        if (az < 0) az += 360;
        return (alt, az);
    }

    // ── Angular separation ────────────────────────────────────────────────────

    public static double AngularSeparationDeg(double ra1, double dec1, double ra2, double dec2)
    {
        double d2r = Math.PI / 180.0;
        double cos =
            Math.Sin(dec1 * d2r) * Math.Sin(dec2 * d2r) +
            Math.Cos(dec1 * d2r) * Math.Cos(dec2 * d2r) * Math.Cos((ra1 - ra2) * d2r);
        return Math.Acos(Math.Clamp(cos, -1, 1)) / d2r;
    }
}
