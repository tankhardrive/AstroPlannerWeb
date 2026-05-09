namespace AstroPlannerWeb.Models;

public enum ObjectType
{
    Galaxy,
    GalaxyPair,
    GalaxyTriplet,
    GalaxyGroup,
    OpenCluster,
    GlobularCluster,
    ClusterNebula,
    PlanetaryNebula,
    EmissionNebula,
    ReflectionNebula,
    SupernovaRemnant,
    HiiRegion,
    DarkNebula,
    BrightNebula,
    Nebula,
    StellarAssociation,
    Star,
    DoubleStar,
    Unknown,
    Duplicate,
    NonExistent
}

public static class ObjectTypeExtensions
{
    public static string ToDisplayString(this ObjectType type) => type switch
    {
        ObjectType.Galaxy           => "Galaxy",
        ObjectType.GalaxyPair       => "Galaxy Pair",
        ObjectType.GalaxyTriplet    => "Galaxy Triple",
        ObjectType.GalaxyGroup      => "Galaxy Group",
        ObjectType.OpenCluster      => "Open Cluster",
        ObjectType.GlobularCluster  => "Globular Cluster",
        ObjectType.ClusterNebula    => "Cluster + Nebula",
        ObjectType.PlanetaryNebula  => "Planetary Nebula",
        ObjectType.EmissionNebula   => "Emission Nebula",
        ObjectType.ReflectionNebula => "Reflection Nebula",
        ObjectType.SupernovaRemnant => "Supernova Remnant",
        ObjectType.HiiRegion        => "HII Region",
        ObjectType.DarkNebula       => "Dark Nebula",
        ObjectType.BrightNebula     => "Bright Nebula",
        ObjectType.Nebula           => "Nebula",
        ObjectType.StellarAssociation => "Stellar Assoc.",
        ObjectType.Star             => "Star",
        ObjectType.DoubleStar       => "Double Star",
        _                           => "Unknown"
    };

    public static string ToShortString(this ObjectType type) => type switch
    {
        ObjectType.Galaxy           => "Gx",
        ObjectType.GalaxyPair       => "GPair",
        ObjectType.GalaxyTriplet    => "GTrpl",
        ObjectType.GalaxyGroup      => "GGrp",
        ObjectType.OpenCluster      => "OC",
        ObjectType.GlobularCluster  => "GC",
        ObjectType.ClusterNebula    => "Cl+N",
        ObjectType.PlanetaryNebula  => "PN",
        ObjectType.EmissionNebula   => "EN",
        ObjectType.ReflectionNebula => "RN",
        ObjectType.SupernovaRemnant => "SNR",
        ObjectType.HiiRegion        => "HII",
        ObjectType.DarkNebula       => "DN",
        ObjectType.BrightNebula     => "BN",
        ObjectType.Nebula           => "Neb",
        ObjectType.StellarAssociation => "*Ass",
        ObjectType.Star             => "*",
        ObjectType.DoubleStar       => "**",
        _                           => "?"
    };

    public static ObjectType FromOpenNgcCode(string code) => code.Trim() switch
    {
        "G" or "Gx"     => ObjectType.Galaxy,
        "GPair"         => ObjectType.GalaxyPair,
        "GTrpl"         => ObjectType.GalaxyTriplet,
        "GGroup"        => ObjectType.GalaxyGroup,
        "OC" or "OCl"   => ObjectType.OpenCluster,
        "GC" or "GCl"   => ObjectType.GlobularCluster,
        "Cl+N"          => ObjectType.ClusterNebula,
        "PN"            => ObjectType.PlanetaryNebula,
        "EN" or "EmN"   => ObjectType.EmissionNebula,
        "RN" or "RfN"   => ObjectType.ReflectionNebula,
        "SNR"           => ObjectType.SupernovaRemnant,
        "HII"           => ObjectType.HiiRegion,
        "DN"            => ObjectType.DarkNebula,
        "BN"            => ObjectType.BrightNebula,
        "Neb"           => ObjectType.Nebula,
        "*Ass"          => ObjectType.StellarAssociation,
        "*"             => ObjectType.Star,
        "**"            => ObjectType.DoubleStar,
        "Dup"           => ObjectType.Duplicate,
        "NonEx"         => ObjectType.NonExistent,
        _               => ObjectType.Unknown
    };

    public static bool IsDeepSky(this ObjectType type) => type switch
    {
        ObjectType.Duplicate or ObjectType.NonExistent or ObjectType.Unknown => false,
        _ => true
    };
}
