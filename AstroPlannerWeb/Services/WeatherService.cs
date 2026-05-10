using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AstroPlannerWeb.Models;

namespace AstroPlannerWeb.Services;

public class WeatherService(HttpClient http)
{
    private readonly Dictionary<string, List<WeatherHour>> _cache = new();

    public async Task<List<WeatherHour>> GetDarkWindowAsync(
        double lat, double lon, DateTime darkStart, DateTime darkEnd)
    {
        if (lat == 0 && lon == 0) return [];

        string key = $"{lat:F2},{lon:F2},{darkStart.ToUniversalTime():yyyyMMdd}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        try
        {
            var d0 = darkStart.ToUniversalTime().Date.ToString("yyyy-MM-dd");
            var d1 = darkEnd.ToUniversalTime().Date.AddDays(1).ToString("yyyy-MM-dd");

            var url = $"https://api.open-meteo.com/v1/forecast" +
                $"?latitude={lat:F4}&longitude={lon:F4}" +
                $"&hourly=cloud_cover,wind_speed_10m,precipitation_probability,relative_humidity_2m,visibility" +
                $"&wind_speed_unit=mph&timezone=UTC" +
                $"&start_date={d0}&end_date={d1}";

            var resp = await http.GetFromJsonAsync<OpenMeteoResponse>(url);
            if (resp?.Hourly?.Time == null) { _cache[key] = []; return []; }

            var h = resp.Hourly;
            var hours = new List<WeatherHour>();
            for (int i = 0; i < h.Time.Length; i++)
            {
                if (!DateTime.TryParse(h.Time[i], null,
                        DateTimeStyles.AssumeUniversal, out var utc))
                    continue;
                utc = utc.ToUniversalTime();
                if (utc < darkStart || utc > darkEnd) continue;
                hours.Add(new WeatherHour
                {
                    UtcTime = utc,
                    CloudCoverPercent = Val(h.CloudCover, i),
                    WindSpeedMph = Val(h.WindSpeed10m, i),
                    PrecipProbabilityPercent = Val(h.PrecipProbability, i),
                    HumidityPercent = Val(h.Humidity, i),
                    VisibilityMiles = Val(h.Visibility, i) is { } vm ? vm / 1609.34 : null,
                });
            }

            _cache[key] = hours;
            return hours;
        }
        catch
        {
            return [];
        }
    }

    private static double? Val(double[]? arr, int i) =>
        arr != null && i < arr.Length ? arr[i] : null;

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("hourly")]
        public OpenMeteoHourly? Hourly { get; set; }
    }

    private sealed class OpenMeteoHourly
    {
        [JsonPropertyName("time")]
        public string[] Time { get; set; } = [];
        [JsonPropertyName("cloud_cover")]
        public double[]? CloudCover { get; set; }
        [JsonPropertyName("wind_speed_10m")]
        public double[]? WindSpeed10m { get; set; }
        [JsonPropertyName("precipitation_probability")]
        public double[]? PrecipProbability { get; set; }
        [JsonPropertyName("relative_humidity_2m")]
        public double[]? Humidity { get; set; }
        [JsonPropertyName("visibility")]
        public double[]? Visibility { get; set; }
    }
}
