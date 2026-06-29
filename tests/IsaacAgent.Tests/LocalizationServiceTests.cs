using IsaacAgent.App.Services;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for LocalizationService — language management,
///   config persistence, and supported languages list.
/// </summary>
public class LocalizationServiceTests
{
    [Fact]
    public void Constructor_DefaultLanguage_IsEnglish()
    {
        var config = new AppConfiguration();
        var svc = new LocalizationService(config);
        Assert.Equal("en", svc.CurrentLanguage);
    }

    [Fact]
    public void Constructor_LoadsLanguageFromConfig()
    {
        var config = new AppConfiguration { Language = "zh" };
        var svc = new LocalizationService(config);
        Assert.Equal("zh", svc.CurrentLanguage);
    }

    [Fact]
    public void Constructor_EmptyLanguage_DefaultsToEnglish()
    {
        var config = new AppConfiguration { Language = "" };
        var svc = new LocalizationService(config);
        Assert.Equal("en", svc.CurrentLanguage);
    }

    [Fact]
    public void Constructor_NullLanguage_DefaultsToEnglish()
    {
        var config = new AppConfiguration { Language = null! };
        var svc = new LocalizationService(config);
        Assert.Equal("en", svc.CurrentLanguage);
    }

    [Fact]
    public void Languages_ContainsEnglishAndChinese()
    {
        Assert.Contains("en", LocalizationService.Languages);
        Assert.Contains("zh", LocalizationService.Languages);
        Assert.Equal(2, LocalizationService.Languages.Count);
    }

    [Fact]
    public void EnglishConstant_IsEn()
    {
        Assert.Equal("en", LocalizationService.English);
    }

    [Fact]
    public void ChineseConstant_IsZh()
    {
        Assert.Equal("zh", LocalizationService.Chinese);
    }
}
