using System.Globalization;
using System.Windows.Data;

namespace AetherIsland.App.Infrastructure;

/// <summary>string equals parameter → true (for segmented buttons/radio styling).</summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter?.ToString() ?? "" : Binding.DoNothing;
}
