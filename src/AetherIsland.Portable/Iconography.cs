using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AetherIsland.Portable;

/// <summary>
/// Font-independent vector icons (24x24 grid, stroked paths). No Private Use
/// Unicode or Segoe MDL2 Assets, preventing missing-glyph rectangles.
/// </summary>
internal static class Iconography
{
    private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        ["home"]      = "M4,11 L12,4 20,11 M6,10 L6,20 10,20 10,14 14,14 14,20 18,20 18,10",
        ["dashboard"] = "M3.5,3.5 L10,3.5 10,10 3.5,10 Z M14,3.5 L20.5,3.5 20.5,10 14,10 Z M3.5,14 L10,14 10,20.5 3.5,20.5 Z M14,14 L20.5,14 20.5,20.5 14,20.5 Z",
        ["shelf"]     = "M5,7.5 L5,20 19,20 19,7.5 M8.5,7.5 A3.5,3.5 0 0 1 15.5,7.5 M5,11.5 L19,11.5",
        ["tasks"]     = "M4,6 L6,8 9.5,4 M12.5,6.5 L20.5,6.5 M4,13 L6,15 9.5,11 M12.5,13.5 L20.5,13.5 M4,20 L20.5,20",
        ["notes"]     = "M6,3 L18,3 18,21 6,21 Z M9,7.5 L15,7.5 M9,11 L15,11 M9,14.5 L13,14.5",
        ["focus"]     = "M7,3 L17,3 17,8 12,12 17,16 17,21 7,21 7,16 12,12 7,8 Z",
        ["media"]     = "M9,18 A2.5,2.5 0 1 1 9,13 M11.5,15.5 L11.5,4.5 19,3.5 19,14 M16.5,16.5 A2.5,2.5 0 1 1 16.5,11.5 M11.5,9 L19,8",
        ["wellbeing"] = "M12,20 C7.2,15.7 3.5,12.6 3.5,8.9 A4.4,4.4 0 0 1 12,6.2 A4.4,4.4 0 0 1 20.5,8.9 C20.5,12.6 16.8,15.7 12,20 Z",
        ["screen"]    = "M12,12 L12,3 A9,9 0 1 1 3.4,15.2 Z",
        ["weather"]   = "M6.5,17.5 A4,4 0 0 1 8,9.8 A6,6 0 0 1 19.4,12 A3.6,3.6 0 0 1 18.5,19 L7,19 Z",
        ["network"]   = "M3,9.5 A13.5,13.5 0 0 1 21,9.5 M6,13.5 A9,9 0 0 1 18,13.5 M9.2,17 A4.5,4.5 0 0 1 14.8,17 M12,20.5 L12,20.6",
        ["ai"]        = "M11,4 L12.7,9.3 18,11 12.7,12.7 11,18 9.3,12.7 4,11 9.3,9.3 Z M18.5,14 L19.3,16.2 21.5,17 19.3,17.8 18.5,20 17.7,17.8 15.5,17 17.7,16.2 Z",
        ["utilities"] = "M19.8,6.3 A5.2,5.2 0 0 1 12.6,11.5 L5.6,18.5 A1.9,1.9 0 0 1 2.9,15.8 L9.9,8.8 A5.2,5.2 0 0 1 15.1,1.6 L11.9,4.8 14.6,7.5 17.7,4.4 Z",
        ["settings"]  = "M12,5.5 A6.5,6.5 0 1 1 12,18.5 A6.5,6.5 0 1 1 12,5.5 Z M12,9.6 A2.4,2.4 0 1 1 12,14.4 A2.4,2.4 0 1 1 12,9.6 Z M12,1.8 L12,3.6 M12,20.4 L12,22.2 M1.8,12 L3.6,12 M20.4,12 L22.2,12 M4.7,4.7 L6,6 M18,18 L19.3,19.3 M19.3,4.7 L18,6 M6,18 L4.7,19.3",
        ["wifi"]      = "M3.5,9.5 A13,13 0 0 1 20.5,9.5 M6.3,13.2 A8.5,8.5 0 0 1 17.7,13.2 M9.3,16.8 A4.4,4.4 0 0 1 14.7,16.8 M12,20.3 L12,20.4",
        ["bluetooth"] = "M7,7.5 L17,16.5 12,21 12,3 17,7.5 7,16.5",
        ["coffee"]    = "M4,9 L16.5,9 16.5,14.5 A4,4 0 0 1 12.5,18.5 L8,18.5 A4,4 0 0 1 4,14.5 Z M16.5,10.2 L18.6,10.2 A2.4,2.4 0 0 1 18.6,15 L16.5,15 M7.5,3.5 L7.5,6 M11,3.5 L11,6",
        ["moon"]      = "M19.5,14.5 A8.2,8.2 0 1 1 9.5,3.6 A6.8,6.8 0 0 0 19.5,14.5 Z",
        ["mic"]       = "M12,3.2 A2.9,2.9 0 0 1 14.9,6.1 L14.9,11 A2.9,2.9 0 0 1 9.1,11 L9.1,6.1 A2.9,2.9 0 0 1 12,3.2 Z M6,11.2 A6,6 0 0 0 18,11.2 M12,17.2 L12,20.6 M8.8,20.6 L15.2,20.6",
        ["volume"]    = "M4,9.4 L7.4,9.4 12.4,5.2 12.4,18.8 7.4,14.6 4,14.6 Z M15.6,9.2 A4.3,4.3 0 0 1 15.6,14.8 M18.2,6.8 A8,8 0 0 1 18.2,17.2",
        ["screenshot"]= "M7.5,3 L7.5,16.5 21,16.5 M3,7.5 L16.5,7.5 16.5,21",
        ["lock"]      = "M6,10.8 L18,10.8 18,20.6 6,20.6 Z M9,10.8 L9,7.4 A3,3 0 0 1 15,7.4 L15,10.8 M12,14.4 L12,17",
        ["refresh"]   = "M19.5,4.5 L19.5,9.5 14.5,9.5 M4.5,19.5 L4.5,14.5 9.5,14.5 M5.2,9.3 A7.6,7.6 0 0 1 18.4,6.9 M18.8,14.7 A7.6,7.6 0 0 1 5.6,17.1",
        ["pin"]       = "M9,3.5 L15,3.5 14.4,9.5 18,12.5 18,14 6,14 6,12.5 9.6,9.5 Z M12,14 L12,21",
        ["pinned"]    = "M9,3.5 L15,3.5 14.4,9.5 18,12.5 18,14 6,14 6,12.5 9.6,9.5 Z M12,14 L12,21",
        ["collapse"]  = "M6,9 L12,15 18,9",
        ["play"]      = "M8,5 L18.5,12 8,19 Z",
        ["pause"]     = "M8.5,5 L8.5,19 M15.5,5 L15.5,19",
        ["prev"]      = "M17.5,5.5 L9,12 17.5,18.5 Z M7,5.5 L7,18.5",
        ["next"]      = "M6.5,5.5 L15,12 6.5,18.5 Z M17,5.5 L17,18.5",
        ["camera"]    = "M3,7.4 L8,7.4 10,4.8 14,4.8 16,7.4 21,7.4 21,18.6 3,18.6 Z M12,9.6 A3.6,3.6 0 1 1 12,16.8 A3.6,3.6 0 0 1 12,9.6 Z",
        ["calendar"]  = "M4,6 L20,6 20,20.5 4,20.5 Z M4,10 L20,10 M8.5,3.5 L8.5,7.5 M15.5,3.5 L15.5,7.5",
        ["plus"]      = "M12,5 L12,19 M5,12 L19,12",
        ["close"]     = "M6,6 L18,18 M18,6 L6,18",
        ["check"]     = "M4.5,12.5 L9.5,17.5 19.5,6.5",
        ["trash"]     = "M5,7 L19,7 M9.5,7 L9.5,4.5 14.5,4.5 14.5,7 M7,7 L7.8,20.5 16.2,20.5 17,7 M10,10.5 L10,17 M14,10.5 L14,17",
        ["share"]     = "M12,14 L12,4 M8,7.5 L12,3.5 16,7.5 M5,12 L5,20 19,20 19,12",
        ["download"]  = "M12,3.5 L12,15.5 M7.5,11.5 L12,16 16.5,11.5 M4.5,19.5 L19.5,19.5",
        ["search"]    = "M10.5,4 A6.5,6.5 0 1 1 10.5,17 A6.5,6.5 0 0 1 10.5,4 Z M15.5,15.5 L20.5,20.5",
        ["drop"]      = "M12,15.5 L12,4.5 M8,8 L12,4 16,8 M5,13.5 L5,19.5 19,19.5 19,13.5",
        ["folder"]    = "M3,6.5 L10,6.5 12,9 21,9 21,19.5 3,19.5 Z",
        ["open"]      = "M9,5 L19,5 19,15 M19,5 L5,19",
        ["copy"]      = "M8.5,8.5 L19,8.5 19,20 8.5,20 Z M5,15.5 L5,4 15.5,4",
        ["eye"]       = "M2.5,12 C5,7.5 8.3,5.5 12,5.5 C15.7,5.5 19,7.5 21.5,12 C19,16.5 15.7,18.5 12,18.5 C8.3,18.5 5,16.5 2.5,12 Z M12,9 A3,3 0 1 1 12,15 A3,3 0 0 1 12,9 Z",
        ["clock"]     = "M12,3.5 A8.5,8.5 0 1 1 12,20.5 A8.5,8.5 0 0 1 12,3.5 Z M12,7.5 L12,12 15.5,14",
        ["bell"]      = "M6,16 L6,10.5 A6,6 0 0 1 18,10.5 L18,16 20,18.5 4,18.5 Z M9.8,18.5 A2.4,2.4 0 0 0 14.2,18.5",
        ["battery"]   = "M3.5,8 L18.5,8 18.5,16 3.5,16 Z M20.5,10.5 L20.5,13.5",
        ["flip"]      = "M4,9 A8.5,8.5 0 0 1 19,7.5 M19.5,15 A8.5,8.5 0 0 1 5,16.5 M19,3.5 L19,7.5 15,7.5 M5,20.5 L5,16.5 9,16.5",
        ["sliders"]   = "M4,7 L20,7 M4,17 L20,17 M9.5,4.5 L9.5,9.5 M14.5,14.5 L14.5,19.5",
        ["activity"]  = "M3.5,12 L7.5,12 10,6 13.5,18 16,12 20.5,12",
        ["calculator"]= "M6,3 L18,3 18,21 6,21 Z M8.5,6.5 L15.5,6.5 M8.5,11 L10,11 M12.5,11 L14,11 M8.5,14.5 L10,14.5 M12.5,14.5 L14,14.5 M8.5,18 L10,18 M12.5,18 L14,18 M14.5,14.5 L16,14.5 M14.5,11 L16,11"
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
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
        if (onClick != null) button.Click += (_, _) => onClick();
        return button;
    }

    public static void SetIcon(Button button, string name, double iconSize = 15, Brush? brush = null)
    {
        button.Content = Draw(name, iconSize, brush: brush ?? (Theme.IsDark ? Brushes.White : Theme.Text));
    }
}
