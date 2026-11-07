using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    // ------------------------------------------------------------- Home

    private static UIElement RenderHome()
    {
        var now = DateTime.Now;
        var greeting = now.Hour switch { < 5 => "Late night", < 12 => "Good morning", < 18 => "Good afternoon", _ => "Good evening" };

        var day = Core.Logic.DayProgressCalculator.DayFraction(now, TimeSpan.FromHours(PortableData.Settings.WakeHour), TimeSpan.FromHours(PortableData.Settings.BedHour));
        var remaining = Core.Logic.DayProgressCalculator.RemainingUntil(now, $"{PortableData.Settings.BedHour:00}:00");

        var weatherTemp = Ui.Text("—", 24, true);
        var weatherDesc = Ui.Text("Live weather appears once a location is set", 9.5, false, Theme.Dim);
        _ = WeatherModule.BindCurrentAsync(weatherTemp, weatherDesc);

        var nextTask = (PortableData.Read<List<PortableTask>>("tasks.json") ?? new())
            .Where(t => !t.Done && !t.Trashed).OrderBy(t => t.DueAt ?? DateTime.MaxValue).FirstOrDefault();

        var layout = new StackPanel { Margin = new Thickness(10, 6, 8, 0) };
        layout.Children.Add(Ui.H(
            Ui.Text($"{greeting}.", 17, true),
            Ui.Spacer(8),
            Ui.Text(FormatDate(now), 11, false, Theme.Dim)));
        layout.Children.Add(Ui.Spacer(0, 10));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(BuildHomeCard("clock", "Day", Ui.V(
            Ui.Text(day.ToString("P0"), 22, true),
            Ui.Spacer(0, 6),
            Ui.Bar(day, 168, 4.5),
            Ui.Spacer(0, 6),
            Ui.Text($"{(int)remaining.TotalHours}h {remaining.Minutes:00}m until bedtime", 9.5, false, Theme.Dim))));
        row.Children.Add(BuildHomeCard("weather", "Weather", Ui.V(
            Ui.H(weatherTemp, Ui.Spacer(6), weatherDesc))));
        row.Children.Add(BuildHomeCard("tasks", "Next task", Ui.V(
            Ui.Text(nextTask is null ? "Nothing queued" : nextTask.Title, 13, true, wrap: true),
            Ui.Spacer(0, 6),
            Ui.Text(nextTask?.DueAt is { } due ? $"Due {due:HH:mm}" : "Add tasks in the Tasks module", 9.5, false, Theme.Dim))));
        var actions = Ui.H(
            Ui.RoundIcon("screenshot", "Screenshot", 34, QuickAction.Screenshot),
            Ui.RoundIcon("lock", "Lock Windows", 34, Toggles.LockScreen),
            Ui.RoundIcon("coffee", "Keep awake", 34, () => Win.ToggleKeepAwake()),
            Ui.RoundIcon("settings", "Open Settings", 34, () => Win.Navigate("settings")));
        row.Children.Add(BuildHomeCard("sliders", "Quick actions", actions));
        layout.Children.Add(row);
        return Ui.WrapPage(layout);
    }

    private static FrameworkElement BuildHomeCard(string icon, string title, UIElement content)
    {
        var stack = new StackPanel { Margin = new Thickness(4, 2, 16, 4) };
        stack.Children.Add(Ui.SectionTitle(icon, title));
        stack.Children.Add(Ui.Spacer(0, 8));
        stack.Children.Add(content);
        return stack;
    }

    // -------------------------------------------------------- Dashboard

    private static UIElement RenderDashboard()
    {
        var grid = new Grid { Margin = new Thickness(2, 0, 2, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(216) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(258) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(BuildWeatherCard());
        var toggles = BuildTogglesCard(); Grid.SetColumn(toggles, 1); grid.Children.Add(toggles);
        var apps = BuildAppsCard(); Grid.SetColumn(apps, 2); grid.Children.Add(apps);
        var day = BuildDayCard(); Grid.SetColumn(day, 3); grid.Children.Add(day);

        for (var i = 1; i < 4; i++)
        {
            var line = new System.Windows.Shapes.Rectangle
            {
                Width = 1, Fill = Theme.CardEdge,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(-1, 12, 0, 12)
            };
            grid.Children.Add(line);
            Grid.SetColumn(line, i);
        }
        return Ui.WrapPage(grid);
    }

    private static FrameworkElement BuildWeatherCard()
    {
        var location = string.IsNullOrWhiteSpace(PortableData.Settings.WeatherLocation)
            ? "Set location" : PortableData.Settings.WeatherLocation.ToUpperInvariant();
        var head = Ui.H(
            Iconography.Draw("pin", 10, 2, Theme.Faint),
            Ui.Text(" " + location, 9.5, true, Theme.Faint));
        head.Margin = new Thickness(0, 2, 0, 0);
        head.Cursor = System.Windows.Input.Cursors.Hand;
        head.MouseLeftButtonDown += (_, _) => Win.Navigate("weather");

        var glyph = new ContentControl { Content = Iconography.DrawFilled("weatherfill", 40, Theme.Text), VerticalAlignment = VerticalAlignment.Center };
        var temp = Ui.Text("—", 27, true);
        var condition = Ui.Text(PortableData.Settings.WeatherLatitude is null
            ? "Choose a location for live weather" : "Loading forecast…", 11, true);
        var hilo = Ui.Text("", 9.5, false, Theme.Dim);
        _ = WeatherModule.BindCurrentAsync(temp, condition, hilo, glyph);

        var stack = new StackPanel { Margin = new Thickness(14, 8, 10, 8) };
        stack.Children.Add(head);
        stack.Children.Add(Ui.Spacer(0, 8));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Cursor = System.Windows.Input.Cursors.Hand };
        row.Children.Add(glyph);
        row.Children.Add(Ui.Spacer(8));
        row.Children.Add(temp);
        row.MouseLeftButtonDown += (_, _) => Win.Navigate("weather");
        stack.Children.Add(row);
        stack.Children.Add(Ui.Spacer(0, 2));
        stack.Children.Add(condition);
        stack.Children.Add(hilo);
        var open = Ui.Text("Open weather ›", 9, false, Theme.Faint);
        open.Margin = new Thickness(0, 8, 0, 0);
        open.Cursor = System.Windows.Input.Cursors.Hand;
        open.MouseLeftButtonDown += (_, _) => Win.Navigate("weather");
        stack.Children.Add(open);
        stack.ToolTip = "Weather — click any part for the full forecast";
        return stack;
    }

    private static FrameworkElement BuildTogglesCard()
    {
        var wrap = new WrapPanel { ItemWidth = 51, ItemHeight = 51, Margin = new Thickness(4, 8, 0, 0) };

        var wifi = new Ui.ToggleBox("wifi", "Wi-Fi", false, async on =>
        {
            var (ok, message) = await Toggles.SetRadioAsync("wifi", on);
            if (!ok)
            {
                App.NotifyBalloon("Wi-Fi", message + " — opening Windows settings.");
                External.Open("ms-settings:network-wifi");
            }
            ReRenderIfVisible("dashboard");
        });
        var bluetooth = new Ui.ToggleBox("bluetooth", "Bluetooth", false, async on =>
        {
            var (ok, message) = await Toggles.SetRadioAsync("bluetooth", on);
            if (!ok)
            {
                App.NotifyBalloon("Bluetooth", message + " — opening Windows settings.");
                External.Open("ms-settings:bluetooth");
            }
            ReRenderIfVisible("dashboard");
        });
        // Reflect the real radio state asynchronously; unavailable radios become disabled.
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var (wifiAvail, wifiOn) = Toggles.GetRadioState("wifi");
            var (btAvail, btOn) = Toggles.GetRadioState("bluetooth");
            _ = wifi.Dispatcher.BeginInvoke(() =>
            {
                if (!wifiAvail) wifi.SetUnavailable("No Wi-Fi radio found — open Windows settings");
                else wifi.IsOn = wifiOn;
            });
            _ = bluetooth.Dispatcher.BeginInvoke(() =>
            {
                if (!btAvail) bluetooth.SetUnavailable("No Bluetooth radio found — open Windows settings");
                else bluetooth.IsOn = btOn;
            });
        });

        var coffee = new Ui.ToggleBox("coffee", "Keep awake", Toggles.KeepAwakeOn, on =>
        {
            Toggles.SetKeepAwake(on);
            Win.RefreshAwakeButton();
            Win.UpdatePillStatus();
        }, onBackground: Theme.Chip, onForeground: Theme.Orange);

        var moon = new Ui.ToggleBox("moon", "Dark mode", Toggles.IsDarkMode(), on =>
        {
            if (Toggles.SetDarkMode(on))
            {
                PortableData.Settings.Theme = on ? "dark" : "light";
                PortableData.SaveSettings();
                Win.ThemeChanged();
            }
        });

        var micState = Toggles.MicrophoneState();
        var mic = new Ui.ToggleBox("mic", micState.available ? "Microphone mute" : "Microphone unavailable",
            micState.available && micState.muted, on =>
        {
            if (!Toggles.SetMicrophoneMute(on)) App.NotifyBalloon("Microphone", "Windows refused the mute change.");
        });
        if (!micState.available) mic.SetUnavailable("No microphone endpoint found");

        var spkState = Toggles.SpeakerState();
        var volume = new Ui.ToggleBox("volume", spkState.available ? "Mute speakers" : "Audio unavailable",
            spkState.available && spkState.muted, on =>
        {
            if (!Toggles.SetSpeakerMute(on)) App.NotifyBalloon("Speakers", "Windows refused the mute change.");
        });
        if (!spkState.available) volume.SetUnavailable("No speaker endpoint found");

        var screenshot = new Ui.ToggleBox("screenshot", "Screenshot → clipboard", false,
            _ => QuickAction.Screenshot()) { Momentary = true };
        var lockBtn = new Ui.ToggleBox("lock", "Lock Windows", false, _ => Toggles.LockScreen()) { Momentary = true };

        foreach (var b in new[] { wifi, bluetooth, coffee, moon, mic, volume, screenshot, lockBtn })
            wrap.Children.Add(b);

        var stack = new StackPanel { Margin = new Thickness(12, 6, 4, 8) };
        stack.Children.Add(Ui.SectionTitle("sliders", "Quick toggles"));
        stack.Children.Add(wrap);
        return stack;
    }

    private static FrameworkElement BuildAppsCard()
    {
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var wrap = new WrapPanel { ItemWidth = 78, ItemHeight = 57, Margin = new Thickness(2, 8, 0, 0) };
        foreach (var (label, fullName, iconKey, target, iconPath, tint) in new[]
        {
            ("File Explr…", "File Explorer", "folder",  "shell:Downloads", (string?)Path.Combine(windir, "explorer.exe"), "#F6C453"),
            ("Downloa…",   "Downloads",     "download","shell:Downloads", (string?)null,                                   "#3F76C9"),
            ("Calculator", "Calculator",    "calculator","calc.exe",      (string?)Path.Combine(windir, "System32", "calc.exe"), "#8A94A6"),
            ("Notepad",    "Notepad",       "notes",   "notepad.exe",     (string?)Path.Combine(windir, "System32", "notepad.exe"), "#7BD3F7"),
            ("Task Mgr",   "Task Manager",  "activity","taskmgr.exe",     (string?)Path.Combine(windir, "System32", "taskmgr.exe"), "#5B8DEF"),
            ("Settings",   "Windows Settings","settings","ms-settings:",  (string?)null,                                   "#98A1B0"),
        })
        {
            var tile = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2) };
            UIElement iconHost;
            if (iconPath is { Length: > 0 } && File.Exists(iconPath))
                iconHost = AppIcons.Tile(iconPath, 31);
            else
                iconHost = new System.Windows.Controls.Border
                {
                    Width = 31, Height = 31, CornerRadius = new CornerRadius(9),
                    Background = Ui.Solid(tint),
                    Child = Iconography.Draw(iconKey, 17, 1.9, Brushes.White)
                };
            tile.Children.Add(iconHost);
            var caption = Ui.Text(label, 8.2, false, Theme.Dim, TextAlignment.Center);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.Margin = new Thickness(0, 3, 0, 0);
            tile.Children.Add(caption);
            tile.Cursor = System.Windows.Input.Cursors.Hand;
            tile.ToolTip = fullName + " — click to open";
            var launch = target;
            tile.MouseLeftButtonDown += (_, _) => External.Open(launch);
            wrap.Children.Add(tile);
        }
        var stack = new StackPanel { Margin = new Thickness(10, 6, 4, 8) };
        stack.Children.Add(Ui.SectionTitle("folder", "Apps"));
        stack.Children.Add(wrap);
        return stack;
    }

    private static FrameworkElement BuildDayCard()
    {
        var now = DateTime.Now;
        var start = now.Date.AddHours(PortableData.Settings.WakeHour);
        var end = now.Date.AddHours(PortableData.Settings.BedHour);
        if (end <= start) end = end.AddDays(1);
        var fraction = Math.Clamp((now - start).TotalSeconds / (end - start).TotalSeconds, 0, 1);
        var remaining = end - now;

        var stack = new StackPanel { Margin = new Thickness(16, 6, 12, 8) };
        stack.Children.Add(Ui.SectionTitle("clock", "Day"));
        stack.Children.Add(Ui.Spacer(0, 8));
        stack.Children.Add(Ui.Text(Math.Round(fraction * 100) + "%", 26, true));
        stack.Children.Add(Ui.Spacer(0, 8));
        stack.Children.Add(Ui.Bar(fraction, 195, 5));
        stack.Children.Add(Ui.Spacer(0, 8));
        stack.Children.Add(Ui.Text(remaining.TotalSeconds > 0
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m until bedtime" : "Daily schedule complete", 9.5, false, Theme.Dim));
        stack.Children.Add(Ui.Spacer(0, 3));
        stack.Children.Add(Ui.Text(
            $"Wake {PortableData.Settings.WakeHour:00}:00 · Bed {PortableData.Settings.BedHour:00}:00", 9, false, Theme.Faint));
        stack.Cursor = System.Windows.Input.Cursors.Hand;
        stack.ToolTip = "Day progress — click to edit wake/bed hours";
        stack.MouseLeftButtonDown += (_, _) => Win.Navigate("settings");
        return stack;
    }

    internal static void ReRenderIfVisible(string module)
    {
        if (Win.CurrentModule == module) Win.Navigate(module);
    }
}

/// <summary>Launch helpers used by the dashboard and utilities.</summary>
internal static class External
{
    public static void Open(string target)
    {
        try
        {
            if (!TryStart(target)) PortableData.Log("LAUNCH", "failed: " + target);
        }
        catch (Exception ex) { PortableData.Log("LAUNCH", $"{target}: {ex.Message}"); }
    }

    private static bool TryStart(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }
}

internal static class QuickAction
{
    public static void Screenshot()
    {
        var file = Toggles.CaptureScreen();
        App.NotifyBalloon("Screenshot", file is null
            ? "Capture failed — see Data\\logs\\app.log"
            : "Saved to clipboard and Data\\screenshots");
    }
}
