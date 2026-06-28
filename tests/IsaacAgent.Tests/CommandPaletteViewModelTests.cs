using Avalonia;
using Avalonia.Headless;
using IsaacAgent.App.ViewModels;
using Xunit;

namespace IsaacAgent.Tests;

public class CommandPaletteViewModelTests
{
    static CommandPaletteViewModelTests()
    {
        try
        {
            AppBuilder.Configure<HeadlessApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
        }
        catch { /* Already initialized */ }
    }

    [Fact]
    public void Constructor_RegistersCommands()
    {
        var vm = new CommandPaletteViewModel();

        Assert.NotEmpty(vm.FilteredCommands);
    }

    [Fact]
    public void SearchText_Empty_ShowsAllCommands()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = ""
        };

        // Should show all commands (file, chat, project, help, skill categories)
        Assert.True(vm.FilteredCommands.Count > 10);
    }

    [Fact]
    public void SearchText_File_FiltersFileCommands()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "new project"
        };

        Assert.NotEmpty(vm.FilteredCommands);
        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("New Project"));
    }

    [Fact]
    public void SearchText_Skill_FiltersSkillCommands()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "create collectible"
        };

        Assert.NotEmpty(vm.FilteredCommands);
        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Create Collectible"));
    }

    [Fact]
    public void SearchText_FuzzyMatch_MatchesSubsequence()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "cc" // matches "Create Collectible"
        };

        // Fuzzy match: "cc" should match "Create Collectible" (c...c...)
        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Create Collectible"));
    }

    [Fact]
    public void SearchText_NoMatch_ReturnsEmpty()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "zzzzzzzzzznomatch"
        };

        Assert.Empty(vm.FilteredCommands);
    }

    [Fact]
    public void SearchText_CategoryMatch_FiltersByCategory()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "skill"
        };

        Assert.NotEmpty(vm.FilteredCommands);
        Assert.All(vm.FilteredCommands, c => Assert.True(c.Title.Contains("Create") || c.Title.Contains("Debug") || c.Title.Contains("Validate") || c.Title.Contains("Add")));
    }

    [Fact]
    public void SelectedCommand_SetToFirstResult_AfterFilter()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "settings"
        };

        Assert.NotNull(vm.SelectedCommand);
        Assert.Contains("Settings", vm.SelectedCommand!.Title);
    }

    [Fact]
    public void SelectedCommand_Null_WhenNoMatch()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "zzzznomatch"
        };

        Assert.Null(vm.SelectedCommand);
    }

    [Fact]
    public void SearchText_Debug_FindsDebugCommand()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "debug"
        };

        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Debug"));
    }

    [Fact]
    public void SearchText_Validate_FindsValidateCommand()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "validate"
        };

        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Validate"));
    }

    [Fact]
    public void SearchText_Add_FindsAllAddCommands()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "add"
        };

        // Should find Add Callback, Add Save Data, Add Trinket, Add Card, Add Pill, Add Boss
        var addCommands = vm.FilteredCommands.Where(c => c.Title.StartsWith("Add")).ToList();
        Assert.True(addCommands.Count >= 5);
    }

    [Fact]
    public void SearchText_Trinket_FindsAddTrinketCommand()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "trinket"
        };

        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Trinket"));
    }

    [Fact]
    public void SearchText_Boss_FindsAddBossCommand()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "boss"
        };

        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Boss"));
    }

    [Fact]
    public void CommandItem_DisplayShortcut_ReturnsShortcut()
    {
        var item = new CommandItem { Title = "Test", Shortcut = "/create-item" };
        Assert.Equal("/create-item", item.DisplayShortcut);
    }

    [Fact]
    public void CommandItem_DisplayShortcut_NullShortcut_ReturnsEmpty()
    {
        var item = new CommandItem { Title = "Test", Shortcut = null };
        Assert.Equal("", item.DisplayShortcut);
    }

    [Fact]
    public void SetCloseAction_DoesNotThrow()
    {
        var vm = new CommandPaletteViewModel();
        var wasCalled = false;
        vm.SetCloseAction(() => wasCalled = true);

        // Just verify it doesn't throw - we can't easily test ExecuteSelected
        // without a full DI setup
        Assert.False(wasCalled);
    }

    [Fact]
    public void SearchText_PartialMatch_FindsCommands()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "open"
        };

        Assert.Contains(vm.FilteredCommands, c => c.Title.Contains("Open"));
    }

    [Fact]
    public void SearchText_Clear_AfterSearch_ShowsAllCommands()
    {
        var vm = new CommandPaletteViewModel
        {
            SearchText = "settings"
        };
        Assert.NotEmpty(vm.FilteredCommands);

        vm.SearchText = "";
        Assert.True(vm.FilteredCommands.Count > 10);
    }
}
