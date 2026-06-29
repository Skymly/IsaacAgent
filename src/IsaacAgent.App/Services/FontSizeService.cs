using Avalonia;

namespace IsaacAgent.App.Services;

/// <summary>
///   Manages UI font size scaling at runtime by setting a
///   "IsaacFontSizeMultiplier" resource that views can reference.
/// </summary>
public sealed class FontSizeService
{
    public const string Small = "small";
    public const string Medium = "medium";
    public const string Large = "large";

    private static readonly string[] SupportedSizes = [Small, Medium, Large];

    /// <summary>Font size multiplier for the current setting.</summary>
    public static double GetMultiplier(string fontSize) => fontSize switch
    {
        Small => 0.85,
        Large => 1.15,
        _ => 1.0
    };

    /// <summary>All supported font size names.</summary>
    public static IReadOnlyList<string> Sizes => SupportedSizes;

    /// <summary>
    ///   Apply the font size multiplier to application resources.
    /// </summary>
    public static void ApplyFontSize(string fontSize)
    {
        if (Application.Current is null) return;
        Application.Current.Resources["IsaacFontSizeMultiplier"] = GetMultiplier(fontSize);
    }
}
