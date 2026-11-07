using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AetherIsland.Portable;

/// <summary>
/// Font-independent vector icons using Lucide (lucide.dev, ISC licensed) path
/// data — 24x24 stroked icons with round caps and joins. No Private Use
/// Unicode or Segoe MDL2 Assets, preventing missing-glyph rectangles.
/// Multi-subpath strings render as one Path with several figures.
/// </summary>
internal static class Iconography
{
    private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        // --- navigation ---
        ["home"]      = "M3 9.5 12 3l9 6.5V20a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 20Z M9 21.5v-8h6v8",
        ["dashboard"] = "M3 3h7v7H3Z M14 3h7v7h-7Z M14 14h7v7h-7Z M3 14h7v7H3Z",
        ["shelf"]     = "M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z M3 6h18 M16 10a4 4 0 0 1-8 0",
        ["tasks"]     = "m3 17 2 2 4-4 M3 7l2 2 4-4 M13 6h8 M13 12h8 M13 18h8",
        ["notes"]     = "M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z M14 2v4a2 2 0 0 0 2 2h4 M10 9H8 M16 13H8 M16 17H8",
        ["focus"]     = "M5 22h14 M5 2h14 M17 22v-4.17a2 2 0 0 0-.59-1.42L12 12l-4.41 4.41A2 2 0 0 0 7 17.83V22 M7 2v4.17a2 2 0 0 0 .59 1.42L12 12l4.41-4.41A2 2 0 0 0 17 6.17V2",
        ["media"]     = "M9 18V5l12-2v13 M6 21a3 3 0 1 1 0-6 3 3 0 0 1 0 6Z M18 19a3 3 0 1 1 0-6 3 3 0 0 1 0 6Z",
        ["wellbeing"] = "M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z",
        ["screen"]    = "M21.21 15.89A10 10 0 1 1 8 2.83 M22 12A10 10 0 0 0 12 2v10Z",
        ["weather"]   = "M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9Z",
        ["network"]   = "M2 8.82a15 15 0 0 1 20 0 M5 12.86a10 10 0 0 1 14 0 M8.5 16.43a5 5 0 0 1 7 0 M12 20h.01",
        ["ai"]        = "M9.94 15.5A2 2 0 0 0 8.5 14.06L2.36 12.48a.5.5 0 0 1 0-.96L8.5 9.94A2 2 0 0 0 9.94 8.5l1.58-6.14a.5.5 0 0 1 .96 0L14.06 8.5A2 2 0 0 0 15.5 9.94l6.14 1.58a.5.5 0 0 1 0 .96L15.5 14.06a2 2 0 0 0-1.44 1.44l-1.58 6.13a.5.5 0 0 1-.96 0Z M20 3v4 M22 5h-4 M4 17v2 M5 18H3",
        ["utilities"] = "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76Z",
        ["settings"]  = "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2Z M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z",

        // --- quick toggles ---
        ["wifi"]      = "M2 8.82a15 15 0 0 1 20 0 M5 12.86a10 10 0 0 1 14 0 M8.5 16.43a5 5 0 0 1 7 0 M12 20h.01",
        ["bluetooth"] = "m7 7 10 10-5 5V2l5 5L7 17",
        ["coffee"]    = "M10 2v2 M14 2v2 M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h13a4 4 0 1 1 0 8h-1 M6 2v2",
        ["moon"]      = "M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z",
        ["mic"]       = "M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3Z M19 10v2a7 7 0 0 1-14 0v-2 M12 19v3",
        ["volume"]    = "M11 5 6 9H2v6h4l5 4V5Z M15.54 8.46a5 5 0 0 1 0 7.07 M19.07 4.93a10 10 0 0 1 0 14.14",
        ["screenshot"]= "M6 2v14a2 2 0 0 0 2 2h14 M18 22V8a2 2 0 0 0-2-2H2",
        ["lock"]      = "M5 10.5h14a1.5 1.5 0 0 1 1.5 1.5v8a1.5 1.5 0 0 1-1.5 1.5h-14A1.5 1.5 0 0 1 3.5 20v-8A1.5 1.5 0 0 1 5 10.5Z M7.5 10.5V7a4.5 4.5 0 0 1 9 0v3.5 M12 14.5v3",

        // --- header / chrome ---
        ["refresh"]   = "M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8 M21 3v5h-5 M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16 M8 16H3v5",
        ["pin"]       = "M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V6h1a2 2 0 0 0 0-4H8a2 2 0 0 0 0 4h1Z",
        ["pinned"]    = "M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V6h1a2 2 0 0 0 0-4H8a2 2 0 0 0 0 4h1Z",
        ["collapse"]  = "m6 9 6 6 6-6",

        // --- media ---
        ["play"]      = "m7 3.5 13 8.5-13 8.5Z",
        ["pause"]     = "M9 4.5v15 M15.5 4.5v15",
        ["prev"]      = "M18 4.5 8.5 12l9.5 7.5Z M5.5 4.5v15",
        ["next"]      = "M6 4.5 15.5 12 6 19.5Z M18.5 4.5v15",
        ["camera"]    = "M14.5 4h-5L7.5 6.5H4a2 2 0 0 0-2 2V18a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8.5a2 2 0 0 0-2-2h-3.5L14.5 4Z M15 12.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z",
        ["calendar"]  = "M8 2v4 M16 2v4 M4.5 4.5h15A1.5 1.5 0 0 1 21 6v14a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 20V6a1.5 1.5 0 0 1 1.5-1.5Z M3 10h18",

        // --- misc ---
        ["plus"]      = "M5 12h14 M12 5v14",
        ["close"]     = "M18 6 6 18 M6 6l12 12",
        ["check"]     = "M20 6 9 17l-5-5",
        ["trash"]     = "M3 6h18 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2 M10 11v6 M14 11v6",
        ["share"]     = "M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8 M16 6l-4-4-4 4 M12 2v13",
        ["download"]  = "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3",
        ["drop"]      = "M12 22a7 7 0 0 0 7-7c0-2-1-3.9-3-5.5s-3.5-4-4-6.5c-.5 2.5-2 4.9-4 6.5C6 11.1 5 13 5 15a7 7 0 0 0 7 7Z",
        ["folder"]    = "M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z",
        ["open"]      = "M15 3h6v6 M10 14 21 3 M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6",
        ["copy"]      = "M8.5 8.5h11a1.5 1.5 0 0 1 1.5 1.5v10a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 7 20V10a1.5 1.5 0 0 1 1.5-1.5Z M4.5 15.5H4a1.5 1.5 0 0 1-1.5-1.5V4A1.5 1.5 0 0 1 4 2.5h10A1.5 1.5 0 0 1 15.5 4v.5",
        ["search"]    = "M11 4a7 7 0 1 1 0 14 7 7 0 0 1 0-14Z M21 21l-4.35-4.35",
        ["clock"]     = "M12 3.5a8.5 8.5 0 1 1 0 17 8.5 8.5 0 0 1 0-17Z M12 7v5l3.5 2",
        ["bell"]      = "M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9 M10.3 21a1.94 1.94 0 0 0 3.4 0",
        ["battery"]   = "M3.5 8h15A1.5 1.5 0 0 1 20 9.5v5a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 2 14.5v-5A1.5 1.5 0 0 1 3.5 8Z M22 11v2",
        ["activity"]  = "M22 12h-4l-3 9L9 3l-3 9H2",
        ["calculator"]= "M5 2.5h14A1.5 1.5 0 0 1 20.5 4v16a1.5 1.5 0 0 1-1.5 1.5H5A1.5 1.5 0 0 1 3.5 20V4A1.5 1.5 0 0 1 5 2.5Z M7.5 6.5h9 M8.5 11h.01 M12 11h.01 M15.5 11h.01 M8.5 14.5h.01 M12 14.5h.01 M15.5 14.5h.01 M8.5 18h.01 M12 18h.01 M15.5 18h.01",
        ["pin-map"]   = "M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z M12 13a3 3 0 1 1 0-6 3 3 0 0 1 0 6Z",
        ["eye"]       = "M2.06 12.35a1 1 0 0 1 0-.7 10.75 10.75 0 0 1 19.88 0 1 1 0 0 1 0 .7 10.75 10.75 0 0 1-19.88 0Z M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z",
        ["sliders"]   = "M21 4h-7 M10 4H3 M21 12h-9 M8 12H3 M21 20h-5 M12 20H3 M14 2v4 M8 10v4 M16 18v4",
        ["flip"]      = "M8 3 4 7l4 4 M4 7h16 M16 21l4-4-4-4 M20 17H4",
        ["weatherfill"]= "M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9Z",
        ["sun"]       = "M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8Z M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M6.34 17.66l-1.41 1.41 M19.07 4.93l-1.41 1.41",
    };

    public static UIElement Draw(string name, double size = 16, double thickness = 1.7, Brush? brush = null)
    {
        var key = Paths.ContainsKey(name) ? name : "dashboard";
        var path = new Path
        {
            Data = Geometry.Parse(Paths[key]),
            Stroke = brush ?? new SolidColorBrush(Color.FromRgb(219, 226, 237)),
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.Uniform,
            Width = 24,
            Height = 24,
            IsHitTestVisible = false
        };
        if (path.Stroke.CanFreeze) path.Stroke.Freeze();
        return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
    }

    public static UIElement DrawFilled(string name, double size, Brush brush)
    {
        var key = Paths.ContainsKey(name) ? name : "dashboard";
        var path = new Path
        {
            Data = Geometry.Parse(Paths[key]),
            Fill = brush,
            Stroke = null,
            Stretch = Stretch.Uniform,
            Width = 24,
            Height = 24,
            IsHitTestVisible = false
        };
        return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
    }

    /// <summary>Square icon button used across the reference layout (nav, toggles, header).</summary>
    public static Button IconButton(string name, string tooltip, double size, Action? onClick = null,
        double iconSize = 15, double corner = 9)
    {
        var button = new Button
        {
            Width = size, Height = size,
            Content = Draw(name, iconSize),
            Background = Brushes.Transparent,
            Margin = new Thickness(1),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            ToolTip = tooltip,
            Cursor = System.Windows.Input.Cursors.Hand,
            FocusVisualStyle = null
        };
        Ui.SetCorner(button, corner);
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
        if (onClick != null) button.Click += (_, _) => onClick();
        return button;
    }

    public static void SetIcon(Button button, string name, double iconSize = 15, Brush? brush = null)
    {
        button.Content = Draw(name, iconSize, brush: brush ?? (Theme.IsDark ? Brushes.White : Theme.Text));
    }
}
