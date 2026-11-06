using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static UIElement RenderSettings()
    {
        var s = PortableData.Settings;
        var grid = new Grid { Margin = new Thickness(14, 6, 12, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel();
        var right = new StackPanel();
        grid.Children.Add(left);
        grid.Children.Add(right);
        Grid.SetColumn(right, 1);

        // ---- appearance ----
        left.Children.Add(Ui.SectionTitle("settings", "Appearance"));
        left.Children.Add(Ui.Spacer(0, 5));
        var themeRow = Ui.H(Ui.Text("Theme   ", 10.5, false, Theme.Dim),
            Ui.Segmented(new[] { "Dark", "Light", "System" }, s.Theme switch { "dark" => 0, "light" => 1, _ => 2 }, i =>
            {
                s.Theme = i switch { 0 => "dark", 1 => "light", _ => "system" };
                PortableData.SaveSettings();
                Win.ThemeChanged();
            }));
        left.Children.Add(themeRow);
        left.Children.Add(Ui.Spacer(0, 6));
        var accentRow = Ui.H(Ui.Text("Accent   ", 10.5, false, Theme.Dim));
        foreach (var hex in new[] { "#33A6F0", "#B48CF2", "#36C797", "#F2A33C", "#FF6878" })
        {
            var swatch = new System.Windows.Controls.Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                Background = Ui.Solid(hex), Margin = new Thickness(3, 0, 3, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = hex
            };
            var captured = hex;
            swatch.MouseLeftButtonDown += (_, _) =>
            {
                s.Accent = captured;
                PortableData.SaveSettings();
                Win.ThemeChanged();
            };
            accentRow.Children.Add(swatch);
        }
        left.Children.Add(accentRow);
        left.Children.Add(Ui.Spacer(0, 6));
        var reduced = new CheckBox { Content = "Reduced motion", IsChecked = s.ReducedMotion, Foreground = Theme.Text };
        reduced.Checked += (_, _) => { s.ReducedMotion = true; PortableData.SaveSettings(); };
        reduced.Unchecked += (_, _) => { s.ReducedMotion = false; PortableData.SaveSettings(); };
        left.Children.Add(reduced);

        // ---- schedule ----
        left.Children.Add(Ui.Spacer(0, 10));
        left.Children.Add(Ui.SectionTitle("clock", "Day schedule"));
        left.Children.Add(Ui.Spacer(0, 5));
        var wake = Ui.Input("7", 52, s.WakeHour.ToString());
        var bed = Ui.Input("23", 52, s.BedHour.ToString());
        var scheduleRow = Ui.H(Ui.Text("Wake  ", 10.5, false, Theme.Dim), wake,
            Ui.Text("   Bed  ", 10.5, false, Theme.Dim), bed,
            Ui.TextButton("Save", () =>
            {
                if (int.TryParse(wake.Text, out var w) && w is >= 0 and < 24) s.WakeHour = w;
                if (int.TryParse(bed.Text, out var b) && b is >= 0 and < 24) s.BedHour = b;
                PortableData.SaveSettings();
                MainWindow.ReRenderIfVisible("dashboard");
            }, "Save hours", 9.5, background: Theme.Chip, foreground: Theme.Text));
        scheduleRow.Margin = new Thickness(0, 0, 0, 6);
        left.Children.Add(scheduleRow);

        var unitRow = Ui.H(Ui.Text("Weather unit   ", 10.5, false, Theme.Dim),
            Ui.Segmented(new[] { "°C", "°F" }, s.WeatherUnit == "c" ? 0 : 1, i =>
            {
                s.WeatherUnit = i == 0 ? "c" : "f";
                PortableData.SaveSettings();
                WeatherModule.ResetCache();
                MainWindow.ReRenderIfVisible("weather");
                MainWindow.ReRenderIfVisible("dashboard");
            }));
        left.Children.Add(unitRow);

        // ---- behavior ----
        right.Children.Add(Ui.SectionTitle("sliders", "Behavior"));
        right.Children.Add(Ui.Spacer(0, 5));
        var startup = new CheckBox { Content = "Launch at Windows startup", IsChecked = s.LaunchAtStartup, Foreground = Theme.Text };
        startup.Checked += (_, _) => { s.LaunchAtStartup = true; PortableData.SaveSettings(); StartupManager.Apply(true); };
        startup.Unchecked += (_, _) => { s.LaunchAtStartup = false; PortableData.SaveSettings(); StartupManager.Apply(false); };
        right.Children.Add(startup);
        var hotkey = new CheckBox { Content = "Ctrl+Alt+Space toggles the island", IsChecked = s.HotkeyEnabled, Foreground = Theme.Text };
        hotkey.Checked += (_, _) => s.HotkeyEnabled = true;
        hotkey.Unchecked += (_, _) => s.HotkeyEnabled = false;
        right.Children.Add(hotkey);
        right.Children.Add(Ui.Spacer(0, 4));
        var pinRow = Ui.H(
            Ui.TextButton("Pin panel", () =>
            {
                s.Pinned = true;
                PortableData.SaveSettings();
                Win.PinStateChanged();
            }, "Keep expanded until unpinned", 9.5, background: Theme.Chip, foreground: Theme.Text),
            Ui.TextButton("Reset position", () => Win.ResetPositionPublic(), "Back to top center", 9.5,
                background: Theme.Chip, foreground: Theme.Text));
        right.Children.Add(pinRow);

        // ---- privacy ----
        right.Children.Add(Ui.Spacer(0, 10));
        right.Children.Add(Ui.SectionTitle("screen", "Privacy"));
        right.Children.Add(Ui.Spacer(0, 5));
        var consent = new CheckBox
        {
            Content = "Screen-time sampling (local only, no titles/URLs)",
            IsChecked = s.ScreenTimeConsent,
            Foreground = Theme.Text
        };
        consent.Checked += (_, _) => { s.ScreenTimeConsent = true; PortableData.SaveSettings(); Win.Activity.Start(); };
        consent.Unchecked += (_, _) => { s.ScreenTimeConsent = false; PortableData.SaveSettings(); Win.Activity.Pause(); };
        right.Children.Add(consent);
        right.Children.Add(Ui.TextButton("Delete screen-time history", () =>
        {
            Win.Activity.Clear();
            App.NotifyBalloon("Screen time", "All locally stored history deleted.");
        }, "Erase local data", 9.5, background: Theme.Chip, foreground: Theme.Bad));
        right.Children.Add(Ui.Spacer(0, 4));
        var well = new CheckBox { Content = "Wellbeing reminders", IsChecked = s.WellbeingEnabled, Foreground = Theme.Text };
        well.Checked += (_, _) => { s.WellbeingEnabled = true; PortableData.SaveSettings(); };
        well.Unchecked += (_, _) => { s.WellbeingEnabled = false; PortableData.SaveSettings(); };
        right.Children.Add(well);

        // ---- calendar sources + diagnostics ----
        right.Children.Add(Ui.Spacer(0, 10));
        right.Children.Add(Ui.SectionTitle("calendar", "Calendars (.ics)"));
        right.Children.Add(Ui.Spacer(0, 5));
        if (s.CalendarSources.Count == 0)
            right.Children.Add(Ui.Text("No calendar sources. Add a public .ics URL or file path.", 9.5, false, Theme.Dim, wrap: true));
        foreach (var source in s.CalendarSources.Take(3))
        {
            var captured = source;
            var row = Ui.H(
                Ui.Text("· " + source, 9.3, false, Theme.Dim),
                Iconography.IconButton("close", "Remove source", 22, () =>
                {
                    s.CalendarSources.Remove(captured);
                    PortableData.SaveSettings();
                    MainWindow.ReRenderIfVisible("media");
                }, 11));
            right.Children.Add(row);
        }
        var icsInput = Ui.Input("https://…/calendar.ics or C:\\path\\file.ics", 250);
        right.Children.Add(Ui.H(icsInput, Ui.TextButton("Add", () =>
        {
            if (string.IsNullOrWhiteSpace(icsInput.Text) || Ui.IsPlaceholder(icsInput)) return;
            s.CalendarSources.Add(icsInput.Text.Trim());
            PortableData.SaveSettings();
            MainWindow.ReRenderIfVisible("settings");
            MainWindow.ReRenderIfVisible("media");
        }, "Add calendar source", 9.5, background: Theme.Chip, foreground: Theme.Text)));
        right.Children.Add(Ui.Text("Google/Outlook OAuth is not supported; public ICS links work.", 8.8, false, Theme.Faint, wrap: true));

        // ---- diagnostics footer ----
        left.Children.Add(Ui.Spacer(0, 10));
        left.Children.Add(Ui.SectionTitle("ai", "About & diagnostics"));
        left.Children.Add(Ui.Spacer(0, 5));
        left.Children.Add(Ui.Text("Aether Island Portable V2.1 · all data in the portable Data\\ folder · credentials use Windows DPAPI.", 9.3, false, Theme.Dim, wrap: true));
        left.Children.Add(Ui.Text("No telemetry. No background polling. CPU stays near zero when idle.", 9.3, false, Theme.Faint, wrap: true));
        left.Children.Add(Ui.H(
            Ui.TextButton("Open log folder", () => External.Open(PortableData.Root + "\\logs"), "Diagnostics", 9.5,
                background: Theme.Chip, foreground: Theme.Text),
            Ui.TextButton("Open data folder", () => External.Open(PortableData.Root), "Local data", 9.5,
                background: Theme.Chip, foreground: Theme.Text)));
        return Ui.WrapPage(grid);
    }
}
