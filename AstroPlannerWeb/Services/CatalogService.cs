using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public class CatalogService
{
    private readonly HttpClient _http;
    private IReadOnlyList<DeepSkyObject>? _catalog;
    private Task? _loadTask;

    public CatalogService(HttpClient http) { _http = http; }

    public Task EnsureLoadedAsync() => _loadTask ??= LoadInternalAsync();

    public bool IsLoaded => _catalog != null;

    public IReadOnlyList<DeepSkyObject> GetAll()
        => _catalog ?? throw new InvalidOperationException("Catalog not loaded. Call EnsureLoadedAsync first.");

    public IEnumerable<DeepSkyObject> Filter(
        string? nameSearch = null,
        ISet<ObjectType>? types = null,
        double? maxMag = null,
        CatalogSource? requiredCatalog = null,
        string? constellation = null)
    {
        return GetAll().Where(obj =>
        {
            if (nameSearch is { Length: > 0 })
            {
                var q = nameSearch.Trim();
                bool hit =
                    obj.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (obj.CommonName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (obj.MessierNumber.HasValue && $"M{obj.MessierNumber}".Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (obj.CaldwellNumber.HasValue && $"C{obj.CaldwellNumber}".Contains(q, StringComparison.OrdinalIgnoreCase));
                if (!hit) return false;
            }

            if (types is { Count: > 0 } && !types.Contains(obj.Type))
                return false;

            if (maxMag.HasValue && obj.DisplayMagnitude > maxMag.Value)
                return false;

            if (requiredCatalog.HasValue && requiredCatalog.Value != CatalogSource.None)
            {
                if ((obj.Catalogs & requiredCatalog.Value) == 0)
                    return false;
            }

            if (constellation is { Length: > 0 } && !constellation.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (!obj.Constellation.Equals(constellation, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        });
    }

    public IReadOnlyList<string> GetConstellations()
    {
        return GetAll()
            .Select(o => o.Constellation)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    private async Task LoadInternalAsync()
    {
        var csvText = await _http.GetStringAsync("data/NGC.csv");
        _catalog = ParseCatalog(csvText);
    }

    private static IReadOnlyList<DeepSkyObject> ParseCatalog(string csvText)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
        };

        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, config);
        var records = csv.GetRecords<NgcRecord>().ToList();

        var objects = new List<DeepSkyObject>(records.Count);

        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.Name)) continue;

            var type = ObjectTypeExtensions.FromOpenNgcCode(r.Type ?? "");
            if (!type.IsDeepSky()) continue;
            if (string.IsNullOrWhiteSpace(r.RA) || string.IsNullOrWhiteSpace(r.Dec)) continue;

            if (!TryParseRa(r.RA, out double raDeg)) continue;
            if (!TryParseDec(r.Dec, out double decDeg)) continue;

            var catalogs = CatalogSource.None;
            if (r.Name.StartsWith("NGC")) catalogs |= CatalogSource.NGC;
            if (r.Name.StartsWith("IC"))  catalogs |= CatalogSource.IC;

            int? messier = null;
            if (!string.IsNullOrWhiteSpace(r.M) && int.TryParse(r.M.Trim(), out int m))
            {
                messier = m;
                catalogs |= CatalogSource.Messier;
            }

            CaldwellMap.TryGetValue(r.Name.Trim(), out int caldwell);
            int? caldwellNum = caldwell > 0 ? caldwell : null;
            if (caldwellNum.HasValue) catalogs |= CatalogSource.Caldwell;

            string? commonName = null;
            if (!string.IsNullOrWhiteSpace(r.CommonNames))
            {
                var parts = r.CommonNames.Split("--", StringSplitOptions.RemoveEmptyEntries);
                commonName = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(commonName)) commonName = null;
            }

            objects.Add(new DeepSkyObject
            {
                Name = r.Name.Trim(),
                Type = type,
                RaDegrees = raDeg,
                DecDegrees = decDeg,
                Constellation = r.Const?.Trim() ?? "",
                MajorAxisArcmin = ParseOptionalDouble(r.MajAx),
                MinorAxisArcmin = ParseOptionalDouble(r.MinAx),
                PositionAngleDeg = ParseOptionalDouble(r.PosAng),
                MagnitudeB = ParseOptionalDouble(r.BMag),
                MagnitudeV = ParseOptionalDouble(r.VMag),
                SurfaceBrightness = ParseOptionalDouble(r.SurfBr),
                HubbleType = string.IsNullOrWhiteSpace(r.Hubble) ? null : r.Hubble.Trim(),
                MessierNumber = messier,
                CaldwellNumber = caldwellNum,
                CommonName = commonName,
                Catalogs = catalogs,
            });
        }

        return objects.AsReadOnly();
    }

    private static bool TryParseRa(string ra, out double degrees)
    {
        degrees = 0;
        var parts = ra.Trim().Split(':');
        if (parts.Length < 3) return false;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double h)) return false;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double mn)) return false;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double s)) return false;
        degrees = (h + mn / 60.0 + s / 3600.0) * 15.0;
        return true;
    }

    private static bool TryParseDec(string dec, out double degrees)
    {
        degrees = 0;
        dec = dec.Trim();
        bool neg = dec.StartsWith('-');
        var clean = dec.TrimStart('+', '-');
        var parts = clean.Split(':');
        if (parts.Length < 3) return false;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return false;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double mn)) return false;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double s)) return false;
        degrees = d + mn / 60.0 + s / 3600.0;
        if (neg) degrees = -degrees;
        return true;
    }

    private static double? ParseOptionalDouble(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private sealed class NgcRecord
    {
        public string? Name        { get; set; }
        public string? Type        { get; set; }
        public string? RA          { get; set; }
        public string? Dec         { get; set; }
        public string? Const       { get; set; }
        public string? MajAx       { get; set; }
        public string? MinAx       { get; set; }
        public string? PosAng      { get; set; }

        [CsvHelper.Configuration.Attributes.Name("B-Mag")]
        public string? BMag        { get; set; }

        [CsvHelper.Configuration.Attributes.Name("V-Mag")]
        public string? VMag        { get; set; }

        public string? SurfBr      { get; set; }
        public string? Hubble      { get; set; }
        public string? M           { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Common names")]
        public string? CommonNames { get; set; }
    }

    private static readonly Dictionary<string, int> CaldwellMap = new()
    {
        { "NGC0188", 1  }, { "NGC0040", 2  }, { "NGC4236", 3  }, { "NGC7023", 4  },
        { "IC0342",  5  }, { "NGC6543", 6  }, { "NGC2403", 7  }, { "NGC0559", 8  },
        { "NGC0663",  10 }, { "NGC7635", 11 }, { "NGC6946", 12 }, { "NGC0457", 13 },
        { "NGC0869",  14 }, { "NGC6826",  15 }, { "NGC7243", 16 }, { "NGC0147", 17 },
        { "NGC0185", 18 }, { "IC5146",   19 }, { "NGC7000", 20 }, { "NGC4449", 21 },
        { "NGC7662", 22 }, { "NGC0891",  23 }, { "NGC1275", 24 }, { "NGC2419", 25 },
        { "NGC4244", 26 }, { "NGC6888",  27 }, { "NGC0752", 28 }, { "NGC5005", 29 },
        { "NGC7331", 30 }, { "IC0405",   31 }, { "NGC4631", 32 }, { "NGC6992", 33 },
        { "NGC6960", 34 }, { "NGC4889",  35 }, { "NGC4559", 36 }, { "NGC6885", 37 },
        { "NGC4565", 38 }, { "NGC2392",  39 }, { "NGC3626", 40 },
        { "NGC7006",  42 }, { "NGC7814", 43 }, { "NGC7479", 44 }, { "NGC5248", 45 },
        { "NGC2261",  46 }, { "NGC6934", 47 }, { "NGC2775", 48 }, { "NGC2237", 49 },
        { "NGC2244",  50 }, { "IC1613",  51 }, { "NGC4697", 52 }, { "NGC3115", 53 },
        { "NGC2506",  54 }, { "NGC7009", 55 }, { "NGC0246", 56 }, { "NGC6822", 57 },
        { "NGC2360",  58 }, { "NGC3242", 59 }, { "NGC4038", 60 }, { "NGC4039", 61 },
        { "NGC0247",  62 }, { "NGC7293", 63 }, { "NGC2362", 64 }, { "NGC0253", 65 },
        { "NGC5694",  66 }, { "NGC1097", 67 }, { "NGC6729", 68 }, { "NGC6302", 69 },
        { "NGC0300",  70 }, { "NGC2477", 71 }, { "NGC0055", 72 }, { "NGC1360", 73 },
        { "NGC3132",  74 }, { "NGC6124", 75 }, { "NGC6231", 76 }, { "NGC5128", 77 },
        { "NGC6541",  78 }, { "NGC3201", 79 }, { "NGC5139", 80 }, { "NGC6352", 81 },
        { "NGC6193",  82 }, { "NGC4945", 83 }, { "NGC5286", 84 }, { "IC2391",  85 },
        { "NGC6397",  86 }, { "NGC1261", 87 }, { "NGC5823", 88 }, { "NGC6087", 89 },
        { "NGC2867",  90 }, { "NGC3532", 91 }, { "NGC3372", 92 }, { "NGC6752", 93 },
        { "NGC4755",  94 }, { "NGC6025", 95 }, { "NGC2516", 96 }, { "NGC3766", 97 },
        { "NGC4609",  98 }, { "IC2944",   100 }, { "NGC6744", 101 }, { "NGC2070", 102 },
        { "NGC3195",  109 },
    };
}
