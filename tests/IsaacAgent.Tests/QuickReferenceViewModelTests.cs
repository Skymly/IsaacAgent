using IsaacAgent.App.ViewModels;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for QuickReferenceViewModel — verifies that the
///   quick reference panel loads callbacks, classes, and mod structure
///   from the static knowledge base.
/// </summary>
public class QuickReferenceViewModelTests
{
    [Fact]
    public void Constructor_LoadsCallbacks()
    {
        var vm = new QuickReferenceViewModel();
        Assert.NotEmpty(vm.Callbacks);
        // Common callbacks should be present
        Assert.Contains(vm.Callbacks, c => c == "MC_POST_GAME_STARTED");
        Assert.Contains(vm.Callbacks, c => c == "MC_POST_UPDATE");
    }

    [Fact]
    public void Constructor_LoadsClasses()
    {
        var vm = new QuickReferenceViewModel();
        Assert.NotEmpty(vm.Classes);
        // Isaac class should be present
        Assert.Contains(vm.Classes, c => c == "Isaac");
    }

    [Fact]
    public void Constructor_LoadsModStructure()
    {
        var vm = new QuickReferenceViewModel();
        Assert.NotEmpty(vm.ModStructure);
        Assert.Contains(vm.ModStructure, s => s == "main.lua");
        Assert.Contains(vm.ModStructure, s => s == "metadata.xml");
        Assert.Contains(vm.ModStructure, s => s == "resources/");
    }

    [Fact]
    public void Callbacks_ContainAllCommonCallbacks()
    {
        var vm = new QuickReferenceViewModel();
        // Common callbacks are added first (not sorted), then remaining sorted
        var common = new[] { "MC_POST_GAME_STARTED", "MC_POST_UPDATE", "MC_USE_ITEM" };
        foreach (var c in common)
            Assert.Contains(c, vm.Callbacks);
    }

    [Fact]
    public void Classes_AreSorted()
    {
        var vm = new QuickReferenceViewModel();
        var sorted = vm.Classes.OrderBy(c => c).ToList();
        Assert.Equal(sorted, vm.Classes.ToList());
    }

    [Fact]
    public void Callbacks_IncludeRepentogonCallbacks()
    {
        var vm = new QuickReferenceViewModel();
        // REPENTOGON callbacks should be included (they have different prefix patterns)
        // Just verify there are a reasonable number
        Assert.True(vm.Callbacks.Count > 50, $"Expected >50 callbacks, got {vm.Callbacks.Count}");
    }

    [Fact]
    public void ModStructure_HasExpectedCount()
    {
        var vm = new QuickReferenceViewModel();
        Assert.Equal(7, vm.ModStructure.Count);
    }

    [Fact]
    public void Callbacks_NoDuplicates()
    {
        var vm = new QuickReferenceViewModel();
        var distinct = vm.Callbacks.Distinct().Count();
        Assert.Equal(vm.Callbacks.Count, distinct);
    }

    [Fact]
    public void Classes_NoDuplicates()
    {
        var vm = new QuickReferenceViewModel();
        var distinct = vm.Classes.Distinct().Count();
        Assert.Equal(vm.Classes.Count, distinct);
    }
}
