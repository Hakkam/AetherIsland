using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AetherIsland.Integrations.Weather;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    // Cache a task per coordinate for 30 minutes. The UI redraws once per minute;
    // that redraw must not cause a fresh network request.
    private Task<WeatherData?>? _weatherTask;
    private string _weatherKey = "";
    private DateTime _weatherLastRequest = DateTime.MinValue;

    private Task<WeatherData?> FetchWeather(double latitude, double longitude, string label, bool force = false)
    {
        var lat = latitude.ToString("0.00000", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("0.00000", CultureInfo.InvariantCulture);
        var key = lat + "_" + lon;
        if (!force && key == _weatherKey && _weatherTask != null &&
            DateTime.UtcNow - _weatherLastRequest < TimeSpan.FromMinutes(30))
            return _weatherTask;
        _weatherKey = key;
        _weatherLastRequest = DateTime.UtcNow;
        // Separate cache directories prevent offline weather from a previous
        // location being incorrectly relabeled as the newly selected city.
        var service = new OpenMeteoService(cacheDir:
            System.IO.Path.Combine(PortableData.Root, "cache", key));
        return _weatherTask = service.GetAsync(latitude, longitude, label);
    }

    private UIElement RenderDashboard()
    {
        var layout = new StackPanel { Orientation = Orientation.Horizontal };

        var weatherText = Ui.Text("Location not set", 26, true);
        var weatherDescription = Ui.Text("Configure a city for live weather", 10, false, Ui.Dim);
        var locationName = string.IsNullOrWhiteSpace(PortableData.Settings.WeatherLocation)
            ? "SET WEATHER LOCATION" : PortableData.Settings.WeatherLocation.ToUpperInvariant();
        var weather = Ui.V(weatherText, weatherDescription,
            Ui.Button("Weather settings", () => Navigate("weather")));
        _ = LoadWeather(weatherText, weatherDescription);
        layout.Children.Add(Ui.Group("◉  " + locationName, weather, 226));

        var toggles = new WrapPanel { Width = 176 };
        foreach (var (glyph, name, uri) in new[]
        {
            ("\uE701", "Wi-Fi settings", "ms-settings:network-wifi"),
            ("\uE702", "Bluetooth settings", "ms-settings:bluetooth"),
            ("\uE767", "Sound settings", "ms-settings:sound"),
            ("\uE708", "Display settings", "ms-settings:display"),
            ("\uE74F", "Night light", "ms-settings:nightlight"),
            ("\uE8B2", "Battery settings", "ms-settings:batterysaver")
        })
        {
            var button = Iconography.Button(name switch
            {
                "Wi-Fi settings" => "network", "Bluetooth settings" => "bluetooth",
                "Sound settings" => "volume", "Display settings" => "screen",
                "Night light" => "weather", _ => "settings"
            }, name, 51);
            button.Click += (_, _) => OpenUrl(uri);
            button.Width = 51;
            button.Height = 46;
            toggles.Children.Add(button);
        }
        layout.Children.Add(Ui.Group("Quick system actions", toggles, 214));

        var apps = new WrapPanel { Width = 192 };
        foreach (var (icon, label, target) in new[]
        {
            ("\uE8B7", "Explorer", "explorer.exe"),
            ("\uE896", "Downloads", "shell:Downloads"),
            ("\uE8EF", "Calculator", "calc.exe"),
            ("\uE70B", "Notepad", "notepad.exe"),
            ("\uE713", "Settings", "ms-settings:"),
            ("\uE7F8", "Task mgr", "taskmgr.exe")
        })
        {
            var button = Iconography.Button(label switch
            {
                "Explorer" => "shelf", "Downloads" => "download",
                "Calculator" => "dashboard", "Notepad" => "notes",
                "Settings" => "settings", _ => "screen"
            }, label, 58);
            button.Click += (_, _) => OpenUrl(target);
            button.Width = 58;
            button.Height = 48;
            apps.Children.Add(button);
        }
        layout.Children.Add(Ui.Group("Apps", apps, 218));

        var now = DateTime.Now;
        var start = now.Date.AddHours(PortableData.Settings.WakeHour);
        var end = now.Date.AddHours(PortableData.Settings.BedHour);
        if (end <= start) end = end.AddDays(1);
        var fraction = Math.Clamp((now - start).TotalSeconds / (end - start).TotalSeconds, 0, 1);
        var remaining = end - now;
        var day = Ui.V(Ui.Text(fraction.ToString("P0"), 35, true), Ui.Bar(fraction, 173),
            new Border { Height = 12 },
            Ui.Text(remaining.TotalSeconds > 0
                ? $"{Math.Floor(remaining.TotalHours)}h {remaining.Minutes}m until bedtime"
                : "Daily schedule complete", 10, false, Ui.Dim),
            Ui.Text($"Wake {PortableData.Settings.WakeHour:00}:00  ·  Bed {PortableData.Settings.BedHour:00}:00",
                10, false, Ui.Dim));
        layout.Children.Add(Ui.Group("Day progress", day, 209, true));
        return Ui.WrapPage(layout);
    }

    private async Task LoadWeather(TextBlock temp, TextBlock description)
    {
        var st = PortableData.Settings;
        if (st.WeatherLatitude is null || st.WeatherLongitude is null) return;
        temp.Text = "…";
        description.Text = "Fetching official forecast";
        try
        {
            var data = await FetchWeather(st.WeatherLatitude.Value,
                st.WeatherLongitude.Value, st.WeatherLocation);
            if (data is null)
            {
                temp.Text = "—"; description.Text = "Weather unavailable / offline"; return;
            }
            temp.Text = $"{data.TempC:0}°";
            description.Text = $"{data.LocationName} · {WeatherCodes.Describe(data.Code, data.IsDay).Desc + (data.FromCache ? " · cached/offline" : "")}";
        }
        catch (Exception ex)
        {
            PortableData.Log("WEATHER", ex.Message);
            temp.Text = "—"; description.Text = "Live weather unavailable";
        }
    }

    private UIElement RenderWeather()
    {
        var city = Ui.Input(PortableData.Settings.WeatherLocation, 240);
        var status = Ui.Text("Choose a city to fetch verified weather.", 11, false, Ui.Dim);
        var forecast = new StackPanel();
        var v = Ui.V(Ui.Title("Weather · Open-Meteo"),
            new Border { Height = 8 },
            Ui.H(city, Ui.Button("Find location", async () =>
            {
                if (string.IsNullOrWhiteSpace(city.Text)) { status.Text = "Enter a city name."; return; }
                status.Text = "Searching…";
                try
                {
                    var results = await OpenMeteoService.GeocodeAsync(city.Text.Trim());
                    if (results.Count == 0) { status.Text = "No matching city found."; return; }
                    var first = results[0];
                    PortableData.Settings.WeatherLatitude = first.Latitude;
                    PortableData.Settings.WeatherLongitude = first.Longitude;
                    PortableData.Settings.WeatherLocation = first.Name + ", " + first.Country;
                    PortableData.SaveSettings();
                    status.Text = PortableData.Settings.WeatherLocation;
                    await RefreshForecast();
                }
                catch (Exception ex) { PortableData.Log("WEATHER", ex.Message); status.Text = "Lookup failed."; }
            })),
            new Border { Height = 9 }, status,
            new Border { Height = 8 }, forecast);
        async Task RefreshForecast()
        {
            forecast.Children.Clear();
            var s = PortableData.Settings;
            if (s.WeatherLatitude is null || s.WeatherLongitude is null) return;
            try
            {
                var current = await FetchWeather(s.WeatherLatitude.Value, s.WeatherLongitude.Value, s.WeatherLocation, force: true);
                if (current is null) { forecast.Children.Add(Ui.Text("Weather is offline.", 11)); return; }
                forecast.Children.Add(Ui.Text($"{current.TempC:0}°C  ·  {WeatherCodes.Describe(current.Code, current.IsDay).Desc + (current.FromCache ? " · cached/offline" : "")}",
                    20, true));
                var cards = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var point in current.Daily.Take(5))
                {
                    var block = Ui.V(Ui.Text(point.Date.ToString("ddd"), 10, true),
                        Ui.Text($"{point.MaxC:0}° / {point.MinC:0}°", 11));
                    block.Margin = new Thickness(6, 6, 20, 0);
                    cards.Children.Add(block);
                }
                forecast.Children.Add(cards);
            }
            catch (Exception ex) { PortableData.Log("WEATHER", ex.Message); forecast.Children.Add(Ui.Text("Forecast unavailable.")); }
        }
        _ = RefreshForecast();
        return Ui.WrapPage(v);
    }

    private UIElement RenderNetwork()
    {
        var status = Ui.Text("Reading Windows network status…", 12);
        var wifi = new StackPanel();
        var v = Ui.V(Ui.Title("Wi-Fi & Bluetooth · Windows state"),
            new Border { Height = 9 }, status,
            new Border { Height = 8 },
            Ui.H(Ui.Button("Wi-Fi settings", () => OpenUrl("ms-settings:network-wifi")),
                 Ui.Button("Bluetooth settings", () => OpenUrl("ms-settings:bluetooth")),
                 Ui.Button("Refresh", () => _ = Refresh())),
            new Border { Height = 6 }, wifi);
        async Task Refresh()
        {
            status.Text = "Scanning…";
            try
            {
                var scan = await Task.Run(() => AetherIsland.Windows.WlanApi.GetAvailableNetworks());
                wifi.Children.Clear();
                status.Text = scan.Any(n => n.Connected) ? "Connected to " +
                    scan.First(n => n.Connected).Ssid : "Wi-Fi not connected";
                foreach (var n in scan.Take(8))
                    wifi.Children.Add(Ui.Text($"{n.Ssid}  ·  Signal {n.SignalPercent}%  ·  " +
                        (n.Secured ? "Secured" : "Open"), 11, false, Ui.Dim));
            }
            catch (Exception ex) { PortableData.Log("NETWORK", ex.Message); status.Text = "Wireless adapter unavailable"; }
        }
        _ = Refresh();
        return Ui.WrapPage(v);
    }
}
