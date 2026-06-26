using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace IsaacAgent.App.Services;

/// <summary>
/// Converts a LogEntry.EntryLevel to a colored brush for the log panel.
/// </summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public static readonly LogLevelToBrushConverter Instance = new();

    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F48771"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#CCA700"));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#608B4E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogEntry.EntryLevel level)
        {
            return level switch
            {
                LogEntry.EntryLevel.Error => ErrorBrush,
                LogEntry.EntryLevel.Warning => WarningBrush,
                _ => InfoBrush
            };
        }
        return InfoBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
