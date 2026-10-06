using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BcdEditTool.Views.BootManager;

/// <summary>
/// Converts boolean values to System.Windows.Visibility with inversion support.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var invert = parameter is string s && string.Equals(s, "invert", StringComparison.OrdinalIgnoreCase);

        if (invert)
        {
            boolValue = !boolValue;
        }

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
