using IsaacAgent.App.Services;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// CLI --project parsing seam for UI E2E launch (issue #79).
/// </summary>
public class AppLaunchArgsTests
{
    [Fact]
    public void TryGetProjectPath_WithProjectAndPath_ReturnsPath()
    {
        var path = AppLaunchArgs.TryGetProjectPath(["--project", @"C:\mods\MyMod"]);
        Assert.Equal(@"C:\mods\MyMod", path);
    }

    [Fact]
    public void TryGetProjectPath_CaseInsensitiveFlag_ReturnsPath()
    {
        var path = AppLaunchArgs.TryGetProjectPath(["--PROJECT", @"D:\fixture"]);
        Assert.Equal(@"D:\fixture", path);
    }

    [Fact]
    public void TryGetProjectPath_Omitted_ReturnsNull()
    {
        Assert.Null(AppLaunchArgs.TryGetProjectPath([]));
        Assert.Null(AppLaunchArgs.TryGetProjectPath(["--verify-onnx"]));
    }

    [Fact]
    public void TryGetProjectPath_FlagWithoutValue_ReturnsNull()
    {
        Assert.Null(AppLaunchArgs.TryGetProjectPath(["--project"]));
    }

    [Fact]
    public void TryGetProjectPath_FlagFollowedByAnotherFlag_ReturnsNull()
    {
        Assert.Null(AppLaunchArgs.TryGetProjectPath(["--project", "--verify-onnx"]));
    }

    [Fact]
    public void TryGetProjectPath_MixedWithOtherArgs_ReturnsPath()
    {
        var path = AppLaunchArgs.TryGetProjectPath(
            ["--verify-onnx", "--project", @"C:\mods\A", "extra"]);
        Assert.Equal(@"C:\mods\A", path);
    }
}
