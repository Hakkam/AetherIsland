using System.Windows.Data;
using System.Windows.Media;

namespace AetherIsland.App.Infrastructure;

/// <summary>Converts a nav item's IsAiAccent flag into the violet AI accent or the default accent brush.</summary>
public sealed class BooleanToBrushConverter : IValueConverter
{
    private static Brush? _ai;
    private static Brush? _accent;

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        _ai ??= TryGet("AiBrush") ?? Brushes.Violet;
        _accent ??= TryGet("AccentDynamicBrush") ?? Brushes.DodgerBlue;
        return value is true ? _ai : _accent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => Binding.DoNothing;

    private static Brush? TryGet(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as Brush;
}
