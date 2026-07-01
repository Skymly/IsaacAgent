using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace IsaacAgent.App.Services;

/// <summary>
///   A Lua code snippet for Isaac modding.
/// </summary>
public sealed class LuaSnippet
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>True if this is a user-defined snippet (not built-in).</summary>
    public bool IsCustom { get; set; }
}

/// <summary>
///   Manages Lua code snippets for Isaac modding. Provides built-in
///   snippets and supports user-defined custom snippets with
///   persistence, grouping by category, and search filtering.
/// </summary>
public sealed class LuaSnippetService
{
    /// <summary>All snippets (built-in + custom), observable for UI binding.</summary>
    public ObservableCollection<LuaSnippet> Snippets { get; } = [];

    /// <summary>Filtered snippets based on the current search query.</summary>
    public ObservableCollection<LuaSnippet> FilteredSnippets { get; } = [];

    private string _searchText = "";

    /// <summary>Search query for filtering snippets. Empty = show all.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            UpdateFiltered();
        }
    }

    /// <summary>Snippets grouped by category for UI display.</summary>
    public IReadOnlyList<SnippetCategoryGroup> GroupedSnippets =>
        Snippets
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key)
            .Select(g => new SnippetCategoryGroup { Category = g.Key, Snippets = g.ToList() })
            .ToList();

    private static string GetCustomSnippetsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IsaacAgent", "custom_snippets.json");

    /// <summary>
    ///   Built-in snippets that ship with the application.
    /// </summary>
    private static readonly IReadOnlyList<LuaSnippet> BuiltInSnippets =
    [
        // ── Callbacks ──────────────────────────────────────────
        new LuaSnippet
        {
            Name = "MC_POST_PEFFECT_UPDATE",
            Category = "Callback",
            Description = "Per-player effect update (every frame per player)",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_UPDATE",
            Category = "Callback",
            Description = "Global update (every frame)",
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
            Name = "MC_POST_RENDER",
            Category = "Render",
            Description = "Post-render (every frame after drawing)",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_RENDER, function() end)"""
        },
        new LuaSnippet
        {
            Name = "MC_PRE_RENDER",
            Category = "Render",
            Description = "Pre-render (every frame before drawing)",
            Code = """mod:AddCallback(ModCallbacks.MC_PRE_RENDER, function() end)"""
        },
        new LuaSnippet
        {
            Name = "MC_GET_CARD",
            Category = "Callback",
            Description = "Card/rune pickup",
            Code = """mod:AddCallback(ModCallbacks.MC_GET_CARD, function(_, card, isRune) end)"""
        },
        new LuaSnippet
        {
            Name = "MC_POST_ADD_COLLECTIBLE",
            Category = "Callback",
            Description = "After a collectible is added to player",
            Code = """mod:AddCallback(ModCallbacks.MC_POST_ADD_COLLECTIBLE, function(_, collectibleType, charge, firstTime, player, slot, varData) end)"""
        },

        // ── Entity ─────────────────────────────────────────────
        new LuaSnippet
        {
            Name = "Spawn entity",
            Category = "Entity",
            Description = "Spawn an entity at a position",
            Code = """Isaac.Spawn(EntityType.ENTITY_FLY, 0, 0, Vector(0, 0), Vector(0, 0), nil)"""
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
            Name = "Register collectible",
            Category = "Entity",
            Description = "Get a custom collectible type ID",
            Code = """local COLLECTIBLE_TYPE = Isaac.GetItemIdByName("MyCollectible")"""
        },
        new LuaSnippet
        {
            Name = "Register trinket",
            Category = "Entity",
            Description = "Get a custom trinket type ID",
            Code = """local TRINKET_TYPE = Isaac.GetTrinketIdByName("MyTrinket")"""
        },
        new LuaSnippet
        {
            Name = "Find entities in room",
            Category = "Entity",
            Description = "Get all entities of a type in the current room",
            Code = """local entities = Isaac.FindInRadius(player.Position, 100, EntityPartition.ALL)"""
        },
        new LuaSnippet
        {
            Name = "Get room entities",
            Category = "Entity",
            Description = "Get all entities in the current room",
            Code = """local entities = Isaac.GetRoomEntities()"""
        },

        // ── Render ─────────────────────────────────────────────
        new LuaSnippet
        {
            Name = "Render text",
            Category = "Render",
            Description = "Draw text on screen",
            Code = """Isaac.RenderText("Hello", 100, 100, 255, 255, 255, 255)"""
        },
        new LuaSnippet
        {
            Name = "Load sprite",
            Category = "Render",
            Description = "Load and play a sprite animation",
            Code = """local sprite = Sprite() sprite:Load("gfx/foo.anm2", true) sprite:Play("Idle", true)"""
        },
        new LuaSnippet
        {
            Name = "Render vector",
            Category = "Render",
            Description = "Get screen center position",
            Code = """local center = Isaac.WorldToScreen(Vector(320, 280))"""
        },

        // ── Save Data ──────────────────────────────────────────
        new LuaSnippet
        {
            Name = "Save data",
            Category = "Save",
            Description = "Save mod data to file",
            Code = """Isaac.SaveModData(mod, "mydata")"""
        },
        new LuaSnippet
        {
            Name = "Load data",
            Category = "Save",
            Description = "Load mod data from file",
            Code = """local data = Isaac.LoadModData(mod)"""
        },
        new LuaSnippet
        {
            Name = "Has data",
            Category = "Save",
            Description = "Check if saved data exists",
            Code = """Isaac.HasModData(mod)"""
        },
        new LuaSnippet
        {
            Name = "Remove data",
            Category = "Save",
            Description = "Remove saved mod data",
            Code = """Isaac.RemoveModData(mod)"""
        },

        // ── Utility ────────────────────────────────────────────
        new LuaSnippet
        {
            Name = "Include",
            Category = "Utility",
            Description = "Include another Lua file",
            Code = """include("path/to/file.lua")"""
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
            Name = "Get game",
            Category = "Utility",
            Description = "Get the Game instance",
            Code = """local game = Game()"""
        },
        new LuaSnippet
        {
            Name = "Get level",
            Category = "Utility",
            Description = "Get the current level",
            Code = """local level = game:GetLevel()"""
        },
        new LuaSnippet
        {
            Name = "Get room",
            Category = "Utility",
            Description = "Get the current room",
            Code = """local room = game:GetRoom()"""
        },
        new LuaSnippet
        {
            Name = "Random number",
            Category = "Utility",
            Description = "Get a random integer",
            Code = """local rng = RNG() local val = rng:RandomInt(100)"""
        },
        new LuaSnippet
        {
            Name = "Register mod",
            Category = "Utility",
            Description = "Register the mod table",
            Code = """local mod = RegisterMod("MyModName", 1)"""
        },
        new LuaSnippet
        {
            Name = "Schedule callback",
            Category = "Utility",
            Description = "Run a callback after N frames",
            Code = """Isaac.ScheduleCallback(30, function() end)"""
        },
    ];

    public LuaSnippetService()
    {
        // Load built-in snippets (in-memory, instant)
        foreach (var s in BuiltInSnippets)
            Snippets.Add(s);

        // Load custom snippets synchronously. The file is small
        // (a handful of user snippets) and ObservableCollection is
        // not thread-safe, so loading on a background thread caused
        // race conditions where Snippets.Count and GroupedSnippets
        // could observe different states.
        LoadCustomSnippets();
        UpdateFiltered();
    }

    // ── Static accessor for built-in snippets (backward compat) ──

    /// <summary>
    ///   Static access to built-in snippets only (for tests and
    ///   backward compatibility).
    /// </summary>
    public static IReadOnlyList<LuaSnippet> BuiltInOnly => BuiltInSnippets;

    // ── Custom snippet management ──────────────────────────────

    /// <summary>
    ///   Add a custom snippet. Returns false if a snippet with the
    ///   same name already exists.
    /// </summary>
    public bool AddCustom(LuaSnippet snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet.Name)) return false;
        if (Snippets.Any(s => s.Name.Equals(snippet.Name, StringComparison.OrdinalIgnoreCase)))
            return false;

        snippet.IsCustom = true;
        if (string.IsNullOrEmpty(snippet.Category))
            snippet.Category = "Custom";
        Snippets.Add(snippet);
        SaveCustomSnippets();
        UpdateFiltered();
        return true;
    }

    /// <summary>
    ///   Remove a custom snippet by name. Built-in snippets cannot
    ///   be removed. Returns true if removed.
    /// </summary>
    public bool RemoveCustom(string name)
    {
        var snippet = Snippets.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && s.IsCustom);
        if (snippet is null) return false;
        Snippets.Remove(snippet);
        SaveCustomSnippets();
        UpdateFiltered();
        return true;
    }

    /// <summary>
    ///   Update an existing custom snippet. Returns false if not
    ///   found or the snippet is built-in.
    /// </summary>
    public bool UpdateCustom(string name, LuaSnippet updated)
    {
        var snippet = Snippets.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && s.IsCustom);
        if (snippet is null) return false;

        snippet.Name = updated.Name;
        snippet.Category = string.IsNullOrEmpty(updated.Category) ? "Custom" : updated.Category;
        snippet.Code = updated.Code;
        snippet.Description = updated.Description;
        SaveCustomSnippets();
        UpdateFiltered();
        return true;
    }

    // ── Persistence ────────────────────────────────────────────

    private void LoadCustomSnippets()
    {
        try
        {
            var path = GetCustomSnippetsPath();
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var custom = JsonSerializer.Deserialize<List<LuaSnippet>>(json);
            if (custom is null) return;
            foreach (var s in custom)
            {
                s.IsCustom = true;
                if (string.IsNullOrEmpty(s.Category))
                    s.Category = "Custom";
                Snippets.Add(s);
            }
        }
        catch
        {
            // Ignore load errors — custom snippets just won't be available
        }
    }

    private void SaveCustomSnippets()
    {
        try
        {
            var path = GetCustomSnippetsPath();
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);
            var custom = Snippets.Where(s => s.IsCustom).ToList();
            var json = JsonSerializer.Serialize(custom, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore save errors — custom snippets won't persist
        }
    }

    // ── Search / filter ────────────────────────────────────────

    private void UpdateFiltered()
    {
        FilteredSnippets.Clear();
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            foreach (var s in Snippets)
                FilteredSnippets.Add(s);
            return;
        }

        var query = _searchText.ToLowerInvariant();
        foreach (var s in Snippets)
        {
            if (s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSnippets.Add(s);
            }
        }
    }
}

/// <summary>
///   A group of snippets sharing the same category, for grouped UI display.
/// </summary>
public sealed class SnippetCategoryGroup
{
    public string Category { get; init; } = "";
    public IReadOnlyList<LuaSnippet> Snippets { get; init; } = [];
}
