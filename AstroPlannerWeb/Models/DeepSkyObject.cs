namespace AstroPlannerWeb.Models;

public class DeepSkyObject
{
    public required string Name { get; init; }
    public ObjectType Type { get; init; }

    /// <summary>J2000 right ascension in decimal degrees.</summary>
    public double RaDegrees { get; init; }

    /// <summary>J2000 declination in decimal degrees.</summary>
    public double DecDegrees { get; init; }

    public string Constellation { get; init; } = "";

    /// <summary>Major axis in arcminutes.</summary>
    public double? MajorAxisArcmin { get; init; }

    /// <summary>Minor axis in arcminutes.</summary>
    public double? MinorAxisArcmin { get; init; }

    public double? PositionAngleDeg { get; init; }
    public double? MagnitudeB { get; init; }
    public double? MagnitudeV { get; init; }
    public double? SurfaceBrightness { get; init; }
    public string? HubbleType { get; init; }

    public int? MessierNumber { get; init; }
    public int? CaldwellNumber { get; init; }
    public string? CommonName { get; init; }

    public CatalogSource Catalogs { get; init; }

    /// <summary>Best available visual magnitude for sorting/display.</summary>
    public double DisplayMagnitude => MagnitudeV ?? MagnitudeB ?? 99.0;

    public string SizeDisplay
    {
        get
        {
            if (MajorAxisArcmin is null) return "";
            if (MinorAxisArcmin is null || Math.Abs(MajorAxisArcmin.Value - MinorAxisArcmin.Value) < 0.01)
                return $"{MajorAxisArcmin:F1}'";
            return $"{MajorAxisArcmin:F1}' × {MinorAxisArcmin:F1}'";
        }
    }

    /// <summary>Primary display name: common name if available, Messier designation next, otherwise catalog name.</summary>
    public string DisplayName =>
        !string.IsNullOrEmpty(CommonName) ? CommonName :
        MessierNumber.HasValue ? $"M{MessierNumber}" :
        ShortName;

    /// <summary>Short catalog name without leading zeros, e.g. "NGC 224" or "IC 342".</summary>
    public string ShortName
    {
        get
        {
            if (Name.StartsWith("NGC"))
            {
                var suffix = Name[3..];
                return int.TryParse(suffix, out int n) ? $"NGC {n}" : $"NGC {suffix.TrimStart('0')}";
            }
            if (Name.StartsWith("IC"))
            {
                var suffix = Name[2..];
                return int.TryParse(suffix, out int n) ? $"IC {n}" : $"IC {suffix.TrimStart('0')}";
            }
            return Name;
        }
    }

    /// <summary>All catalog designations, e.g. "M31 / NGC 224".</summary>
    public string CatalogIds
    {
        get
        {
            var parts = new List<string>();
            if (MessierNumber.HasValue) parts.Add($"M{MessierNumber}");
            if (CaldwellNumber.HasValue) parts.Add($"C{CaldwellNumber}");
            parts.Add(ShortName);
            return string.Join(" / ", parts);
        }
    }
}
