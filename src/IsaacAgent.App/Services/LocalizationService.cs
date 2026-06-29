using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using IsaacAgent.App.Services;

namespace IsaacAgent.App.Services;

/// <summary>
///   Manages UI language switching at runtime by swapping the merged
///   string resource dictionary in Application.Resources.
/// </summary>
public sealed class LocalizationService
{
    public const string English = "en";
    public const string Chinese = "zh";

    private static readonly string[] SupportedLanguages = [English, Chinese];

    private readonly AppConfiguration _config;
    private string _currentLanguage;
    private ResourceDictionary? _currentStrings;

    public LocalizationService(AppConfiguration config)
    {
        _config = config;
        _currentLanguage = string.IsNullOrEmpty(config.Language) ? English : config.Language;
    }

    /// <summary>Current language code ("en" or "zh").</summary>
    public string CurrentLanguage => _currentLanguage;

    /// <summary>All supported language codes.</summary>
    public static IReadOnlyList<string> Languages => SupportedLanguages;

    /// <summary>
    ///   Apply the saved language to the application resources.
    ///   Call once at startup after Avalonia is initialized.
    /// </summary>
    public void ApplyInitialLanguage()
    {
        ApplyLanguage(_currentLanguage);
    }

    /// <summary>
    ///   Switch the UI language at runtime. Updates the merged resource
    ///   dictionary so all {DynamicResource} bindings refresh automatically.
    /// </summary>
    public void SetLanguage(string language)
    {
        if (language == _currentLanguage) return;
        ApplyLanguage(language);
        _currentLanguage = language;
        _config.Language = language;
        _config.Save();
    }

    private void ApplyLanguage(string language)
    {
        if (Application.Current is null) return;

        var sourceUri = language switch
        {
            Chinese => "avares://IsaacAgent/Styles/Strings.zh.axaml",
            _ => "avares://IsaacAgent/Styles/Strings.en.axaml"
        };

        var newDict = (ResourceDictionary)AvaloniaXamlLoader.Load(
            new System.Uri(sourceUri),
            null);

        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

        // Remove the previous strings dictionary if present.
        if (_currentStrings is not null)
            mergedDictionaries.Remove(_currentStrings);

        _currentStrings = newDict;
        mergedDictionaries.Add(newDict);
    }
}
