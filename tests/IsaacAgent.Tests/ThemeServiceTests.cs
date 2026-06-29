using IsaacAgent.App.Services;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for ThemeService — theme management,
///   config persistence, and supported themes list.
/// </summary>
public class ThemeServiceTests
{
    [Fact]
    public void Constructor_DefaultTheme_IsDark()
    {
        var config = new AppConfiguration();
        var svc = new ThemeService(config);
        Assert.Equal("dark", svc.CurrentTheme);
    }

    [Fact]
    public void Constructor_LoadsThemeFromConfig()
    {
        var config = new AppConfiguration { Theme = "light" };
        var svc = new ThemeService(config);
        Assert.Equal("light", svc.CurrentTheme);
    }

    [Fact]
    public void Constructor_EmptyTheme_DefaultsToDark()
    {
        var config = new AppConfiguration { Theme = "" };
        var svc = new ThemeService(config);
        Assert.Equal("dark", svc.CurrentTheme);
    }

    [Fact]
    public void Constructor_NullTheme_DefaultsToDark()
    {
        var config = new AppConfiguration { Theme = null! };
        var svc = new ThemeService(config);
        Assert.Equal("dark", svc.CurrentTheme);
    }

    [Fact]
    public void Themes_ContainsDarkAndLight()
    {
        Assert.Contains("dark", ThemeService.Themes);
        Assert.Contains("light", ThemeService.Themes);
        Assert.Equal(2, ThemeService.Themes.Count);
    }

    [Fact]
    public void DarkConstant_IsDark()
    {
        Assert.Equal("dark", ThemeService.Dark);
    }

    [Fact]
    public void LightConstant_IsLight()
    {
        Assert.Equal("light", ThemeService.Light);
    }
}
