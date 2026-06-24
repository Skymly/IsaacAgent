using Avalonia;
using Avalonia.Headless;
using IsaacAgent.App.ViewModels;
using Xunit;

namespace IsaacAgent.Tests;

public class ChatMessageViewModelTests
{
    static ChatMessageViewModelTests()
    {
        // Initialize Avalonia headless application once for all tests.
        // This enables DispatcherTimer and other UI-thread-dependent features.
        try
        {
            AppBuilder.Configure<HeadlessApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
        }
        catch { /* Already initialized */ }
    }

    [Fact]
    public void RoleLabel_User_ReturnsYou()
    {
        var vm = new ChatMessageViewModel { Role = "user" };
        Assert.Equal("You", vm.RoleLabel);
    }

    [Fact]
    public void RoleLabel_Assistant_ReturnsIsaacAgent()
    {
        var vm = new ChatMessageViewModel { Role = "assistant" };
        Assert.Equal("IsaacAgent", vm.RoleLabel);
    }

    [Fact]
    public void RoleLabel_Tool_ReturnsToolCall()
    {
        var vm = new ChatMessageViewModel { Role = "tool", ToolName = "read_file" };
        Assert.Equal("🔧 read_file", vm.RoleLabel);
    }

    [Fact]
    public void RoleLabel_ToolResult_ReturnsToolNameWithDuration()
    {
        var vm = new ChatMessageViewModel
        {
            Role = "tool_result",
            ToolName = "write_file",
            ToolDuration = TimeSpan.FromMilliseconds(150)
        };
        Assert.Contains("✅ write_file", vm.RoleLabel);
        Assert.Contains("150ms", vm.RoleLabel);
    }

    [Fact]
    public void IsTool_True_WhenRoleIsToolOrToolResult()
    {
        var vm1 = new ChatMessageViewModel { Role = "tool" };
        var vm2 = new ChatMessageViewModel { Role = "tool_result" };
        Assert.True(vm1.IsTool);
        Assert.True(vm2.IsTool);
    }

    [Fact]
    public void RoleLabel_Error_ReturnsError()
    {
        var vm = new ChatMessageViewModel { Role = "error" };
        Assert.Equal("Error", vm.RoleLabel);
    }

    [Fact]
    public void RoleLabel_Unknown_ReturnsRole()
    {
        var vm = new ChatMessageViewModel { Role = "custom" };
        Assert.Equal("custom", vm.RoleLabel);
    }

    [Fact]
    public void IsUser_True_WhenRoleIsUser()
    {
        var vm = new ChatMessageViewModel { Role = "user" };
        Assert.True(vm.IsUser);
        Assert.False(vm.IsAssistant);
    }

    [Fact]
    public void IsAssistant_True_WhenRoleIsAssistant()
    {
        var vm = new ChatMessageViewModel { Role = "assistant" };
        Assert.True(vm.IsAssistant);
        Assert.False(vm.IsUser);
    }

    [Fact]
    public void IsError_True_WhenRoleIsError()
    {
        var vm = new ChatMessageViewModel { Role = "error" };
        Assert.True(vm.IsError);
    }

    [Fact]
    public void IsSystem_True_WhenRoleIsSystem()
    {
        var vm = new ChatMessageViewModel { Role = "system" };
        Assert.True(vm.IsSystem);
    }

    [Fact]
    public void Content_Set_UpdatesDebouncedMarkdownImmediately_FirstTime()
    {
        var vm = new ChatMessageViewModel { Role = "assistant" };
        vm.Content = "Hello world";
        // First set should update DebouncedMarkdown immediately
        Assert.Equal("Hello world", vm.DebouncedMarkdown);
    }
}

/// <summary>
/// Minimal Avalonia application for headless testing.
/// </summary>
internal sealed class HeadlessApp : Avalonia.Application
{
    public override void Initialize() { }
}
