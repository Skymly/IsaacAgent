using Avalonia;
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

    private static IBrush ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is IBrush b)
            return b;
        return new SolidColorBrush(Colors.Transparent);
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogEntry.EntryLevel level)
        {
            return level switch
            {
                LogEntry.EntryLevel.Error => ResolveBrush("IsaacLogErrorBrush"),
                LogEntry.EntryLevel.Warning => ResolveBrush("IsaacLogWarningBrush"),
                _ => ResolveBrush("IsaacLogInfoBrush")
            };
        }
        return ResolveBrush("IsaacLogInfoBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
