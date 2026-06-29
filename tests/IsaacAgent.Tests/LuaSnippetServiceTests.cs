using IsaacAgent.App.Services;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for LuaSnippetService — snippet catalog.
/// </summary>
public class LuaSnippetServiceTests
{
    [Fact]
    public void Snippets_IsNotEmpty()
    {
        Assert.NotEmpty(LuaSnippetService.Snippets);
    }

    [Fact]
    public void Snippets_AllHaveName()
    {
        foreach (var s in LuaSnippetService.Snippets)
            Assert.False(string.IsNullOrEmpty(s.Name));
    }

    [Fact]
    public void Snippets_AllHaveCode()
    {
        foreach (var s in LuaSnippetService.Snippets)
            Assert.False(string.IsNullOrEmpty(s.Code));
    }

    [Fact]
    public void Snippets_AllHaveCategory()
    {
        foreach (var s in LuaSnippetService.Snippets)
            Assert.False(string.IsNullOrEmpty(s.Category));
    }

    [Fact]
    public void Snippets_ContainsCallbackCategory()
    {
        Assert.Contains(LuaSnippetService.Snippets, s => s.Category == "Callback");
    }

    [Fact]
    public void Snippets_ContainsUtilityCategory()
    {
        Assert.Contains(LuaSnippetService.Snippets, s => s.Category == "Utility");
    }

    [Fact]
    public void Snippets_ContainsEntityCategory()
    {
        Assert.Contains(LuaSnippetService.Snippets, s => s.Category == "Entity");
    }

    [Fact]
    public void Snippets_ContainsPostPEffectUpdate()
    {
        Assert.Contains(LuaSnippetService.Snippets, s => s.Name == "MC_POST_PEFFECT_UPDATE");
    }

    [Fact]
    public void Snippets_NamesAreUnique()
    {
        var names = LuaSnippetService.Snippets.Select(s => s.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
