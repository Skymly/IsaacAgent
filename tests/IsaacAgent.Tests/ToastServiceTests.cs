using Avalonia.Threading;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for ToastService and ToastNotification.
///   ToastService uses DispatcherTimer for auto-dismiss, so tests
///   use the Avalonia collection fixture and clean up timers in finally.
/// </summary>
[Collection("Avalonia")]
public class ToastServiceTests
{
    /// <summary>
    ///   Creates a ToastService with auto-dismiss disabled to prevent
    ///   DispatcherTimer/Task.Delay from blocking the headless test runner.
    /// </summary>
    private static ToastService CreateTestService()
    {
        return new ToastService { AutoDismissEnabled = false };
    }

    [Fact]
    public void ShowInfo_AddsToastToActiveToasts()
    {
        var service = CreateTestService();
        service.ShowInfo("Test info message");

        Assert.Single(service.ActiveToasts);
        Assert.Equal("Test info message", service.ActiveToasts[0].Message);
        Assert.Equal(ToastSeverity.Info, service.ActiveToasts[0].Severity);
    }

    [Fact]
    public void ShowSuccess_AddsSuccessToast()
    {
        var service = CreateTestService();
        service.ShowSuccess("Project loaded");

        Assert.Single(service.ActiveToasts);
        Assert.Equal(ToastSeverity.Success, service.ActiveToasts[0].Severity);
        Assert.Equal("Project loaded", service.ActiveToasts[0].Message);
    }

    [Fact]
    public void ShowWarning_AddsWarningToast()
    {
        var service = CreateTestService();
        service.ShowWarning("Deprecated API");

        Assert.Single(service.ActiveToasts);
        Assert.Equal(ToastSeverity.Warning, service.ActiveToasts[0].Severity);
    }

    [Fact]
    public void ShowError_AddsErrorToast()
    {
        var service = CreateTestService();
        service.ShowError("Build failed");

        Assert.Single(service.ActiveToasts);
        Assert.Equal(ToastSeverity.Error, service.ActiveToasts[0].Severity);
    }

    [Fact]
    public void Show_MultipleToasts_AllAddedToList()
    {
        var service = CreateTestService();
        service.ShowInfo("First");
        service.ShowSuccess("Second");
        service.ShowError("Third");

        Assert.Equal(3, service.ActiveToasts.Count);
    }

    [Fact(Skip = "Auto-dismiss uses Task.Delay + Dispatcher.Post which blocks headless test runner")]
    public async Task Show_AutoDismissesAfterDuration()
    {
        var service = new ToastService(); // auto-dismiss enabled
        service.ShowInfo("Temporary", durationMs: 50);
        Assert.Single(service.ActiveToasts);
        await Task.Delay(200);
        Assert.Empty(service.ActiveToasts);
    }

    [Fact]
    public void Dismiss_RemovesToastFromList()
    {
        var service = CreateTestService();
        service.ShowInfo("To be dismissed");
        var toast = service.ActiveToasts[0];

        service.Dismiss(toast);

        Assert.Empty(service.ActiveToasts);
    }

    [Fact]
    public void Dismiss_NonExistentToast_DoesNotThrow()
    {
        var service = CreateTestService();
        var externalToast = new ToastNotification("external");

        service.Dismiss(externalToast); // should not throw
    }

    [Fact]
    public void ToastNotification_DefaultDuration_Is4000Ms()
    {
        var toast = new ToastNotification("test");
        Assert.Equal(4000, toast.RemainingMs);
    }

    [Fact]
    public void ToastNotification_CustomDuration_SetsRemainingMs()
    {
        var toast = new ToastNotification("test", ToastSeverity.Warning, 8000);
        Assert.Equal(8000, toast.RemainingMs);
        Assert.Equal(ToastSeverity.Warning, toast.Severity);
    }

    [Fact]
    public void ToastNotification_Icon_MapsToSeverity()
    {
        Assert.Equal("ℹ", new ToastNotification("a", ToastSeverity.Info).Icon);
        Assert.Equal("✓", new ToastNotification("a", ToastSeverity.Success).Icon);
        Assert.Equal("⚠", new ToastNotification("a", ToastSeverity.Warning).Icon);
        Assert.Equal("✗", new ToastNotification("a", ToastSeverity.Error).Icon);
    }

    [Fact]
    public void ToastNotification_Message_SetCorrectly()
    {
        var toast = new ToastNotification("Hello world");
        Assert.Equal("Hello world", toast.Message);
    }
}
