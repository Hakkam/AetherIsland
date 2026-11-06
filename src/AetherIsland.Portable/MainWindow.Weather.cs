using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AetherIsland.Integrations.Weather;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static UIElement RenderWeather() => WeatherModule.RenderDetail();
}

/// <summary>
/// Weather module: reference-style detail (current, chips, hourly strip,
/// 7-day strip) plus location management. Data comes from Open-Meteo with a
/// per-location 30-minute cache; offline reads are labeled as cached.
/// </summary>
internal static class WeatherModule
{
    private static Task<WeatherData?>? _task;
    private static string _key = "";
    private static DateTime _lastRequest = DateTime.MinValue;

    private static bool IsFahrenheit => PortableData.Settings.WeatherUnit == "f";

    public static Task<WeatherData?> FetchAsync(bool force = false)
    {
        var s = PortableData.Settings;
        if (s.WeatherLatitude is null || s.WeatherLongitude is null) return Task.FromResult<WeatherData?>(null);
        var lat = s.WeatherLatitude.Value.ToString("0.00000", CultureInfo.InvariantCulture);
        var lon = s.WeatherLongitude.Value.ToString("0.00000", CultureInfo.InvariantCulture);
        var key = lat + "_" + lon;
        if (!force && key == _key && _task != null && DateTime.UtcNow - _lastRequest < TimeSpan.FromMinutes(30))
            return _task;
        _key = key;
        _lastRequest = DateTime.UtcNow;
        var service = new OpenMeteoService(cacheDir: Path.Combine(PortableData.Root, "cache", key), imperial: IsFahrenheit);
        return _task = service.GetAsync(s.WeatherLatitude.Value, s.WeatherLongitude.Value, s.WeatherLocation);
    }

    public static double ConvertTemp(double? celsius)
    {
        if (celsius is null) return 0;
        return IsFahrenheit ? Math.Round(celsius.Value * 9 / 5 + 32) : Math.Round(celsius.Value);
    }

    private static string Unit => IsFahrenheit ? "°F" : "°C";

    public static void ResetCache() { _key = ""; _task = null; _lastRequest = DateTime.MinValue; }

    /// <summary>Binds the compact dashboard card to the latest/current weather.</summary>
    public static async Task BindCurrentAsync(TextBlock temp, TextBlock condition, TextBlock? hilo = null, ContentControl? glyph = null)
    {
        var s = PortableData.Settings;
        if (s.WeatherLatitude is null)
        {
            condition.Text = "Choose a location for live weather";
            return;
        }
        temp.Text = "…";
        condition.Text = "Fetching official forecast";
        try
        {
            var data = await FetchAsync();
            if (data is null) { temp.Text = "—"; condition.Text = "Weather unavailable"; return; }
            temp.Text = $"{ConvertTemp(data.TempC)}°";
            var described = WeatherCodes.Describe(data.Code, data.IsDay);
            condition.Text = described.Desc + (data.FromCache ? " · cached/offline" : "");
            if (hilo != null)
            {
                var today = data.Daily.FirstOrDefault();
                hilo.Text = today is null ? "" :
                    $"H {ConvertTemp(today.MaxC)}° · L {ConvertTemp(today.MinC)}° · Feels {ConvertTemp(data.FeelsLikeC)}°";
            }
        }
        catch (Exception ex)
        {
            PortableData.Log("WEATHER", ex.Message);
            temp.Text = "—";
            condition.Text = "Live weather unavailable";
        }
    }

    public static UIElement RenderDetail()
    {
        var s = PortableData.Settings;
        var stack = new StackPanel { Margin = new Thickness(12, 6, 10, 4) };

        // ---- current conditions row ----
        var current = new Grid();
        current.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var glyph = new ContentControl { Content = Iconography.Draw("weather", 44, 2.2, Theme.Text), VerticalAlignment = VerticalAlignment.Center };
        current.Children.Add(glyph);
        var headCol = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var tempRow = Ui.H(Ui.Text("—", 29, true));
        var condition = Ui.Text(s.WeatherLatitude is null ? "No location set" : "Loading…", 11.5, true);
        var location = Ui.Text(s.WeatherLocation is { Length: > 0 } loc ? "📍 " + loc + (s.WeatherAutoLocated ? "  · auto-detected" : "") : "📍 location not set", 9.5, false, Theme.Dim);
        var hilo = Ui.Text("", 9.5, false, Theme.Dim);
        headCol.Children.Add(tempRow);
        headCol.Children.Add(condition);
        headCol.Children.Add(location);
        headCol.Children.Add(hilo);
        current.Children.Add(headCol);
        Grid.SetColumn(headCol, 1);

        var chips = new UniformGrid { Columns = 2, VerticalAlignment = VerticalAlignment.Center };
        var chipHosts = new Dictionary<string, TextBlock>();
        foreach (var (key, label) in new[]
        {
            ("humidity", "Humidity"), ("wind", "Wind"), ("uv", "UV max"),
            ("pressure", "Pressure"), ("sunrise", "Sunrise"), ("sunset", "Sunset")
        })
        {
            var value = Ui.Text("—", 10.5, true);
            chipHosts[key] = value;
            var chip = new System.Windows.Controls.Border
            {
                Background = Theme.Chip,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(3),
                Child = Ui.H(
                    Ui.Text(label + "  ", 8.5, false, Theme.Faint),
                    value)
            };
            chips.Children.Add(chip);
        }
        current.Children.Add(chips);
        Grid.SetColumn(chips, 2);
        stack.Children.Add(current);
        stack.Children.Add(Ui.Spacer(0, 8));

        // ---- hourly strip ----
        var hourly = new UniformGrid { Columns = 7 };
        var hourlySlots = new List<(TextBlock time, ContentControl glyph, TextBlock t, TextBlock p)>();
        for (var i = 0; i < 7; i++)
        {
            var time = Ui.Text("", 8.5, false, Theme.Faint, TextAlignment.Center);
            var g = new ContentControl { Content = Iconography.Draw("weather", 17, 1.8, Theme.Dim), HorizontalAlignment = HorizontalAlignment.Center };
            var t = Ui.Text("", 10.5, true, Theme.Text, TextAlignment.Center);
            var p = Ui.Text("", 8, false, Theme.Accent, TextAlignment.Center);
            hourlySlots.Add((time, g, t, p));
            var cell = Ui.V(time, g, t, p);
            cell.Margin = new Thickness(2, 0, 2, 0);
            hourly.Children.Add(cell);
        }
        var hourlyCard = new System.Windows.Controls.Border
        {
            Background = Theme.Chip, CornerRadius = new CornerRadius(11),
            Padding = new Thickness(8, 7, 8, 6), Child = hourly
        };
        stack.Children.Add(hourlyCard);
        stack.Children.Add(Ui.Spacer(0, 6));

        // ---- 7-day strip ----
        var daily = new UniformGrid { Columns = 7 };
        var dailySlots = new List<(TextBlock day, ContentControl glyph, TextBlock max, TextBlock min)>();
        for (var i = 0; i < 7; i++)
        {
            var day = Ui.Text("", 8.5, false, Theme.Faint, TextAlignment.Center);
            var g = new ContentControl { Content = Iconography.Draw("weather", 17, 1.8, Theme.Dim), HorizontalAlignment = HorizontalAlignment.Center };
            var max = Ui.Text("", 10.5, true, Theme.Text, TextAlignment.Center);
            var min = Ui.Text("", 9, false, Theme.Dim, TextAlignment.Center);
            dailySlots.Add((day, g, max, min));
            var cell = Ui.V(day, g, max, min);
            cell.Margin = new Thickness(2, 0, 2, 0);
            daily.Children.Add(cell);
        }
        var dailyCard = new System.Windows.Controls.Border
        {
            Background = Theme.Chip, CornerRadius = new CornerRadius(11),
            Padding = new Thickness(8, 7, 8, 6), Child = daily
        };
        stack.Children.Add(dailyCard);

        // ---- footer: unit toggle + location management ----
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 8, 0, 0) };
        footer.Children.Add(Ui.TextButton(IsFahrenheit ? "Use °C" : "Use °F", () =>
        {
            PortableData.Settings.WeatherUnit = IsFahrenheit ? "c" : "f";
            PortableData.SaveSettings();
            ResetCache();
            MainWindow.ReRenderIfVisible("weather");
            MainWindow.ReRenderIfVisible("dashboard");
        }, "Temperature unit", 9, background: Theme.Chip, foreground: Theme.Dim));
        footer.Children.Add(Ui.TextButton("Change location", () => OpenLocationEditor(), "Weather location", 9,
            background: Theme.Chip, foreground: Theme.Dim));
        footer.Children.Add(Ui.TextButton("Refresh now", async () =>
        {
            ResetCache();
            await FetchAsync(force: true);
            MainWindow.ReRenderIfVisible("weather");
        }, "Force fetch", 9, background: Theme.Chip, foreground: Theme.Dim));
        stack.Children.Add(footer);
        if (s.WeatherLatitude is null)
        {
            stack.Children.Add(Ui.Spacer(0, 6));
            stack.Children.Add(RenderLocationEditor());
        }

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var data = await FetchAsync(force: true);
                if (data is null) return;
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var described = WeatherCodes.Describe(data.Code, data.IsDay);
                    tempRow.Children.Clear();
                    tempRow.Children.Add(Ui.Text($"{ConvertTemp(data.TempC)}°", 29, true));
                    condition.Text = described.Desc + (data.FromCache ? " · cached/offline" : "");
                    hilo.Text = data.Daily.Count > 0
                        ? $"H {ConvertTemp(data.Daily[0].MaxC)}° · L {ConvertTemp(data.Daily[0].MinC)}° · Feels {ConvertTemp(data.FeelsLikeC)}°"
                        : $"Feels {ConvertTemp(data.FeelsLikeC)}°";
                    chipHosts["humidity"].Text = (data.Humidity?.ToString() ?? "—") + "%";
                    chipHosts["wind"].Text = $"{data.WindKmh:0} km/h";
                    chipHosts["uv"].Text = data.Daily.Count > 0 && data.Daily[0].UvIndex is { } uv ? uv.ToString("0.#") : "—";
                    chipHosts["pressure"].Text = data.PressureHpa is { } hp ? $"{hp:0} hPa" : "—";
                    chipHosts["sunrise"].Text = data.Sunrise?.ToString("HH:mm") ?? "—";
                    chipHosts["sunset"].Text = data.Sunset?.ToString("HH:mm") ?? "—";

                    foreach (var slot in hourlySlots) { slot.time.Text = ""; slot.t.Text = ""; slot.p.Text = ""; }
                    var hours = data.Hourly.Take(7).ToList();
                    for (var i = 0; i < hours.Count; i++)
                    {
                        var h = hours[i];
                        var (time, g, t, p) = hourlySlots[i];
                        time.Text = i == 0 ? "Now" : h.Time.ToString("HH:mm");
                        var code = WeatherCodes.Describe(h.Code, h.Time.Hour is >= 6 and <= 19);
                        t.Text = $"{ConvertTemp(h.TempC)}°";
                        p.Text = h.PrecipProb is > 4 ? $"{h.PrecipProb}%" : "";
                    }
                    for (var i = 0; i < Math.Min(7, data.Daily.Count); i++)
                    {
                        var d = data.Daily[i];
                        var (day, g, max, min) = dailySlots[i];
                        day.Text = i == 0 ? "Today" : d.Date.ToString("ddd");
                        max.Text = $"{ConvertTemp(d.MaxC)}°";
                        min.Text = $"{ConvertTemp(d.MinC)}°";
                    }
                });
            }
            catch (Exception ex)
            {
                PortableData.Log("WEATHER", ex.Message);
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    condition.Text = "Forecast unavailable — offline or service error");
            }
        });

        return Ui.WrapPage(stack);
    }

    public static void OpenLocationEditor()
    {
        MainWindow.ReRenderIfVisible("weather");
    }

    /// <summary>Location search + manual entry; embedded at the bottom of the weather module.</summary>
    public static UIElement RenderLocationEditor()
    {
        var input = Ui.Input("City name — e.g. Jakarta", 240);
        var status = Ui.Text("", 10, false, Theme.Dim, wrap: true);
        var result = Ui.TextButton("Find location", async () =>
        {
            if (string.IsNullOrWhiteSpace(input.Text) || input.Tag is string)
            {
                status.Text = "Enter a city name first.";
                return;
            }
            status.Text = "Searching…";
            try
            {
                var results = await OpenMeteoService.GeocodeAsync(input.Text.Trim());
                if (results.Count == 0) { status.Text = "No matching city found."; return; }
                var first = results[0];
                var s = PortableData.Settings;
                s.WeatherLatitude = first.Latitude;
                s.WeatherLongitude = first.Longitude;
                s.WeatherLocation = first.Name + ", " + first.Country;
                s.WeatherAutoLocated = false;
                PortableData.SaveSettings();
                ResetCache();
                status.Text = "Location set to " + s.WeatherLocation;
                MainWindow.ReRenderIfVisible("weather");
                MainWindow.ReRenderIfVisible("dashboard");
            }
            catch (Exception ex)
            {
                PortableData.Log("WEATHER", ex.Message);
                status.Text = "Lookup failed — check your connection.";
            }
        });
        return Ui.V(Ui.H(input, result), Ui.Spacer(0, 4), status);
    }
}
