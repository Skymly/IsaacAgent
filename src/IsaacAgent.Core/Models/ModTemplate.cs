namespace IsaacAgent.Core.Models;

/// <summary>
/// Defines a mod template with file contents and metadata.
/// Used by the Template Gallery to scaffold new projects.
/// </summary>
public sealed class ModTemplate
{
    /// <summary>Display name shown in the gallery.</summary>
    public string Name { get; init; } = "";

    /// <summary>Short description of what the template provides.</summary>
    public string Description { get; init; } = "";

    /// <summary>Category tag (e.g. "Item", "Entity", "Challenge").</summary>
    public string Category { get; init; } = "";

    /// <summary>Relative paths and content for files to generate.</summary>
    public Dictionary<string, string> Files { get; init; } = [];

    /// <summary>Directory paths to create (relative to project root).</summary>
    public List<string> Directories { get; init; } = [];

    /// <summary>Preview code snippet shown in the gallery.</summary>
    public string Preview { get; init; } = "";
}

/// <summary>
/// Built-in mod templates for the Template Gallery.
/// </summary>
public static class ModTemplates
{
    public static IReadOnlyList<ModTemplate> All { get; } =
    [
        new ModTemplate
        {
            Name = "Basic Mod",
            Description = "Minimal mod with main.lua and metadata.xml. Best starting point.",
            Category = "Basic",
            Preview = """
                local mod = RegisterMod("MyMod", 1)

                function mod:onGameStart(isSave)
                    -- Called when a new game starts
                end

                mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED,
                    function(_, isSave) mod:onGameStart(isSave) end)
                """,
            Files = new()
            {
                ["main.lua"] = """
                    local mod = RegisterMod("{name}", 1)

                    function mod:onGameStart(isSave)
                        -- Called when a new game starts or is continued
                    end

                    mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
                        mod:onGameStart(isSave)
                    end)

                    -- Add your callbacks here

                    print("{name} loaded!")
                    """,
                ["metadata.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <metadata>
                        <name>{name}</name>
                        <description>{description}</description>
                        <author>{author}</author>
                        <version>1.0</version>
                        <apiVersion>1</apiVersion>
                    </metadata>
                    """
            },
            Directories = ["resources/gfx", "resources/scripts"]
        },

        new ModTemplate
        {
            Name = "Collectible Item Mod",
            Description = "Mod with a custom collectible item (passive + active) and items.xml.",
            Category = "Item",
            Preview = """
                local mod = RegisterMod("ItemMod", 1)

                -- Custom passive item callback
                mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE,
                    function(_, player)
                        if player:HasCollectible(CollectibleType.COLLECTIBLE_CUSTOM_PASSIVE) then
                            -- Apply passive effect each frame
                        end
                    end)
                """,
            Files = new()
            {
                ["main.lua"] = """
                    local mod = RegisterMod("{name}", 1)

                    -- Collectible IDs (must match items.xml)
                    local Collectible = {
                        CUSTOM_PASSIVE = Isaac.GetItemIdByName("Custom Passive Item"),
                        CUSTOM_ACTIVE = Isaac.GetItemIdByName("Custom Active Item"),
                    }

                    -- Passive item: apply effect while held
                    mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
                        if player:HasCollectible(Collectible.CUSTOM_PASSIVE) then
                            -- TODO: Apply your passive effect here
                            -- Example: player.DamageMultiplier = player.DamageMultiplier * 1.1
                        end
                    end)

                    -- Active item: triggered on use
                    mod:AddCallback(ModCallbacks.MC_USE_ITEM, function(_, item, rng, player, useFlags)
                        if item == Collectible.CUSTOM_ACTIVE then
                            -- TODO: Apply your active item effect here
                            -- Example: player:AddSoulHearts(2)
                            return true -- Show use animation
                        end
                    end)

                    print("{name} loaded!")
                    """,
                ["metadata.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <metadata>
                        <name>{name}</name>
                        <description>{description}</description>
                        <author>{author}</author>
                        <version>1.0</version>
                        <apiVersion>1</apiVersion>
                    </metadata>
                    """,
                ["items.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <items gfxroot="gfx/items/" lastgottenid="2000">
                        <active name="Custom Active Item"
                                description="A custom active item"
                                gfx="collectibles/001_sadonion.png"
                                maxcharges="2"
                                cache="damage"
                                quality="3" />
                        <passive name="Custom Passive Item"
                                 description="A custom passive item"
                                 gfx="collectibles/002_innereye.png"
                                 cache="damage"
                                 quality="2" />
                    </items>
                    """
            },
            Directories = ["resources/gfx/collectibles", "resources/scripts"]
        },

        new ModTemplate
        {
            Name = "Familiar Mod",
            Description = "Mod with a custom familiar entity that follows the player.",
            Category = "Entity",
            Preview = """
                local mod = RegisterMod("FamiliarMod", 1)

                -- Familiar follow callback
                mod:AddCallback(ModCallbacks.MC_FAMILIAR_UPDATE,
                    function(_, familiar)
                        local player = familiar.Player
                        local target = player.Position
                        familiar:FollowPosition(target)
                    end, familiarVariant)
                """,
            Files = new()
            {
                ["main.lua"] = """
                    local mod = RegisterMod("{name}", 1)

                    -- Get familiar variant from entities2.xml
                    local FAMILIAR_VARIANT = Isaac.GetEntityVariantByName("Custom Familiar")

                    -- Spawn familiar when player picks up the item
                    mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
                        if player:HasCollectible(Isaac.GetItemIdByName("Familiar Item")) then
                            -- Check if familiar already exists
                            local hasFamiliar = false
                            for _, familiar in ipairs(Isaac.FindByType(EntityType.ENTITY_FAMILIAR, FAMILIAR_VARIANT)) do
                                if familiar:GetData().Owner == player.InitSeed then
                                    hasFamiliar = true
                                    break
                                end
                            end
                            if not hasFamiliar then
                                local familiar = Isaac.Spawn(EntityType.ENTITY_FAMILIAR, FAMILIAR_VARIANT, 0, player.Position, Vector.Zero, player)
                                familiar:GetData().Owner = player.InitSeed
                            end
                        end
                    end)

                    -- Familiar AI: follow the player
                    mod:AddCallback(ModCallbacks.MC_FAMILIAR_UPDATE, function(_, familiar)
                        local player = familiar.Player
                        if player then
                            familiar:FollowPosition(player.Position)
                        end
                    end, FAMILIAR_VARIANT)

                    print("{name} loaded!")
                    """,
                ["metadata.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <metadata>
                        <name>{name}</name>
                        <description>{description}</description>
                        <author>{author}</author>
                        <version>1.0</version>
                        <apiVersion>1</apiVersion>
                    </metadata>
                    """,
                ["items.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <items gfxroot="gfx/items/" lastgottenid="2000">
                        <passive name="Familiar Item"
                                 description="Summons a custom familiar"
                                 gfx="collectibles/001_sadonion.png"
                                 cache="familiars"
                                 quality="2" />
                    </items>
                    """,
                ["entities2.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <entities anm2root="gfx/" lastid="950">
                        <entity name="Custom Familiar"
                                ID="950"
                                Type="3"
                                Variant="1000"
                                SubType="0"
                                gfx="familiars/custom_familiar.anm2"
                                friction="1"
                                shadow-size="12"
                                tags="familiar" />
                    </entities>
                    """
            },
            Directories = ["resources/gfx/collectibles", "resources/gfx/familiars", "resources/scripts"]
        },

        new ModTemplate
        {
            Name = "Challenge Mod",
            Description = "Mod that adds a custom challenge run with specific starting items.",
            Category = "Challenge",
            Preview = """
                local mod = RegisterMod("ChallengeMod", 1)

                -- Give starting items on challenge begin
                mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED,
                    function(_, isSave)
                        if not isSave then
                            local player = Isaac.GetPlayer(0)
                            player:AddCollectible(CollectibleType.COLLECTIBLE_SAD_ONION)
                        end
                    end)
                """,
            Files = new()
            {
                ["main.lua"] = """
                    local mod = RegisterMod("{name}", 1)

                    -- Challenge ID from challenges.xml
                    local CHALLENGE_ID = Isaac.GetChallengeIdByName("Custom Challenge")

                    -- Give starting items when the challenge begins
                    mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
                        if not isSave and Isaac.GetChallenge() == CHALLENGE_ID then
                            local player = Isaac.GetPlayer(0)
                            -- TODO: Add your starting items here
                            -- player:AddCollectible(CollectibleType.COLLECTIBLE_SAD_ONION)
                        end
                    end)

                    -- Modify gameplay during the challenge
                    mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
                        if Isaac.GetChallenge() == CHALLENGE_ID then
                            -- TODO: Add per-room modifications
                        end
                    end)

                    print("{name} loaded!")
                    """,
                ["metadata.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <metadata>
                        <name>{name}</name>
                        <description>{description}</description>
                        <author>{author}</author>
                        <version>1.0</version>
                        <apiVersion>1</apiVersion>
                    </metadata>
                    """,
                ["challenges.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <challenges>
                        <challenge name="Custom Challenge"
                                   achievements="0"
                                   players="1"
                                   items="1"
                                   noitems="false"
                                   notrinkets="false"
                                   nocards="false"
                                   nopills="false"
                                   difficulty="0"
                                   nameimage="custom_challenge.png"
                                   rulesimage="custom_challenge_rules.png" />
                    </challenges>
                    """
            },
            Directories = ["resources/gfx", "resources/scripts"]
        },

        new ModTemplate
        {
            Name = "Save Data Mod",
            Description = "Mod with save/load data persistence using JSON-style storage.",
            Category = "Utility",
            Preview = """
                local mod = RegisterMod("SaveMod", 1)
                local SaveData = {}

                -- Load save data
                mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED,
                    function(_, isSave)
                        if isSave then
                            SaveData = Isaac.LoadModData(mod) or {}
                        end
                    end)
                """,
            Files = new()
            {
                ["main.lua"] = """
                    local mod = RegisterMod("{name}", 1)

                    -- In-memory save data (persisted to disk on game end)
                    local SaveData = {}

                    -- Load save data when continuing a game
                    mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
                        if isSave then
                            local data = Isaac.LoadModData(mod)
                            SaveData = data and __JSON_decode(data) or {}
                            print("Loaded save data for {name}")
                        else
                            SaveData = {}
                        end
                    end)

                    -- Save data when the game ends
                    mod:AddCallback(ModCallbacks.MC_PRE_GAME_EXIT, function()
                        if next(SaveData) then
                            local json = __JSON_encode(SaveData)
                            mod:SaveData(json)
                        end
                    end)

                    -- Example: track pickups collected
                    mod:AddCallback(ModCallbacks.MC_POST_PICKUP_PICKUP, function(_, pickup)
                        local key = tostring(pickup.Type) .. "_" .. tostring(pickup.Variant)
                        SaveData[key] = (SaveData[key] or 0) + 1
                    end)

                    -- Simple JSON helpers (Isaac doesn't have built-in JSON)
                    function __JSON_encode(t)
                        -- TODO: Use a JSON library or implement encoding
                        -- Common approach: include a json.lua file in scripts/
                        return ""
                    end

                    function __JSON_decode(s)
                        -- TODO: Use a JSON library or implement decoding
                        return {}
                    end

                    print("{name} loaded!")
                    """,
                ["metadata.xml"] = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <metadata>
                        <name>{name}</name>
                        <description>{description}</description>
                        <author>{author}</author>
                        <version>1.0</version>
                        <apiVersion>1</apiVersion>
                    </metadata>
                    """,
                ["scripts/json.lua"] = """
                    -- Minimal JSON encoder/decoder for save data
                    -- Based on https://github.com/rxi/json.lua (MIT license)

                    local json = {}

                    function json.encode(t)
                        -- TODO: Implement JSON encoding
                        return "{}"
                    end

                    function json.decode(s)
                        -- TODO: Implement JSON decoding
                        return {}
                    end

                    return json
                    """
            },
            Directories = ["resources/gfx", "resources/scripts"]
        }
    ];
}
