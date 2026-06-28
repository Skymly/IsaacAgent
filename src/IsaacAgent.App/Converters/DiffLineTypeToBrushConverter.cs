using Avalonia;
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

    private static IBrush ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is IBrush b)
            return b;
        return new SolidColorBrush(Colors.Transparent);
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiffLine.LineType type)
        {
            return type switch
            {
                DiffLine.LineType.Added => ResolveBrush("IsaacDiffAddedBrush"),
                DiffLine.LineType.Removed => ResolveBrush("IsaacDiffRemovedBrush"),
                DiffLine.LineType.Header => ResolveBrush("IsaacAccentBrush"),
                _ => ResolveBrush("IsaacDiffContextBrush")
            };
        }
        return ResolveBrush("IsaacDiffContextBrush");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
