using System.Reflection;
using Xunit;

namespace IsaacAgent.UiTests;

/// <summary>
/// PublishSmoke trait contract — A→B only; Chat/Settings stay on build-output UiTest (issue #88).
/// </summary>
public sealed class PublishSmokeTraitTests
{
    private const string TraitName = "FlaUI";
    private const string TraitValue = "PublishSmoke";

    [Fact]
    public void ColdStartAndOpenProject_CarryPublishSmokeTrait()
    {
        Assert.True(HasPublishSmoke(typeof(ColdStartSmokeTests), nameof(ColdStartSmokeTests.Launch_ReleaseApp_MainWindowAppears_ThenCleanExit)));
        Assert.True(HasPublishSmoke(typeof(OpenProjectSmokeTests), nameof(OpenProjectSmokeTests.Launch_WithProject_StatusOrTreeShowsFixture_ThenCleanExit)));
    }

    [Fact]
    public void ChatAndSettingsAndOrphanCleanup_DoNotCarryPublishSmokeTrait()
    {
        Assert.False(HasPublishSmoke(typeof(ChatSmokeTests), nameof(ChatSmokeTests.Launch_ReleaseApp_ChatInputAppears_ThenCleanExit)));
        Assert.False(HasPublishSmoke(typeof(SettingsSmokeTests), nameof(SettingsSmokeTests.Launch_OpenSettingsViaFileMenu_ThenCloseSettings_ThenCleanExit)));
        Assert.False(HasPublishSmoke(typeof(OrphanProcessCleanupTests), nameof(OrphanProcessCleanupTests.Dispose_WithoutCleanClose_KillsLaunchedAppProcess)));
    }

    private static bool HasPublishSmoke(Type type, string methodName)
    {
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Method {type.Name}.{methodName} not found.");

        return method.CustomAttributes
            .Where(a => a.AttributeType == typeof(TraitAttribute))
            .Any(a =>
                a.ConstructorArguments.Count == 2 &&
                Equals(a.ConstructorArguments[0].Value, TraitName) &&
                Equals(a.ConstructorArguments[1].Value, TraitValue));
    }
}
