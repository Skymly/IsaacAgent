using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;
using IsaacAgent.App.Services;

namespace IsaacAgent.App.Services;

/// <summary>
/// Converts a DiffLine.LineType to a colored brush for the diff viewer.
/// </summary>
public sealed class DiffLineTypeToBrushConverter : IValueConverter
{
    public static readonly DiffLineTypeToBrushConverter Instance = new();

    private static readonly IBrush AddedBrush = new SolidColorBrush(Color.Parse("#4EC9B0"));
    private static readonly IBrush RemovedBrush = new SolidColorBrush(Color.Parse("#F48771"));
    private static readonly IBrush ContextBrush = new SolidColorBrush(Color.Parse("#D4D4D4"));
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.Parse("#569CD6"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiffLine.LineType type)
        {
            return type switch
            {
                DiffLine.LineType.Added => AddedBrush,
                DiffLine.LineType.Removed => RemovedBrush,
                DiffLine.LineType.Header => HeaderBrush,
                _ => ContextBrush
            };
        }
        return ContextBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
