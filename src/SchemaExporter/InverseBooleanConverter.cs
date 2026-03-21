using System.Globalization;
using System.Windows.Data;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// 將布林值轉換為反向值，供 XAML 繫結使用。
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBooleanConverter : IValueConverter {
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        return value is bool boolValue && !boolValue;
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        return value is bool boolValue && !boolValue;
    }
}

