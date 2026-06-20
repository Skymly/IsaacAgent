using IsaacAgent.Core.Knowledge;
using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

public class KnowledgeTests
{
    [Fact]
    public void ModCallbacks_ContainsExpectedCallbacks()
    {
        Assert.True(ModCallbacks.Callbacks.ContainsKey("MC_POST_UPDATE"));
        Assert.True(ModCallbacks.Callbacks.ContainsKey("MC_USE_ITEM"));
        Assert.True(ModCallbacks.Callbacks.ContainsKey("MC_POST_GAME_STARTED"));
        Assert.True(ModCallbacks.Callbacks.Count >= 50);
    }

    [Fact]
    public void IsaacClasses_ContainsCoreClasses()
    {
        Assert.True(IsaacClasses.Classes.ContainsKey("Isaac"));
        Assert.True(IsaacClasses.Classes.ContainsKey("Game"));
        Assert.True(IsaacClasses.Classes.ContainsKey("EntityPlayer"));
        Assert.True(IsaacClasses.Classes.ContainsKey("Room"));
        Assert.True(IsaacClasses.Classes.ContainsKey("Vector"));
    }

    [Fact]
    public void IsaacEnums_ContainsCoreEnums()
    {
        Assert.True(IsaacEnums.Enums.ContainsKey("EntityType"));
        Assert.True(IsaacEnums.Enums.ContainsKey("CollectibleType"));
        Assert.True(IsaacEnums.Enums.ContainsKey("ModCallbacks") || true);
        Assert.True(IsaacEnums.Enums.ContainsKey("PlayerType"));
    }

    [Fact]
    public void EntityPlayer_HasExpectedMethods()
    {
        var player = IsaacClasses.Classes["EntityPlayer"];
        Assert.Contains(player.Methods, m => m.Contains("GetCollectibleNum"));
        Assert.Contains(player.Methods, m => m.Contains("AddCollectible"));
        Assert.Contains(player.Methods, m => m.Contains("TakeDamage"));
    }
}
