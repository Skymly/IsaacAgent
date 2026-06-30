using IsaacAgent.App.Services;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for LuaSnippetService — snippet catalog, custom
///   snippets, search filtering, and category grouping.
/// </summary>
public class LuaSnippetServiceTests
{
    private static LuaSnippetService CreateService() => new();

    // ── Built-in snippet catalog ───────────────────────────────

    [Fact]
    public void BuiltIn_IsNotEmpty()
    {
        Assert.NotEmpty(LuaSnippetService.BuiltInOnly);
    }

    [Fact]
    public void BuiltIn_AllHaveName()
    {
        foreach (var s in LuaSnippetService.BuiltInOnly)
            Assert.False(string.IsNullOrEmpty(s.Name));
    }

    [Fact]
    public void BuiltIn_AllHaveCode()
    {
        foreach (var s in LuaSnippetService.BuiltInOnly)
            Assert.False(string.IsNullOrEmpty(s.Code));
    }

    [Fact]
    public void BuiltIn_AllHaveCategory()
    {
        foreach (var s in LuaSnippetService.BuiltInOnly)
            Assert.False(string.IsNullOrEmpty(s.Category));
    }

    [Fact]
    public void BuiltIn_ContainsCallbackCategory()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Category == "Callback");
    }

    [Fact]
    public void BuiltIn_ContainsUtilityCategory()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Category == "Utility");
    }

    [Fact]
    public void BuiltIn_ContainsEntityCategory()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Category == "Entity");
    }

    [Fact]
    public void BuiltIn_ContainsRenderCategory()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Category == "Render");
    }

    [Fact]
    public void BuiltIn_ContainsSaveCategory()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Category == "Save");
    }

    [Fact]
    public void BuiltIn_ContainsPostPEffectUpdate()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Name == "MC_POST_PEFFECT_UPDATE");
    }

    [Fact]
    public void BuiltIn_ContainsPostRender()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Name == "MC_POST_RENDER");
    }

    [Fact]
    public void BuiltIn_ContainsSaveData()
    {
        Assert.Contains(LuaSnippetService.BuiltInOnly, s => s.Name == "Save data");
    }

    [Fact]
    public void BuiltIn_NamesAreUnique()
    {
        var names = LuaSnippetService.BuiltInOnly.Select(s => s.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void BuiltIn_HasAtLeast30Snippets()
    {
        Assert.True(LuaSnippetService.BuiltInOnly.Count >= 30,
            $"Expected at least 30 built-in snippets, got {LuaSnippetService.BuiltInOnly.Count}");
    }

    // ── Instance service ───────────────────────────────────────

    [Fact]
    public void Constructor_LoadsBuiltInSnippets()
    {
        var svc = CreateService();
        Assert.True(svc.Snippets.Count >= 30);
    }

    [Fact]
    public void FilteredSnippets_InitiallyContainsAll()
    {
        var svc = CreateService();
        Assert.Equal(svc.Snippets.Count, svc.FilteredSnippets.Count);
    }

    // ── Custom snippet management ──────────────────────────────

    [Fact]
    public void AddCustom_AddsToSnippets()
    {
        var svc = CreateService();
        var initialCount = svc.Snippets.Count;
        var uniqueName = $"TestSnippet_{Guid.NewGuid():N}";

        var result = svc.AddCustom(new LuaSnippet
        {
            Name = uniqueName,
            Category = "Test",
            Code = "print('test')",
            Description = "A test snippet"
        });

        Assert.True(result);
        Assert.Equal(initialCount + 1, svc.Snippets.Count);
        Assert.Contains(svc.Snippets, s => s.Name == uniqueName);

        // Cleanup
        svc.RemoveCustom(uniqueName);
    }

    [Fact]
    public void AddCustom_DuplicateName_ReturnsFalse()
    {
        var svc = CreateService();
        var name = $"DupTest_{Guid.NewGuid():N}";
        svc.AddCustom(new LuaSnippet
        {
            Name = name,
            Category = "Test",
            Code = "print('1')"
        });

        var result = svc.AddCustom(new LuaSnippet
        {
            Name = name,
            Category = "Test",
            Code = "print('2')"
        });

        Assert.False(result);

        // Cleanup
        svc.RemoveCustom(name);
    }

    [Fact]
    public void AddCustom_EmptyName_ReturnsFalse()
    {
        var svc = CreateService();
        var result = svc.AddCustom(new LuaSnippet { Name = "", Code = "print('x')" });
        Assert.False(result);
    }

    [Fact]
    public void AddCustom_EmptyCategory_DefaultsToCustom()
    {
        var svc = CreateService();
        var name = $"NoCat_{Guid.NewGuid():N}";
        svc.AddCustom(new LuaSnippet
        {
            Name = name,
            Category = "",
            Code = "print('x')"
        });

        var snippet = svc.Snippets.First(s => s.Name == name);
        Assert.Equal("Custom", snippet.Category);

        // Cleanup
        svc.RemoveCustom(name);
    }

    [Fact]
    public void AddCustom_SetsIsCustomFlag()
    {
        var svc = CreateService();
        var name = $"Flag_{Guid.NewGuid():N}";
        svc.AddCustom(new LuaSnippet
        {
            Name = name,
            Category = "Test",
            Code = "print('x')"
        });

        var snippet = svc.Snippets.First(s => s.Name == name);
        Assert.True(snippet.IsCustom);

        // Cleanup
        svc.RemoveCustom(name);
    }

    [Fact]
    public void RemoveCustom_RemovesCustomSnippet()
    {
        var svc = CreateService();
        var name = $"Remove_{Guid.NewGuid():N}";
        svc.AddCustom(new LuaSnippet
        {
            Name = name,
            Category = "Test",
            Code = "print('x')"
        });

        var result = svc.RemoveCustom(name);
        Assert.True(result);
        Assert.DoesNotContain(svc.Snippets, s => s.Name == name);
    }

    [Fact]
    public void RemoveCustom_BuiltInSnippet_ReturnsFalse()
    {
        var svc = CreateService();
        var builtInName = svc.Snippets.First(s => !s.IsCustom).Name;
        var result = svc.RemoveCustom(builtInName);
        Assert.False(result);
    }

    [Fact]
    public void RemoveCustom_NonExistent_ReturnsFalse()
    {
        var svc = CreateService();
        var result = svc.RemoveCustom("DoesNotExist_xyz");
        Assert.False(result);
    }

    [Fact]
    public void UpdateCustom_UpdatesFields()
    {
        var svc = CreateService();
        var name = $"Update_{Guid.NewGuid():N}";
        svc.AddCustom(new LuaSnippet
        {
            Name = name,
            Category = "Test",
            Code = "print('old')",
            Description = "old desc"
        });

        var result = svc.UpdateCustom(name, new LuaSnippet
        {
            Name = name,
            Category = "Updated",
            Code = "print('new')",
            Description = "new desc"
        });

        Assert.True(result);
        var snippet = svc.Snippets.First(s => s.Name == name);
        Assert.Equal("Updated", snippet.Category);
        Assert.Equal("print('new')", snippet.Code);
        Assert.Equal("new desc", snippet.Description);

        // Cleanup
        svc.RemoveCustom(name);
    }

    [Fact]
    public void UpdateCustom_BuiltInSnippet_ReturnsFalse()
    {
        var svc = CreateService();
        var builtInName = svc.Snippets.First(s => !s.IsCustom).Name;
        var result = svc.UpdateCustom(builtInName, new LuaSnippet
        {
            Name = builtInName,
            Category = "X",
            Code = "x"
        });
        Assert.False(result);
    }

    // ── Search filtering ───────────────────────────────────────

    [Fact]
    public void SearchText_FiltersByName()
    {
        var svc = CreateService();
        svc.SearchText = "MC_POST";
        Assert.NotEmpty(svc.FilteredSnippets);
        Assert.All(svc.FilteredSnippets, s =>
            Assert.Contains("MC_POST", s.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchText_FiltersByCategory()
    {
        var svc = CreateService();
        svc.SearchText = "Callback";
        Assert.NotEmpty(svc.FilteredSnippets);
        Assert.All(svc.FilteredSnippets, s =>
            Assert.True(
                s.Category.Contains("Callback", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains("Callback", StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains("Callback", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SearchText_NoMatch_EmptyFiltered()
    {
        var svc = CreateService();
        svc.SearchText = "xyz_nonexistent_search_query";
        Assert.Empty(svc.FilteredSnippets);
    }

    [Fact]
    public void SearchText_Cleared_RestoresAll()
    {
        var svc = CreateService();
        svc.SearchText = "MC_POST";
        Assert.NotEmpty(svc.FilteredSnippets);

        svc.SearchText = "";
        Assert.Equal(svc.Snippets.Count, svc.FilteredSnippets.Count);
    }

    [Fact]
    public void SearchText_CaseInsensitive()
    {
        var svc = CreateService();
        svc.SearchText = "callback";
        Assert.NotEmpty(svc.FilteredSnippets);
    }

    // ── Category grouping ──────────────────────────────────────

    [Fact]
    public void GroupedSnippets_HasMultipleCategories()
    {
        var svc = CreateService();
        Assert.True(svc.GroupedSnippets.Count >= 3);
    }

    [Fact]
    public void GroupedSnippets_AllSnippetsAccountedFor()
    {
        var svc = CreateService();
        var totalGrouped = svc.GroupedSnippets.Sum(g => g.Snippets.Count);
        Assert.Equal(svc.Snippets.Count, totalGrouped);
    }

    [Fact]
    public void GroupedSnippets_CategoriesAreSorted()
    {
        var svc = CreateService();
        var categories = svc.GroupedSnippets.Select(g => g.Category).ToList();
        var sorted = categories.OrderBy(c => c).ToList();
        Assert.Equal(sorted, categories);
    }
}
