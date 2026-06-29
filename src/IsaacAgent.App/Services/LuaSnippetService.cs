namespace IsaacAgent.App.Services;

/// <summary>
///   Provides quick-insert Lua code snippets for Isaac modding.
/// </summary>
public sealed class LuaSnippet
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
}

/// <summary>
///   Static catalog of common Lua snippets for Binding of Isaac modding.
/// </summary>
public static class LuaSnippetService
{
    public static IReadOnlyList<LuaSnippet> Snippets { get; } =
    [
        new LuaSnippet
        {
            Name = "MC_POST_PEFFECT_UPDATE",
            Category = "Callback",
            Description = "Per-player effect update (runs every frame for each player)",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_UPDATE",
            Category = "Callback",
            Description = "Global update (runs every frame)",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function() end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_PLAYER_INIT",
            Category = "Callback",
            Description = "Player initialization",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_PLAYER_INIT, function(_, player) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_USE_ITEM",
            Category = "Callback",
            Description = "Active item use",
            Code = """mod:AddCallback(ModCallbacks.MC_USE_ITEM, function(_, item, rng, player, useFlags, activeSlot, customVarData) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_PICKUP_INIT",
            Category = "Callback",
            Description = "Pickup initialization",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_PICKUP_INIT, function(_, pickup) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_TEAR_INIT",
            Category = "Callback",
            Description = "Tear initialization",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_TEAR_INIT, function(_, tear) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_FAMILIAR_INIT",
            Category = "Callback",
            Description = "Familiar initialization",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_FAMILIAR_INIT, function(_, fam) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_NPC_DEATH",
            Category = "Callback",
            Description = "NPC death",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc) end)"""
        },
        new LuaSnippet
        {
            Name = "Include",
            Category = "Utility",
            Description = "Include another Lua file",
            Code = """include("path/to/file.lua")"""
        },
        new LuaSnippet
        {
            Name = "Register familiar",
            Category = "Entity",
            Description = "Register a custom familiar variant",
            Code = """local FAMILIAR_VARIANT = Isaac.GetEntityVariantByName("MyFamiliar")"""
        },
        new LuaSnippet
        {
            Name = "Get player",
            Category = "Utility",
            Description = "Get player 1",
            Code = """local player = Isaac.GetPlayer(0)"""
        },
        new LuaSnippet
        {
            Name = "Spawn entity",
            Category = "Entity",
            Description = "Spawn an entity at a position",
            Code = """Isaac.Spawn(EntityType.ENTITY_FLY, 0, 0, Vector(0, 0), Vector(0, 0), nil)"""
        }
    ];
}
