using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AetherIsland.Portable;

/// <summary>
/// Font-independent vector icons. No Private Use Unicode or Segoe MDL2 Assets
/// required: this prevents missing-glyph rectangles on some Windows builds.
/// </summary>
internal static class Iconography
{
    private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        ["dashboard"] = "M3,3 L10,3 10,10 3,10 Z M14,3 L21,3 21,10 14,10 Z M3,14 L10,14 10,21 3,21 Z M14,14 L21,14 21,21 14,21 Z",
        ["screen"] = "M3,4 L21,4 21,19 3,19 Z M8,8 A5,5 0 1 1 8,18 A5,5 0 1 1 8,8 M13,8 L13,13 17,16",
        ["media"] = "M5,4 L5,20 M8,5 L19,12 8,19 Z",
        ["shelf"] = "M2,7 L2,20 22,20 22,9 12,9 10,6 2,6 Z M2,7 L10,7 12,10 22,10",
        ["tasks"] = "M3,6 L5,8 9,3 M12,6 L21,6 M3,14 L5,16 9,11 M12,14 L21,14 M3,21 L21,21",
        ["notes"] = "M5,2 L19,2 19,22 5,22 Z M8,7 L16,7 M8,11 L16,11 M8,15 L14,15",
        ["focus"] = "M12,2 A10,10 0 1 1 12,22 A10,10 0 1 1 12,2 M12,6 L12,12 16,15",
        ["weather"] = "M6,15 A4,4 0 0 1 9,9 A6,6 0 0 1 20,13 A4,4 0 0 1 19,20 L7,20 A4,4 0 0 1 6,15 Z",
        ["network"] = "M2,9 A16,16 0 0 1 22,9 M5,13 A11,11 0 0 1 19,13 M9,17 A6,6 0 0 1 15,17 M12,21 L12,21",
        ["ai"] = "M12,2 L14,9 21,12 14,15 12,22 10,15 3,12 10,9 Z M19,2 L20,4 22,5 20,6 19,8 18,6 16,5 18,4 Z",
        ["bluetooth"] = "M8,7 L16,17 12,21 12,3 16,7 8,17",
        ["volume"] = "M3,9 L7,9 12,5 12,19 7,15 3,15 Z M16,8 A6,6 0 0 1 16,16 M18,5 A10,10 0 0 1 18,19",
        ["settings"] = "M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M9,3 L9,9 M16,9 L16,15 M9,15 L9,21",
        ["download"] = "M12,3 L12,16 M7,11 L12,16 17,11 M4,18 L4,21 20,21 20,18",
        ["pin"] = "M8,3 L16,3 15,9 19,12 19,14 5,14 5,12 9,9 Z M12,14 L12,22",
        ["pinned"] = "M8,3 L16,3 15,9 19,12 19,14 5,14 5,12 9,9 Z M12,14 L12,22",
        ["collapse"] = "M6,8 L12,14 18,8"
    };

    public static UIElement Draw(string name, double size = 16)
    {
        var key = Paths.ContainsKey(name) ? name : "dashboard";
        var path = new Path
        {
            Data = Geometry.Parse(Paths[key]),
            Stroke = new SolidColorBrush(Color.FromRgb(219, 226, 237)),
            StrokeThickness = 1.8,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.Uniform,
            Width = 22,
            Height = 22,
            IsHitTestVisible = false
        };
        return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
    }

    public static Button Button(string name, string label, double width = 34)
    {
        var button = new Button
        {
            Width = width,
            Height = width <= 35 ? 30 : width <= 52 ? 46 : 48,
            Content = Draw(name, width <= 35 ? 16 : 19),
            Background = Brushes.Transparent,
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(3),
            ToolTip = label,
            Tag = name
        };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        return button;
    }
}
