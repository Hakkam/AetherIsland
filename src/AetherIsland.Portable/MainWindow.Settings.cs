using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static UIElement RenderSettings()
    {
        var s = PortableData.Settings;

        var page = new StackPanel { Margin = new Thickness(10, 6, 10, 4) };

        // ---- title strip ----
        var title = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 6) };
        title.Children.Add(Ui.Text("Settings", 14, true));
        title.Children.Add(Ui.Spacer(7));
        var badge = new System.Windows.Controls.Border
        {
            Background = Theme.Chip, CornerRadius = new CornerRadius(8),
            Padding = new Thickness(7, 2, 7, 2), VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = Ui.Text("v" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "2.1"), 8.5, false, Theme.Dim);
        title.Children.Add(badge);
        title.Children.Add(Ui.Spacer(6));
        title.Children.Add(Ui.Text("·  everything stays inside the portable Data\\ folder", 8.8, false, Theme.Faint));
        page.Children.Add(title);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel();
        var right = new StackPanel();
        grid.Children.Add(left);
        grid.Children.Add(right);
        Grid.SetColumn(right, 2);
        page.Children.Add(grid);

        // ============================== LEFT ==============================

        // ---- appearance card ----
        var appearance = SettingsCard("settings", "Appearance", "Theme, accent and motion");
        appearance.Body.Children.Add(Ui.H(Ui.Text("Theme", 10, false, Theme.Dim), Ui.Spacer(10),
            Ui.Segmented(new[] { "Dark", "Light", "System" }, s.Theme switch { "dark" => 0, "light" => 1, _ => 2 }, i =>
            {
                s.Theme = i switch { 0 => "dark", 1 => "light", _ => "system" };
                PortableData.SaveSettings();
                Win.ThemeChanged();
            })));
        appearance.Body.Children.Add(Ui.Spacer(0, 8));

        var accentRow = new StackPanel { Orientation = Orientation.Horizontal };
        accentRow.Children.Add(Ui.Text("Accent", 10, false, Theme.Dim));
        accentRow.Children.Add(Ui.Spacer(4));
        foreach (var hex in new[] { "#33A6F0", "#B48CF2", "#36C797", "#F2A33C", "#FF6878", "#3BB3C3" })
        {
            var captured = hex;
            var selected = string.Equals(s.Accent, captured, StringComparison.OrdinalIgnoreCase);
            var swatch = new System.Windows.Controls.Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                Background = Ui.Solid(captured),
                BorderThickness = new Thickness(selected ? 2 : 0),
                BorderBrush = Theme.Text,
                Margin = new Thickness(4, 0, 4, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = selected ? "Current accent" : "Accent " + captured
            };
            swatch.MouseLeftButtonDown += (_, _) =>
            {
                s.Accent = captured;
                PortableData.SaveSettings();
                Win.ThemeChanged();
            };
            accentRow.Children.Add(swatch);
        }
        appearance.Body.Children.Add(accentRow);
        appearance.Body.Children.Add(Ui.Spacer(0, 8));

        var motionRow = Ui.H(
            Ui.Text("Reduced motion", 10.5),
            Ui.Spacer(8),
            new Ui.Switch(s.ReducedMotion, on => { s.ReducedMotion = on; PortableData.SaveSettings(); },
                "Fewer animations"));
        motionRow.VerticalAlignment = VerticalAlignment.Center;
        appearance.Body.Children.Add(motionRow);
        left.Children.Add(appearance.Card);
        left.Children.Add(Ui.Spacer(0, 10));

        // ---- day schedule card ----
        var schedule = SettingsCard("clock", "Day schedule", "Drives the dashboard progress bar");
        schedule.Body.Children.Add(BuildStepper("Wake", s.WakeHour, v =>
        {
            s.WakeHour = ((v % 24) + 24) % 24;
            PortableData.SaveSettings();
            MainWindow.ReRenderIfVisible("dashboard");
        }));
        schedule.Body.Children.Add(Ui.Spacer(0, 8));
        schedule.Body.Children.Add(BuildStepper("Bed", s.BedHour, v =>
        {
            s.BedHour = ((v % 24) + 24) % 24;
            PortableData.SaveSettings();
            MainWindow.ReRenderIfVisible("dashboard");
        }));
        schedule.Body.Children.Add(Ui.Spacer(0, 8));
        schedule.Body.Children.Add(Ui.H(Ui.Text("Weather unit", 10, false, Theme.Dim), Ui.Spacer(10),
            Ui.Segmented(new[] { "°C", "°F" }, s.WeatherUnit == "c" ? 0 : 1, i =>
            {
                s.WeatherUnit = i == 0 ? "c" : "f";
                PortableData.SaveSettings();
                WeatherModule.ResetCache();
                MainWindow.ReRenderIfVisible("weather");
                MainWindow.ReRenderIfVisible("dashboard");
            })));
        left.Children.Add(schedule.Card);
        left.Children.Add(Ui.Spacer(0, 10));

        // ---- privacy card ----
        var privacy = SettingsCard("screen", "Privacy", "On-device only, nothing leaves this PC");
        privacy.Body.Children.Add(BuildSwitchRow("Screen-time sampling",
            "App names + seconds, never titles or URLs", s.ScreenTimeConsent, on =>
            {
                s.ScreenTimeConsent = on;
                PortableData.SaveSettings();
                if (on) Win.Activity.Start(); else Win.Activity.Pause();
            }));
        privacy.Body.Children.Add(Ui.Spacer(0, 6));
        privacy.Body.Children.Add(Ui.H(
            Ui.TextButton("Delete screen-time history", () =>
            {
                Win.Activity.Clear();
                App.NotifyBalloon("Screen time", "All locally stored history deleted.");
            }, "Erase local data", 9.5, background: Brushes.Transparent, foreground: Theme.Bad)));
        privacy.Body.Children.Add(Ui.Spacer(0, 6));
        privacy.Body.Children.Add(BuildSwitchRow("Wellbeing reminders",
            $"Eye break every {s.EyeBreakMinutes} min · movement every {s.StandMinutes} min",
            s.WellbeingEnabled, on => { s.WellbeingEnabled = on; PortableData.SaveSettings(); }));
        left.Children.Add(privacy.Card);

        // ============================== RIGHT ==============================

        // ---- behavior card ----
        var behavior = SettingsCard("sliders", "Behavior", "Startup, shortcuts and window");
        behavior.Body.Children.Add(BuildSwitchRow("Launch at Windows startup",
            "Standard per-user startup entry, self-healing", s.LaunchAtStartup, on =>
            {
                s.LaunchAtStartup = on;
                PortableData.SaveSettings();
                StartupManager.Apply(on);
            }));
        behavior.Body.Children.Add(Ui.Spacer(0, 6));
        behavior.Body.Children.Add(BuildSwitchRow("Ctrl+Alt+Space toggles the island",
            "Global hotkey, works over any app", s.HotkeyEnabled, on =>
            {
                s.HotkeyEnabled = on;
                PortableData.SaveSettings();
                App.NotifyBalloon("Hotkey", on
                    ? "Ctrl+Alt+Space enabled — takes effect on next launch."
                    : "Ctrl+Alt+Space disabled — takes effect on next launch.");
            }));
        behavior.Body.Children.Add(Ui.Spacer(0, 8));
        behavior.Body.Children.Add(Ui.H(
            Ui.TextButton("Pin panel", () => { s.Pinned = true; PortableData.SaveSettings(); Win.PinStateChanged(); },
                "Keep expanded until unpinned", 9.5, background: Theme.Chip, foreground: Theme.Text),
            Ui.TextButton("Reset position", () => Win.ResetPositionPublic(), "Back to top center", 9.5,
                background: Theme.Chip, foreground: Theme.Text)));
        right.Children.Add(behavior.Card);
        right.Children.Add(Ui.Spacer(0, 10));

        // ---- calendars card ----
        var calendars = SettingsCard("calendar", "Calendars (.ics)", "Public URLs or local files — OAuth is not supported");
        if (s.CalendarSources.Count == 0)
            calendars.Body.Children.Add(Ui.Text("No calendar sources yet. The Media tab shows an empty month until you add one.", 9.5, false, Theme.Dim, wrap: true));
        foreach (var source in s.CalendarSources.Take(3))
        {
            var captured = source;
            var row = Ui.H(
                Iconography.Draw("open", 11, 1.9, Theme.Faint),
                Ui.Text("  " + source, 9.3, false, Theme.Dim),
                Iconography.IconButton("close", "Remove source", 22, () =>
                {
                    s.CalendarSources.Remove(captured);
                    PortableData.SaveSettings();
                    MainWindow.ReRenderIfVisible("media");
                }, 11));
            row.Margin = new Thickness(0, 2, 0, 2);
            calendars.Body.Children.Add(row);
        }
        var icsInput = Ui.Input("https://…/calendar.ics or C:\\path\\file.ics", 300);
        calendars.Body.Children.Add(Ui.Spacer(0, 5));
        calendars.Body.Children.Add(Ui.H(icsInput, Ui.TextButton("Add", () =>
        {
            if (string.IsNullOrWhiteSpace(icsInput.Text) || Ui.IsPlaceholder(icsInput)) return;
            s.CalendarSources.Add(icsInput.Text.Trim());
            PortableData.SaveSettings();
            MainWindow.ReRenderIfVisible("settings");
            MainWindow.ReRenderIfVisible("media");
        }, "Add calendar source", 9.5, background: Theme.Accent, foreground: Ui.Solid("#0B0E12"))));
        right.Children.Add(calendars.Card);
        right.Children.Add(Ui.Spacer(0, 10));

        // ---- about card ----
        var about = SettingsCard("ai", "About & diagnostics", "Local-first · no telemetry");
        about.Body.Children.Add(Ui.Text(
            "Aether Island Portable · all data in Data\\ · API keys use Windows DPAPI (bound to this Windows account).",
            9.3, false, Theme.Dim, wrap: true));
        about.Body.Children.Add(Ui.Spacer(0, 7));
        about.Body.Children.Add(Ui.H(
            Ui.TextButton("Open log folder", () => External.Open(PortableData.Root + "\\logs"), "Diagnostics", 9.5,
                background: Theme.Chip, foreground: Theme.Text),
            Ui.TextButton("Open data folder", () => External.Open(PortableData.Root), "Local data", 9.5,
                background: Theme.Chip, foreground: Theme.Text),
            Ui.TextButton("Reset settings", () =>
            {
                if (MessageBox.Show("Reset all Aether Island settings to defaults? Notes, tasks and shelf files are kept.",
                        "Aether Island", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                PortableData.ReplaceSettings(new PortableSettings());
                Theme.Apply("dark");
                Win.ThemeChanged();
                MainWindow.ReRenderIfVisible("settings");
            }, "Restore defaults", 9.5, background: Theme.Chip, foreground: Theme.Bad)));
        right.Children.Add(about.Card);

        return Ui.WrapPage(page);
    }

    /// <summary>Rounded section card with icon-chip header and body stack.</summary>
    private static (Border Card, StackPanel Body) SettingsCard(string icon, string title, string subtitle)
    {
        var card = new System.Windows.Controls.Border
        {
            Background = Theme.SectionCard,
            BorderBrush = Theme.CardEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11, 9, 11, 10)
        };
        var body = new StackPanel();

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
        var chip = new System.Windows.Controls.Border
        {
            Width = 22, Height = 22, CornerRadius = new CornerRadius(7),
            Background = Theme.Chip, VerticalAlignment = VerticalAlignment.Center
        };
        chip.Child = Iconography.Draw(icon, 12, 1.9, Theme.Accent);
        head.Children.Add(chip);
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        titles.Children.Add(Ui.Text(title, 11, true));
        titles.Children.Add(Ui.Text(subtitle, 8.3, false, Theme.Faint));
        head.Children.Add(titles);
        body.Children.Add(head);
        card.Child = body;
        return (card, body);
    }

    /// <summary>Labeled − value + stepper for hour values.</summary>
    private static FrameworkElement BuildStepper(string label, int value, Action<int> onChanged)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Ui.Text(label, 10, false, Theme.Dim));
        row.Children.Add(Ui.Spacer(10));
        var display = Ui.Text(value.ToString("00"), 12, true);
        display.Width = 30;
        var minus = Ui.TextButton("−", () => onChanged(value - 1), "One hour earlier", 10,
            background: Theme.Chip, foreground: Theme.Text, padding: 7);
        var plus = Ui.TextButton("+", () => onChanged(value + 1), "One hour later", 10,
            background: Theme.Accent, foreground: Ui.Solid("#0B0E12"), padding: 7);
        row.Children.Add(minus);
        row.Children.Add(display);
        row.Children.Add(plus);
        return row;
    }

    /// <summary>Label + description + pill switch row.</summary>
    private static FrameworkElement BuildSwitchRow(string label, string description, bool isOn, Action<bool> onChanged)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = Ui.V(
            Ui.Text(label, 10.5),
            Ui.Text(description, 8.3, false, Theme.Faint));
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);
        var sw = new Ui.Switch(isOn, onChanged, label);
        sw.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sw, 1);
        grid.Children.Add(sw);
        return grid;
    }
}
