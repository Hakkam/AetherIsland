using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static int _screenRange; // 0 = today, 1 = 7 days

    private static UIElement RenderScreen()
    {
        if (!PortableData.Settings.ScreenTimeConsent)
            return RenderScreenConsent();

        var tracker = Win.Activity;
        var series = _screenRange == 0 ? tracker.Today() : tracker.Last7Days();
        var total = series.Sum(p => p.Value);
        var (switches, currentApp) = tracker.SwitchInfo();

        var page = new Grid { Margin = new Thickness(8, 2, 6, 0) };
        page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });

        // ---- Left: category donut + legend + switch counter ----
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var categories = ActivityTracker.CategoryTotals(series);
        var ordered = categories.OrderByDescending(p => p.Value).ToList();

        var donut = Ui.Donut(CategorySegments(ordered, total), 118, 13,
            Ui.V(
                Ui.Text(FormatSeconds(total), 16, true, Theme.Text, TextAlignment.Center),
                Ui.Text(_screenRange == 0 ? "today" : "7 days", 8.5, false, Theme.Dim, TextAlignment.Center)));
        var donutRow = new StackPanel { Orientation = Orientation.Horizontal };
        donutRow.Children.Add(donut);
        var legend = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        foreach (var (name, color) in CategoryLegend(ordered))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new System.Windows.Shapes.Ellipse
            { Width = 7, Height = 7, Fill = color, VerticalAlignment = VerticalAlignment.Center });
            var label = Ui.Text("  " + name, 9.5, false, Theme.Dim);
            row.Children.Add(label);
            legend.Children.Add(row);
        }
        donutRow.Children.Add(legend);
        left.Children.Add(donutRow);
        left.Children.Add(Ui.Spacer(0, 8));
        left.Children.Add(Ui.H(
            Iconography.Draw("flip", 11, 1.9, Theme.Faint),
            Ui.Text($"  {switches} app switches" + (currentApp is null ? "" : $" · now: {currentApp}"),
                9.5, false, Theme.Dim)));
        Grid.SetColumn(left, 0);
        page.Children.Add(left);

        // ---- Right: most used ----
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var headRow = new StackPanel { Orientation = Orientation.Horizontal };
        headRow.Children.Add(Ui.SectionTitle("activity", "Most used"));
        var spacer = new Border { Width = 30 };
        headRow.Children.Add(spacer);
        headRow.Children.Add(Ui.Segmented(new[] { "Today", "7 days" }, _screenRange, i =>
        {
            _screenRange = i;
            ReRenderIfVisible("screen");
        }));
        right.Children.Add(headRow);
        right.Children.Add(Ui.Spacer(0, 6));

        if (series.Count == 0)
        {
            right.Children.Add(Ui.Text(
                _screenRange == 0 ? "No activity sampled yet — data appears every ~10 seconds." : "No history yet.",
                10.5, false, Theme.Dim));
        }
        foreach (var (exe, seconds) in series.Take(7))
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(172) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

            row.Children.Add(AppIcons.Tile(exe, 17, preferExeLookup: true));
            var name = Ui.Text(Friendly(exe), 10.2);
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            var bar = Ui.Bar(total == 0 ? 0 : seconds / (double)total, 190, 4);
            bar.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(bar, 2);
            row.Children.Add(bar);
            var time = Ui.Text(FormatSeconds(seconds), 9.5, false, Theme.Dim, TextAlignment.Right);
            Grid.SetColumn(time, 3);
            row.Children.Add(time);
            row.ToolTip = $"{Friendly(exe)} — {FormatSeconds(seconds)}";
            right.Children.Add(row);
        }
        Grid.SetColumn(right, 1);
        page.Children.Add(right);

        var outer = Ui.V(page);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 6, 0, 2) };
        footer.Children.Add(Ui.TextButton("Pause tracking", () =>
        {
            PortableData.Settings.ScreenTimeConsent = false;
            PortableData.SaveSettings();
            Win.Activity.Pause();
            ReRenderIfVisible("screen");
        }, "Pause on-device sampling", 9, background: Brushes.Transparent, foreground: Theme.Faint));
        footer.Children.Add(Ui.TextButton("Delete history", () =>
        {
            Win.Activity.Clear();
            ReRenderIfVisible("screen");
        }, "Delete all locally stored screen-time data", 9, background: Brushes.Transparent, foreground: Theme.Faint));
        outer.Children.Add(footer);
        return Ui.WrapPage(outer);
    }

    private static UIElement RenderScreenConsent()
    {
        var stack = new StackPanel { Margin = new Thickness(14, 10, 10, 6), VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(Ui.SectionTitle("screen", "Screen time · Off by default"));
        stack.Children.Add(Ui.Spacer(0, 10));
        stack.Children.Add(Ui.Text("Only active app names and approximate seconds are recorded, locally, in Data\\screen-time.json.", 11.5));
        stack.Children.Add(Ui.Text("No window titles, keystrokes or browsing history are ever read.", 10.5, false, Theme.Dim));
        stack.Children.Add(Ui.Spacer(0, 12));
        var enable = Ui.TextButton("Enable on-device screen time tracking", () =>
        {
            PortableData.Settings.ScreenTimeConsent = true;
            PortableData.SaveSettings();
            Win.Activity.Start();
            ReRenderIfVisible("screen");
        }, "Consent", 11);
        stack.Children.Add(enable);
        return Ui.WrapPage(stack);
    }

    private static List<(double ratio, Brush brush)> CategorySegments(List<KeyValuePair<string, long>> ordered, long total)
    {
        var palette = new Dictionary<string, Brush>(StringComparer.Ordinal)
        {
            ["browsing"] = Theme.Accent,
            ["productivity"] = Theme.Purple,
            ["communication"] = Theme.Orange,
            ["media"] = Ui.Solid("#DE6FA9"),
            ["system"] = Ui.Solid(Theme.IsDark ? "#8A94A6" : "#7C8698"),
            ["other"] = Ui.Solid(Theme.IsDark ? "#565E6C" : "#B4BCC8"),
        };
        var segments = new List<(double, Brush)>();
        foreach (var (category, seconds) in ordered)
        {
            var key = palette.ContainsKey(category) ? category : "other";
            segments.Add((total == 0 ? 0 : seconds / (double)total, palette[key]));
        }
        return segments;
    }

    private static List<(string name, Brush brush)> CategoryLegend(List<KeyValuePair<string, long>> ordered)
    {
        var labels = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["browsing"] = ("Browsing", "#33A6F0"),
            ["productivity"] = ("Productivity", "#B48CF2"),
            ["communication"] = ("Communication", "#F2A33C"),
            ["media"] = ("Media", "#DE6FA9"),
            ["system"] = ("System", "#8A94A6"),
            ["other"] = ("Other", "#565E6C"),
        };
        var result = new List<(string, Brush)>();
        foreach (var (category, _) in ordered.Take(4))
        {
            if (labels.TryGetValue(category, out var entry))
                result.Add((entry.Item1, Ui.Solid(Theme.IsDark ? entry.Item2 : Darken(entry.Item2))));
            else
                result.Add(("Other", Ui.Solid(Theme.IsDark ? "#565E6C" : "#B4BCC8")));
        }
        if (result.Count == 0) result.Add(("No data yet", Theme.Track));
        return result;
    }

    private static string Darken(string hex)
    {
        // Light theme legend colors need a touch more weight to stay legible.
        return hex switch
        {
            "#33A6F0" => "#1B7FBE",
            "#B48CF2" => "#8A5CE0",
            "#F2A33C" => "#C97F1D",
            "#8A94A6" => "#6C7686",
            _ => hex
        };
    }

    internal static string Friendly(string exe)
    {
        if (ShellProcessNames.Contains(exe.ToLowerInvariant())) return "System";
        return AetherIsland.Windows.ForegroundTracker.FriendlyName(exe);
    }

    private static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "conhost", "dllhost", "runtimebroker", "searchhost", "textinputhost",
        "startmenuexperiencehost", "shellexperiencehost", "applicationframehost", "widgets",
        "taskmgr", "systemsettings", "sihost", "ctfmon", "fontdrvhost", "dwm", "winlogon"
    };

    internal static string FormatSeconds(long seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        if (seconds < 3600) return $"{seconds / 60}m";
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes:00}m";
    }
}
