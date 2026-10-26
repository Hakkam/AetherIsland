using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AetherIsland.Portable;

internal static class Ui
{
    internal static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(147, 155, 170));
    internal static readonly Brush Cyan = new SolidColorBrush(Color.FromRgb(27, 169, 234));
    internal static TextBlock Text(string text, int size = 12, bool bold = false, Brush? color = null)
        => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = color ?? Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
    internal static TextBlock Title(string s) => Text(s.ToUpperInvariant(), 10, true, Dim);
    internal static Border Group(string title, UIElement content, double width = 200, bool last = false)
    {
        var stack = new StackPanel { Margin = new Thickness(10, 9, 10, 8) };
        stack.Children.Add(Title(title));
        stack.Children.Add(new Border { Height = 10, Background = Brushes.Transparent });
        stack.Children.Add(content);
        return new Border
        {
            Width = width, BorderBrush = new SolidColorBrush(Color.FromRgb(39, 45, 53)),
            BorderThickness = last ? new Thickness(0) : new Thickness(0, 0, 1, 0),
            Child = stack, Background = Brushes.Transparent
        };
    }
    internal static StackPanel H(params UIElement[] children)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var item in children) s.Children.Add(item);
        return s;
    }
    internal static StackPanel V(params UIElement[] children)
    {
        var s = new StackPanel();
        foreach (var item in children) s.Children.Add(item);
        return s;
    }
    internal static Button Button(string text, Action callback, string? tooltip = null, int size = 11)
    {
        var b = new Button { Content = text, FontSize = size, Margin = new Thickness(2), ToolTip = tooltip ?? text };
        b.Click += (_, _) => callback();
        return b;
    }
    internal static TextBox Input(string hint = "", int width = 230)
        => new() { Width = width, Text = hint, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
    internal static UIElement Bar(double ratio, int width = 160)
    {
        var g = new Grid { Width = width, Height = 5, HorizontalAlignment = HorizontalAlignment.Left };
        g.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(46, 50, 57)), CornerRadius = new CornerRadius(3) });
        g.Children.Add(new Border { Background = Cyan, Width = Math.Max(0, Math.Min(width, width * ratio)),
            CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left });
        return g;
    }
    internal static UIElement WrapPage(UIElement child)
        => new ScrollViewer { Content = child, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8, 3, 8, 2) };
    internal static UIElement Placeholder(string title, string detail)
    {
        var v = V(Title(title), Text(detail, 12, false, Dim));
        v.Margin = new Thickness(12, 12, 10, 5);
        return WrapPage(v);
    }
}
