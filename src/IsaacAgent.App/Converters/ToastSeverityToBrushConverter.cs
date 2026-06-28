using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Converters;

/// <summary>
///   Converts a <see cref="ToastSeverity"/> to a brush for the toast icon.
/// </summary>
public sealed class ToastSeverityToBrushConverter : IValueConverter
{
    public static readonly ToastSeverityToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ToastSeverity severity) return null;
        return severity switch
        {
            ToastSeverity.Success => ResolveBrush("ToastSuccessBrush"),
            ToastSeverity.Warning => ResolveBrush("ToastWarningBrush"),
            ToastSeverity.Error => ResolveBrush("ToastErrorBrush"),
            _ => ResolveBrush("ToastInfoBrush")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static IBrush? ResolveBrush(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetValue(key, out var resource) == true
            && resource is IBrush brush)
            return brush;
        return Brushes.White;
    }
}
