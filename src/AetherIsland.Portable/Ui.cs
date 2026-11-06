using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AetherIsland.Portable;

/// <summary>Reference-fidelity building blocks shared by every module renderer.</summary>
internal static class Ui
{
    internal static TextBlock Text(string text, double size = 12, bool bold = false, Brush? color = null,
        TextAlignment align = TextAlignment.Left, bool wrap = false)
        => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = color ?? Theme.Text,
            TextAlignment = align,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };

    internal static FrameworkElement SectionTitle(string glyphIcon, string title)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Iconography.Draw(glyphIcon, 12, 1.9, Theme.Faint));
        var label = Text("  " + title.ToUpperInvariant(), 9.5, true, Theme.Faint);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        return row;
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

    internal static Border Spacer(double width = 0, double height = 0)
        => new() { Width = width > 0 ? width : double.NaN, Height = height > 0 ? height : double.NaN, Background = Brushes.Transparent };

    internal static Button TextButton(string label, Action onClick, string? tooltip = null, double fontSize = 10.5,
        Brush? background = null, Brush? foreground = null, double padding = 9)
    {
        var b = new Button
        {
            Content = label,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground ?? Theme.Text,
            Background = background ?? Theme.Chip,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(padding, 4, padding, 4),
            Margin = new Thickness(2, 0, 2, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FocusVisualStyle = null,
            VerticalAlignment = VerticalAlignment.Center
        };
        SetCorner(b, 8);
        b.Click += (_, _) => onClick();
        if (tooltip != null) b.ToolTip = tooltip;
        return b;
    }

    internal static void SetCorner(Button b, double radius)
    {
        // WPF Button template: round via template-less approach using a border-less custom template.
        b.Template = ButtonTemplate(radius);
    }

    internal static ControlTemplate TextBoxTemplate()
    {
        var template = new ControlTemplate(typeof(TextBox));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "bd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TextBox.BorderThicknessProperty));
        var host = new FrameworkElementFactory(typeof(ScrollViewer));
        host.Name = "PART_ContentHost";
        host.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(TextBox.PaddingProperty));
        border.AppendChild(host);
        template.VisualTree = border;
        return template;
    }

    internal static ControlTemplate ButtonTemplate(double radius)
    {
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "bd";
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        factory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(content);
        template.VisualTree = factory;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        template.Triggers.Add(hover);
        return template;
    }

    internal static TextBox Input(string placeholder, double width, string? text = null, double fontSize = 11)
    {
        var box = new TextBox
        {
            Width = width,
            Text = text ?? "",
            FontSize = fontSize,
            Background = Theme.Chip,
            Foreground = Theme.Text,
            BorderBrush = Theme.CardEdge,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 4, 7, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        box.Template = TextBoxTemplate();
        if (!string.IsNullOrEmpty(placeholder) && string.IsNullOrEmpty(text))
        {
            box.Tag = placeholder;
            box.Foreground = Theme.Faint;
            box.Text = placeholder;
            box.GotKeyboardFocus += (_, _) => { if (ReferenceEquals(box.Tag, placeholder)) { box.Text = ""; box.Foreground = Theme.Text; box.Tag = null; } };
            box.LostKeyboardFocus += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text)) { box.Tag = placeholder; box.Text = placeholder; box.Foreground = Theme.Faint; }
            };
        }
        return box;
    }

    internal static bool IsPlaceholder(TextBox box) => box.Tag is string;

    internal static FrameworkElement Bar(double ratio, double width, double height = 4, Brush? color = null)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        var g = new Grid { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Left };
        g.Children.Add(new Border { Background = Theme.Track, CornerRadius = new CornerRadius(height / 2) });
        if (ratio > 0)
            g.Children.Add(new Border
            {
                Background = color ?? Theme.Accent,
                CornerRadius = new CornerRadius(height / 2),
                Width = Math.Max(height, width * ratio),
                HorizontalAlignment = HorizontalAlignment.Left
            });
        return g;
    }

    /// <summary>Rounded quick toggle matching the reference: white face when on.</summary>
    internal sealed class ToggleBox : Button
    {
        private string _icon;
        private readonly double _size;
        private readonly Brush? _onBackground;
        private readonly Brush? _onForeground;
        private readonly double _corner;
        private bool _on;
        private bool _momentary;

        public event Action<bool>? Toggled;

        public bool IsOn
        {
            get => _on;
            set { _on = value; Refresh(); }
        }

        public ToggleBox(string icon, string tooltip, bool isOn, Action<bool>? onChanged = null,
            double size = 45, Brush? onBackground = null, Brush? onForeground = null, double corner = 15)
        {
            _icon = icon;
            _size = size;
            _onBackground = onBackground;
            _onForeground = onForeground;
            _corner = corner;
            _on = isOn;
            Width = size; Height = size;
            Margin = new Thickness(3);
            BorderThickness = new Thickness(0);
            Cursor = System.Windows.Input.Cursors.Hand;
            FocusVisualStyle = null;
            ToolTip = tooltip;
            SetCorner(this, corner);
            if (onChanged != null) Toggled += onChanged;
            Click += (_, _) =>
            {
                var next = Momentary ? false : !_on;
                var previous = _on;
                _on = next;
                Refresh();
                if (next != previous) Toggled?.Invoke(next);
            };
            Refresh();
        }

        /// <summary>Momentary buttons always revert to the off visual after firing.</summary>
        public bool Momentary { get => _momentary; set { _momentary = value; if (value) _on = false; Refresh(); } }

        public void SetIcon(string icon) { _icon = icon; Refresh(); }

        public void SetUnavailable(string reason)
        {
            Opacity = 0.45;
            IsHitTestVisible = false;
            ToolTip = reason;
        }

        public void Refresh()
        {
            if (_on && _onBackground != null)
            {
                Background = _onBackground;
                Content = Iconography.Draw(_icon, _size * 0.42, 1.9, _onForeground ?? Theme.Text);
            }
            else if (_on)
            {
                Background = Theme.ChipOn;
                Content = Iconography.Draw(_icon, _size * 0.42, 1.9, Solid("#161B22"));
            }
            else
            {
                Background = Theme.Chip;
                Content = Iconography.Draw(_icon, _size * 0.42, 1.9, Theme.Text);
            }
        }
    }

    /// <summary>Segmented control (Today | 7 days, Active | Done | Trash, Devices | Networks).</summary>
    internal static StackPanel Segmented(string[] options, int active, Action<int> onSelected, double fontSize = 9.5)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var buttons = new List<Button>();
        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            var b = new Button
            {
                Content = options[i],
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(9, 3.5, 9, 3.5),
                Margin = new Thickness(1.5, 0, 1.5, 0),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FocusVisualStyle = null
            };
            SetCorner(b, 9);
            buttons.Add(b);
            b.Click += (_, _) =>
            {
                foreach (var other in buttons) StyleSegment(other, false);
                StyleSegment(b, true);
                onSelected(index);
            };
            panel.Children.Add(b);
        }
        StyleSegment(buttons[active], true);
        return panel;
    }

    internal static void StyleSegment(Button b, bool active)
    {
        b.Background = active ? Theme.ChipOn : Brushes.Transparent;
        b.Foreground = active ? Solid("#161B22") : Theme.Dim;
    }

    /// <summary>Reference-style donut with proportional colored arcs and a center label.</summary>
    internal static FrameworkElement Donut(IReadOnlyList<(double ratio, Brush brush)> segments, double size,
        double thickness, FrameworkElement center)
    {
        var grid = new Grid { Width = size, Height = size };
        grid.Children.Add(new Ellipse
        {
            Width = size - thickness, Height = size - thickness,
            Stroke = Theme.Track, StrokeThickness = thickness
        });
        var radius = (size - thickness) / 2;
        var mid = size / 2;
        var startAngle = -90d;
        foreach (var (ratio, brush) in segments)
        {
            if (ratio <= 0) continue;
            var sweep = Math.Min(359.99, ratio * 360);
            var fig = new PathFigure { StartPoint = PointFromAngle(mid, radius, startAngle), IsClosed = false };
            fig.Segments.Add(new ArcSegment(
                PointFromAngle(mid, radius, startAngle + sweep),
                new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true));
            grid.Children.Add(new Path
            {
                Data = new PathGeometry(new[] { fig }),
                Stroke = brush,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat
            });
            startAngle += sweep;
        }
        center.VerticalAlignment = VerticalAlignment.Center;
        center.HorizontalAlignment = HorizontalAlignment.Center;
        grid.Children.Add(center);
        return grid;
    }

    private static Point PointFromAngle(double mid, double radius, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180;
        return new Point(mid + radius * Math.Cos(rad), mid + radius * Math.Sin(rad));
    }

    internal static ScrollViewer WrapPage(UIElement child, double horizontalPadding = 10)
        => new()
        {
            Content = child,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(horizontalPadding, 4, horizontalPadding, 6),
            Background = Brushes.Transparent,
            Focusable = false
        };

    internal static Brush Solid(string hex)
    {
        var b = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    /// <summary>Round transport button for media controls.</summary>
    internal static Button RoundIcon(string name, string tooltip, double size, Action onClick, bool filled = false)
    {
        var b = new Button
        {
            Width = size, Height = size,
            BorderThickness = new Thickness(0),
            Background = filled ? Theme.Accent : Theme.Chip,
            Cursor = System.Windows.Input.Cursors.Hand,
            FocusVisualStyle = null,
            ToolTip = tooltip,
            Margin = new Thickness(2, 0, 2, 0)
        };
        SetCorner(b, size / 2);
        b.Content = Iconography.Draw(name, size * 0.5, 1.8, filled ? Solid("#0B0E12") : Theme.Text);
        b.Click += (_, _) => onClick();
        return b;
    }
}
