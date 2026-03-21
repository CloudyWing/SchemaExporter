#nullable enable

using System.Globalization;
using System.Windows.Data;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Converts a boolean value to its inverse for binding scenarios.
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
