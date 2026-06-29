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
    public void Languages_ContainsAllFourLanguages()
    {
        Assert.Contains("en", LocalizationService.Languages);
        Assert.Contains("zh", LocalizationService.Languages);
        Assert.Contains("ja", LocalizationService.Languages);
        Assert.Contains("ko", LocalizationService.Languages);
        Assert.Equal(4, LocalizationService.Languages.Count);
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

    [Fact]
    public void JapaneseConstant_IsJa()
    {
        Assert.Equal("ja", LocalizationService.Japanese);
    }

    [Fact]
    public void KoreanConstant_IsKo()
    {
        Assert.Equal("ko", LocalizationService.Korean);
    }
}
