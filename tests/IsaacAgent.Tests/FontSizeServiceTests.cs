using IsaacAgent.App.Services;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for FontSizeService — multiplier mapping and sizes list.
/// </summary>
public class FontSizeServiceTests
{
    [Fact]
    public void GetMultiplier_Small_Returns085()
    {
        Assert.Equal(0.85, FontSizeService.GetMultiplier("small"));
    }

    [Fact]
    public void GetMultiplier_Medium_Returns10()
    {
        Assert.Equal(1.0, FontSizeService.GetMultiplier("medium"));
    }

    [Fact]
    public void GetMultiplier_Large_Returns115()
    {
        Assert.Equal(1.15, FontSizeService.GetMultiplier("large"));
    }

    [Fact]
    public void GetMultiplier_Unknown_Returns10()
    {
        Assert.Equal(1.0, FontSizeService.GetMultiplier("unknown"));
    }

    [Fact]
    public void GetMultiplier_Empty_Returns10()
    {
        Assert.Equal(1.0, FontSizeService.GetMultiplier(""));
    }

    [Fact]
    public void Sizes_ContainsAllThreeOptions()
    {
        Assert.Equal(3, FontSizeService.Sizes.Count);
        Assert.Contains("small", FontSizeService.Sizes);
        Assert.Contains("medium", FontSizeService.Sizes);
        Assert.Contains("large", FontSizeService.Sizes);
    }

    [Fact]
    public void SmallConstant_IsSmall()
    {
        Assert.Equal("small", FontSizeService.Small);
    }

    [Fact]
    public void MediumConstant_IsMedium()
    {
        Assert.Equal("medium", FontSizeService.Medium);
    }

    [Fact]
    public void LargeConstant_IsLarge()
    {
        Assert.Equal("large", FontSizeService.Large);
    }
}
