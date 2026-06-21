using System.Globalization;
using Avalonia.Data.Converters;
using IsaacAgent.Rag.Embedding;

namespace IsaacAgent.App.Converters;

/// <summary>
/// Converts an EmbeddingSourceType to a visibility bool for Ollama or ONNX panels.
/// Use EmbeddingSourceVisibilityConverter.OllamaInstance or .OnnxInstance.
/// </summary>
public sealed class EmbeddingSourceVisibilityConverter : IValueConverter
{
    public static readonly EmbeddingSourceVisibilityConverter OllamaInstance = new() { _target = EmbeddingSourceType.Ollama };
    public static readonly EmbeddingSourceVisibilityConverter OnnxInstance = new() { _target = EmbeddingSourceType.Onnx };

    private EmbeddingSourceType _target;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EmbeddingSourceType source)
            return source == _target;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
