using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

public class SearchApiTests
{
    private readonly SearchApiTool _tool = new();

    [Fact]
    public async Task Search_Player_ReturnsClassInfo()
    {
        var result = await _tool.ExecuteAsync("""{"query": "EntityPlayer", "category": "class"}""");
        Assert.Contains("EntityPlayer", result);
        Assert.Contains("AddCollectible", result);
    }

    [Fact]
    public async Task Search_Callback_ReturnsCallbackInfo()
    {
        var result = await _tool.ExecuteAsync("""{"query": "MC_POST_UPDATE", "category": "callback"}""");
        Assert.Contains("MC_POST_UPDATE", result);
        Assert.Contains("update frame", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_Enum_ReturnsEnumValues()
    {
        var result = await _tool.ExecuteAsync("""{"query": "EntityType", "category": "enum"}""");
        Assert.Contains("EntityType", result);
        Assert.Contains("ENTITY_PLAYER", result);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmptyMessage()
    {
        var result = await _tool.ExecuteAsync("""{"query": "xyznonexistent"}""");
        Assert.Contains("No results", result, StringComparison.OrdinalIgnoreCase);
    }
}
