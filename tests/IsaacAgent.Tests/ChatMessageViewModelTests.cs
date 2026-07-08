using Avalonia.Headless.XUnit;
using IsaacAgent.App.ViewModels;
using Xunit;

namespace IsaacAgent.Tests;

[Collection("Avalonia")]
public class ChatMessageViewModelTests
{

    [AvaloniaFact]
    public void RoleLabel_User_ReturnsYou()
    {
        var vm = new ChatMessageViewModel { Role = "user" };
        Assert.Equal("You", vm.RoleLabel);
    }

    [AvaloniaFact]
    public void RoleLabel_Assistant_ReturnsIsaacAgent()
    {
        var vm = new ChatMessageViewModel { Role = "assistant" };
        Assert.Equal("IsaacAgent", vm.RoleLabel);
    }

    [AvaloniaFact]
    public void RoleLabel_Tool_ReturnsToolCall()
    {
        var vm = new ChatMessageViewModel { Role = "tool", ToolName = "read_file" };
        Assert.Equal("🔧 read_file", vm.RoleLabel);
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void IsTool_True_WhenRoleIsToolOrToolResult()
    {
        var vm1 = new ChatMessageViewModel { Role = "tool" };
        var vm2 = new ChatMessageViewModel { Role = "tool_result" };
        Assert.True(vm1.IsTool);
        Assert.True(vm2.IsTool);
    }

    [AvaloniaFact]
    public void RoleLabel_Error_ReturnsError()
    {
        var vm = new ChatMessageViewModel { Role = "error" };
        Assert.Equal("Error", vm.RoleLabel);
    }

    [AvaloniaFact]
    public void RoleLabel_Unknown_ReturnsRole()
    {
        var vm = new ChatMessageViewModel { Role = "custom" };
        Assert.Equal("custom", vm.RoleLabel);
    }

    [AvaloniaFact]
    public void IsUser_True_WhenRoleIsUser()
    {
        var vm = new ChatMessageViewModel { Role = "user" };
        Assert.True(vm.IsUser);
        Assert.False(vm.IsAssistant);
    }

    [AvaloniaFact]
    public void IsAssistant_True_WhenRoleIsAssistant()
    {
        var vm = new ChatMessageViewModel { Role = "assistant" };
        Assert.True(vm.IsAssistant);
        Assert.False(vm.IsUser);
    }

    [AvaloniaFact]
    public void IsError_True_WhenRoleIsError()
    {
        var vm = new ChatMessageViewModel { Role = "error" };
        Assert.True(vm.IsError);
    }

    [AvaloniaFact]
    public void IsSystem_True_WhenRoleIsSystem()
    {
        var vm = new ChatMessageViewModel { Role = "system" };
        Assert.True(vm.IsSystem);
    }

    [AvaloniaFact]
    public void Content_Set_UpdatesDebouncedMarkdownImmediately_FirstTime()
    {
        var vm = new ChatMessageViewModel { Role = "assistant" };
        vm.Content = "Hello world";
        // First set should update DebouncedMarkdown immediately
        Assert.Equal("Hello world", vm.DebouncedMarkdown);
    }

    [AvaloniaFact]
    public void ToolDurationLabel_MillisecondsFormat_WhenUnderOneSecond()
    {
        var vm = new ChatMessageViewModel { ToolDuration = TimeSpan.FromMilliseconds(500) };
        Assert.Equal("500ms", vm.ToolDurationLabel);
    }

    [AvaloniaFact]
    public void ToolDurationLabel_SecondsFormat_WhenOverOneSecond()
    {
        var vm = new ChatMessageViewModel { ToolDuration = TimeSpan.FromSeconds(2.5) };
        Assert.Equal("2.5s", vm.ToolDurationLabel);
    }

    [AvaloniaFact]
    public void ToolArgsPreview_TruncatesAt80Chars()
    {
        var vm = new ChatMessageViewModel { Content = new string('x', 100) };
        Assert.EndsWith("...", vm.ToolArgsPreview);
        Assert.Equal(83, vm.ToolArgsPreview.Length); // 80 + "..."
    }

    [AvaloniaFact]
    public void ToolArgsPreview_EmptyContent_ReturnsEmpty()
    {
        var vm = new ChatMessageViewModel { Content = "" };
        Assert.Equal("", vm.ToolArgsPreview);
    }

    [AvaloniaFact]
    public void ToolArgsPreview_Exactly80Chars_NoTruncation()
    {
        var vm = new ChatMessageViewModel { Content = new string('x', 80) };
        Assert.Equal(80, vm.ToolArgsPreview.Length);
        Assert.DoesNotContain("...", vm.ToolArgsPreview);
    }

    [AvaloniaFact]
    public void IsExpanded_TogglesCorrectly()
    {
        var vm = new ChatMessageViewModel { IsExpanded = false };
        vm.IsExpanded = true;
        Assert.True(vm.IsExpanded);
        vm.IsExpanded = false;
        Assert.False(vm.IsExpanded);
    }
}
