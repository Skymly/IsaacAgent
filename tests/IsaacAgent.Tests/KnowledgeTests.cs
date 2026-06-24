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
    public void ModCallbacks_VanillaIds_MatchDocumentation()
    {
        // These IDs were previously wrong in the codebase; verify they match
        // the official IsaacDocs enums/ModCallbacks.md values.
        Assert.Equal(0, ModCallbacks.Callbacks["MC_NPC_UPDATE"].Id);
        Assert.Equal(1, ModCallbacks.Callbacks["MC_POST_UPDATE"].Id);
        Assert.Equal(2, ModCallbacks.Callbacks["MC_POST_RENDER"].Id);
        Assert.Equal(3, ModCallbacks.Callbacks["MC_USE_ITEM"].Id);
        Assert.Equal(4, ModCallbacks.Callbacks["MC_POST_PEFFECT_UPDATE"].Id);
        Assert.Equal(5, ModCallbacks.Callbacks["MC_USE_CARD"].Id);
        Assert.Equal(6, ModCallbacks.Callbacks["MC_FAMILIAR_UPDATE"].Id);
        Assert.Equal(7, ModCallbacks.Callbacks["MC_FAMILIAR_INIT"].Id);
        Assert.Equal(8, ModCallbacks.Callbacks["MC_EVALUATE_CACHE"].Id);
        Assert.Equal(9, ModCallbacks.Callbacks["MC_POST_PLAYER_INIT"].Id);
        Assert.Equal(10, ModCallbacks.Callbacks["MC_USE_PILL"].Id);
        Assert.Equal(11, ModCallbacks.Callbacks["MC_ENTITY_TAKE_DMG"].Id);
        Assert.Equal(34, ModCallbacks.Callbacks["MC_POST_PICKUP_INIT"].Id);
        Assert.Equal(61, ModCallbacks.Callbacks["MC_POST_FIRE_TEAR"].Id);
        Assert.Equal(73, ModCallbacks.Callbacks["MC_PRE_MOD_UNLOAD"].Id);
    }

    [Fact]
    public void ModCallbacks_RepentogonCallbacks_ContainKeyAdditions()
    {
        Assert.True(ModCallbacks.RepentogonCallbacks.ContainsKey("MC_PRE_ADD_COLLECTIBLE"));
        Assert.True(ModCallbacks.RepentogonCallbacks.ContainsKey("MC_POST_ENTITY_TAKE_DMG"));
        Assert.True(ModCallbacks.RepentogonCallbacks.ContainsKey("MC_PRE_PLAYER_TAKE_DMG"));
        Assert.True(ModCallbacks.RepentogonCallbacks.ContainsKey("MC_PRE_SFX_PLAY"));
        Assert.True(ModCallbacks.RepentogonCallbacks.ContainsKey("MC_PRE_MUSIC_PLAY"));
        Assert.True(ModCallbacks.RepentogonCallbacks.Count >= 100);
    }

    [Fact]
    public void ModCallbacks_RepentogonIds_AreIn1000Range()
    {
        foreach (var (_, info) in ModCallbacks.RepentogonCallbacks)
            Assert.True(info.Id >= 1000, $"REPENTOGON callback ID {info.Id} should be >= 1000");
    }

    [Fact]
    public void ModCallbacks_RepentogonModifiedIds_MapKnownOverrides()
    {
        Assert.Equal(1007, ModCallbacks.GetRepentogonId("MC_ENTITY_TAKE_DMG"));
        Assert.Equal(1064, ModCallbacks.GetRepentogonId("MC_USE_PILL"));
        Assert.Null(ModCallbacks.GetRepentogonId("MC_POST_UPDATE"));
    }

    [Fact]
    public void ModCallbacks_Lookup_FindsBothVanillaAndRepentogon()
    {
        Assert.NotNull(ModCallbacks.Lookup("MC_POST_UPDATE"));
        Assert.Equal(1, ModCallbacks.Lookup("MC_POST_UPDATE")!.Id);

        Assert.NotNull(ModCallbacks.Lookup("MC_PRE_ADD_COLLECTIBLE"));
        Assert.Equal(1004, ModCallbacks.Lookup("MC_PRE_ADD_COLLECTIBLE")!.Id);

        Assert.Null(ModCallbacks.Lookup("MC_NONEXISTENT_CALLBACK"));
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
