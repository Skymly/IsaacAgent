using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using IsaacAgent.App.Services;

namespace IsaacAgent.App.Services;

/// <summary>
///   Manages light/dark theme switching at runtime.
///   Uses Avalonia's built-in ThemeVariant for the FluentTheme plus
///   a custom color override dictionary for Isaac-specific colors.
/// </summary>
public sealed class ThemeService
{
    public const string Dark = "dark";
    public const string Light = "light";

    private static readonly string[] SupportedThemes = [Dark, Light];

    private readonly AppConfiguration _config;
    private string _currentTheme;
    private ResourceDictionary? _currentThemeColors;

    /// <summary>Custom accent color hex string, or null to use theme default.</summary>
    public string? CustomAccentColor
    {
        get => _config.AccentColor;
        set
        {
            _config.AccentColor = value;
            ApplyAccentColor(value);
        }
    }

    public ThemeService(AppConfiguration config)
    {
        _config = config;
        _currentTheme = string.IsNullOrEmpty(config.Theme) ? Dark : config.Theme;
    }

    /// <summary>Current theme name ("dark" or "light").</summary>
    public string CurrentTheme => _currentTheme;

    /// <summary>All supported theme names.</summary>
    public static IReadOnlyList<string> Themes => SupportedThemes;

    /// <summary>
    ///   Apply the saved theme to the application. Call once at startup
    ///   after Avalonia is initialized.
    /// </summary>
    public void ApplyInitialTheme()
    {
        ApplyTheme(_currentTheme);
    }

    /// <summary>
    ///   Switch the theme at runtime. Updates RequestedThemeVariant
    ///   and swaps the Isaac-specific color palette.
    /// </summary>
    public void SetTheme(string theme)
    {
        if (theme == _currentTheme) return;
        ApplyTheme(theme);
        _currentTheme = theme;
        _config.Theme = theme;
        _config.Save();
    }

    private void ApplyTheme(string theme)
    {
        if (Application.Current is null) return;

        // Set the FluentTheme variant.
        Application.Current.RequestedThemeVariant = theme switch
        {
            Light => ThemeVariant.Light,
            _ => ThemeVariant.Dark
        };

        // Swap the Isaac-specific color palette.
        var sourceUri = theme switch
        {
            Light => "avares://IsaacAgent/Styles/Theme.Light.axaml",
            _ => "avares://IsaacAgent/Styles/Theme.axaml"
        };

        var newDict = (ResourceDictionary)AvaloniaXamlLoader.Load(
            new System.Uri(sourceUri),
            null);

        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

        // Remove the previous theme colors if present.
        if (_currentThemeColors is not null)
            mergedDictionaries.Remove(_currentThemeColors);

        _currentThemeColors = newDict;
        mergedDictionaries.Add(newDict);

        // Re-apply custom accent color if set.
        if (!string.IsNullOrEmpty(_config.AccentColor))
            ApplyAccentColor(_config.AccentColor);
    }

    /// <summary>
    ///   Override the IsaacAccentColor/Brush resources with a custom hex color.
    ///   Pass null to reset to the theme default.
    /// </summary>
    public void ApplyAccentColor(string? hexColor)
    {
        if (Application.Current is null) return;

        if (string.IsNullOrEmpty(hexColor))
        {
            // Remove custom override — the theme dictionary's value will show through.
            Application.Current.Resources.Remove("IsaacAccentColor");
            Application.Current.Resources.Remove("IsaacAccentBrush");
            return;
        }

        try
        {
            var color = Avalonia.Media.Color.Parse(hexColor);
            Application.Current.Resources["IsaacAccentColor"] = color;
            Application.Current.Resources["IsaacAccentBrush"] = new Avalonia.Media.SolidColorBrush(color);
        }
        catch
        {
            // Invalid hex string — ignore.
        }
    }

    /// <summary>
    ///   Apply the saved accent color at startup.
    /// </summary>
    public void ApplyInitialAccentColor()
    {
        if (!string.IsNullOrEmpty(_config.AccentColor))
            ApplyAccentColor(_config.AccentColor);
    }
}
