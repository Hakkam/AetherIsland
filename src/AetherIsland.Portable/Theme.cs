using System.Windows;
using System.Windows.Media;

namespace AetherIsland.Portable;

/// <summary>
/// Central design tokens. Dark theme matches the reference panel (near-black
/// surfaces, blue accent). Light theme mirrors the same hierarchy. All module
/// renderers read brushes from here so a theme change re-renders cleanly.
/// </summary>
internal static class Theme
{
    public static bool IsDark { get; private set; } = true;

    public static void Apply(string setting)
    {
        var mode = string.IsNullOrWhiteSpace(setting) ? "dark" : setting.ToLowerInvariant();
        if (mode == "system")
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                IsDark = value is not int light || light == 0;
            }
            catch { IsDark = true; }
        }
        else IsDark = mode != "light";
    }

    private static Color C(string hex) => (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);

    public static Color AccentColor =>
        C(string.IsNullOrWhiteSpace(PortableData.Settings.Accent) ? "#33A6F0" : PortableData.Settings.Accent);

    public static Brush Bg        => Solid(IsDark ? "#0F1216" : "#F4F6F9");
    public static Brush PanelEdge => Solid(IsDark ? "#262C36" : "#D9DEE7");
    public static Brush Card      => Solid(IsDark ? "#14181E" : "#FFFFFF");
    public static Brush CardEdge  => Solid(IsDark ? "#1F252E" : "#E4E8EF");
    public static Brush Chip      => Solid(IsDark ? "#222833" : "#E8ECF2");
    public static Brush ChipOn    => Brushes.White;
    public static Brush Text      => Solid(IsDark ? "#F2F5F9" : "#1A2029");
    public static Brush Dim       => Solid(IsDark ? "#98A1B0" : "#5C6675");
    public static Brush Faint     => Solid(IsDark ? "#6B7484" : "#8B95A3");
    public static Brush Track     => Solid(IsDark ? "#2A303A" : "#DDE2EA");
    public static Brush Accent    => Solid(AccentColor.ToString());
    public static Brush Purple    => Solid(IsDark ? "#B48CF2" : "#8A5CE0");
    public static Brush Orange    => Solid("#F2A33C");
    public static Brush Good      => Solid("#36C797");
    public static Brush Warn      => Solid("#F2B45C");
    public static Brush Bad       => Solid("#FF6878");
    public static Brush IconOnBg  => Brushes.White;

    private static readonly Dictionary<string, SolidColorBrush> Cache = new();
    private static SolidColorBrush Solid(string hex)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(hex, out var brush)) return brush;
            var b = new SolidColorBrush(C(hex));
            b.Freeze();
            Cache[hex] = b;
            return b;
        }
    }

    /// <summary>Deterministic pastel tile color for apps without an extractable icon.</summary>
    public static Brush TileFor(string key)
    {
        var palette = new[] { "#5B8DEF", "#46B881", "#E9695E", "#B48CF2", "#F2A33C", "#3BB3C3", "#DE6FA9", "#7CA45B" };
        unchecked
        {
            var hash = 17;
            foreach (var ch in key) hash = hash * 31 + ch;
            return Solid(palette[Math.Abs(hash) % palette.Length]);
        }
    }
}
