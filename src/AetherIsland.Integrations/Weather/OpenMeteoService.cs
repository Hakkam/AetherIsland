using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherIsland.Integrations.Weather;

public sealed class WeatherData
{
    public string LocationName { get; set; } = "";
    public double? TempC { get; set; }
    public double? FeelsLikeC { get; set; }
    public int? Humidity { get; set; }
    public double? WindKmh { get; set; }
    public double? PressureHpa { get; set; }
    public int Code { get; set; }
    public bool IsDay { get; set; } = true;
    public List<HourPoint> Hourly { get; set; } = new();
    public List<DayPoint> Daily { get; set; } = new();
    public DateTime? Sunrise { get; set; }
    public DateTime? Sunset { get; set; }
    public bool FromCache { get; set; }
}

public sealed class HourPoint
{
    public DateTime Time { get; set; }
    public double? TempC { get; set; }
    public int Code { get; set; }
    public int? PrecipProb { get; set; }
}

public sealed class DayPoint
{
    public DateTime Date { get; set; }
    public int Code { get; set; }
    public double? MaxC { get; set; }
    public double? MinC { get; set; }
    public int? PrecipProb { get; set; }
    public double? UvIndex { get; set; }
}

public sealed record GeoResult(string Name, string Country, double Latitude, double Longitude);

/// <summary>
/// Weather from Open-Meteo (open, key-less service, subject to its usage terms).
/// Results are cached to disk so the module still works offline (marked as cached).
/// </summary>
public sealed class OpenMeteoService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly string _cacheDir;
    private readonly bool _imperial;

    public OpenMeteoService(string? cacheDir = null, bool imperial = false)
    {
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherIsland", "cache");
        Directory.CreateDirectory(_cacheDir);
        _imperial = imperial;
    }

    public async Task<WeatherData?> GetAsync(double lat, double lon, string locationName, CancellationToken ct = default)
    {
        var units = _imperial ? "&temperature_unit=fahrenheit&wind_speed_unit=mph" : "";
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  "&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,weather_code,wind_speed_10m,pressure_msl" +
                  "&hourly=temperature_2m,weather_code,precipitation_probability" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max,precipitation_probability_max" +
                  "&timezone=auto&forecast_days=7" + units;

        try
        {
            var json = await Http.GetStringAsync(url, ct);
            var data = Parse(json);
            if (data is null) return null;
            data.LocationName = locationName;
            data.FromCache = false;
            File.WriteAllText(CachePath, json);
            return data;
        }
        catch
        {
            // offline — serve cache marked as stale
            try
            {
                var cached = File.ReadAllText(CachePath);
                var data = Parse(cached);
                if (data is not null) { data.FromCache = true; if (!string.IsNullOrEmpty(locationName)) data.LocationName = locationName; }
                return data;
            }
            catch { return null; }
        }
    }

    private string CachePath => Path.Combine(_cacheDir, "weather.json");

    public static async Task<List<GeoResult>> GeocodeAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=5&language=en";
            var doc = await Http.GetFromJsonAsync<JsonElement>(url, ct);
            var list = new List<GeoResult>();
            foreach (var r in doc.GetProperty("results").EnumerateArray().Take(5))
            {
                list.Add(new GeoResult(
                    r.GetProperty("name").GetString() ?? "",
                    r.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "",
                    r.GetProperty("latitude").GetDouble(),
                    r.GetProperty("longitude").GetDouble()));
            }
            return list;
        }
        catch { return new List<GeoResult>(); }
    }

    private static WeatherData? Parse(string json)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(json, new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString });
            var cur = doc.GetProperty("current");
            var data = new WeatherData
            {
                TempC = cur.GetProperty("temperature_2m").GetDouble(),
                FeelsLikeC = cur.GetProperty("apparent_temperature").GetDouble(),
                Humidity = cur.GetProperty("relative_humidity_2m").GetInt32(),
                WindKmh = cur.GetProperty("wind_speed_10m").GetDouble(),
                PressureHpa = cur.GetProperty("pressure_msl").GetDouble(),
                Code = cur.GetProperty("weather_code").GetInt32(),
                IsDay = cur.GetProperty("is_day").GetInt32() == 1
            };

            if (doc.TryGetProperty("hourly", out var hourly))
            {
                var times = hourly.GetProperty("time").EnumerateArray().Select(e => e.GetString()!).ToList();
                var temps = hourly.GetProperty("temperature_2m");
                var codes = hourly.GetProperty("weather_code");
                var precip = hourly.TryGetProperty("precipitation_probability", out var pp)
                    ? pp.EnumerateArray().ToList() : new List<JsonElement>();
                var now = DateTime.Now;
                for (var i = 0; i < times.Count; i++)
                {
                    var t = DateTime.Parse(times[i]);
                    if (t < now.AddHours(-1)) continue;
                    if (t > now.AddHours(24)) break;
                    data.Hourly.Add(new HourPoint
                    {
                        Time = t,
                        TempC = temps[i].ValueKind == JsonValueKind.Number ? temps[i].GetDouble() : null,
                        Code = codes[i].GetInt32(),
                        PrecipProb = i < precip.Count && precip[i].ValueKind == JsonValueKind.Number ? precip[i].GetInt32() : null
                    });
                }
            }

            if (doc.TryGetProperty("daily", out var daily))
            {
                var times = daily.GetProperty("time").EnumerateArray().Select(e => e.GetString()!).ToList();
                var max = daily.GetProperty("temperature_2m_max");
                var min = daily.GetProperty("temperature_2m_min");
                var codes = daily.GetProperty("weather_code");
                var precip = daily.TryGetProperty("precipitation_probability_max", out var pp)
                    ? pp.EnumerateArray().ToList() : new List<JsonElement>();
                var uv = daily.TryGetProperty("uv_index_max", out var uv2)
                    ? uv2.EnumerateArray().ToList() : new List<JsonElement>();
                var sunriseArr = daily.TryGetProperty("sunrise", out var sr) ? sr.EnumerateArray().Select(e => e.GetString()).ToList() : new();
                var sunsetArr = daily.TryGetProperty("sunset", out var ss) ? ss.EnumerateArray().Select(e => e.GetString()).ToList() : new();

                for (var i = 0; i < times.Count; i++)
                {
                    data.Daily.Add(new DayPoint
                    {
                        Date = DateTime.Parse(times[i]),
                        Code = codes[i].GetInt32(),
                        MaxC = max[i].ValueKind == JsonValueKind.Number ? max[i].GetDouble() : null,
                        MinC = min[i].ValueKind == JsonValueKind.Number ? min[i].GetDouble() : null,
                        PrecipProb = i < precip.Count && precip[i].ValueKind == JsonValueKind.Number ? precip[i].GetInt32() : null,
                        UvIndex = i < uv.Count && uv[i].ValueKind == JsonValueKind.Number ? uv[i].GetDouble() : null
                    });
                    if (i == 0)
                    {
                        if (i < sunriseArr.Count && sunriseArr[i] is not null) data.Sunrise = DateTime.Parse(sunriseArr[i]!);
                        if (i < sunsetArr.Count && sunsetArr[i] is not null) data.Sunset = DateTime.Parse(sunsetArr[i]!);
                    }
                }
            }
            return data;
        }
        catch { return null; }
    }
}

public static class WeatherCodes
{
    /// <summary>Returns (description, Segoe MDL2/Fluent glyph, accent color hex).</summary>
    public static (string Desc, string Glyph, string Color) Describe(int code, bool isDay = true) => code switch
    {
        0 => (isDay ? "Clear sky" : "Clear night", "\uE706", "#FFD35C"),
        1 => ("Mainly clear", "\uE706", "#FFD35C"),
        2 => ("Partly cloudy", "\uE70D", "#9DB7D4"),
        3 => ("Overcast", "\uE9CE", "#8A97A8"),
        45 or 48 => ("Fog", "\uE819", "#8A97A8"),
        51 or 53 or 55 => ("Drizzle", "\uE820", "#5FA8E8"),
        56 or 57 => ("Freezing drizzle", "\uE820", "#7FC3F0"),
        61 => ("Light rain", "\uE820", "#5FA8E8"),
        63 => ("Rain", "\uE820", "#4A96DD"),
        65 => ("Heavy rain", "\uE820", "#3E86C9"),
        66 or 67 => ("Freezing rain", "\uE820", "#7FC3F0"),
        71 => ("Light snow", "\uE718", "#BFE3F5"),
        73 => ("Snow", "\uE718", "#A8D8F0"),
        75 => ("Heavy snow", "\uE718", "#93CBEB"),
        77 => ("Snow grains", "\uE718", "#BFE3F5"),
        80 => ("Rain showers", "\uE820", "#5FA8E8"),
        81 => ("Showers", "\uE820", "#4A96DD"),
        82 => ("Violent showers", "\uE820", "#3E86C9"),
        85 or 86 => ("Snow showers", "\uE718", "#A8D8F0"),
        95 => ("Thunderstorm", "\uE9CE", "#A58BFF"),
        96 or 99 => ("Hailstorm", "\uE9CE", "#A58BFF"),
        _ => ("—", "\uE70D", "#8A97A8")
    };
}
