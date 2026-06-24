---
title: Custom Character
category: pattern
tags: character, player, custom, stats
---
# Custom Character

## Overview
This pattern shows how to register a custom playable character with unique
starting stats, items, and mechanics.

## main.lua

```lua
local mod = RegisterMod("Custom Character Mod", 1)

-- Character ID (must be unique)
local CHAR_ID = Isaac.GetPlayerTypeByName("The Alchemist")
if CHAR_ID == -1 then
    CHAR_ID = PlayerType.NUM_PLAYER_TYPES + 1
end

-- Character configuration
local CharConfig = {
    name = "The Alchemist",
    startingItems = {
        CollectibleType.COLLECTIBLE_SULFURIC_ACID,
        CollectibleType.COLLECTIBLE_BOOK_OF_SHADOWS,
    },
    startingHealth = {
        maxHearts = 2,  -- 1 heart container
        soulHearts = 2,
        blackHearts = 0,
    },
    stats = {
        damage = 3.5,
        fireDelay = 15,  -- tears stat (lower = faster)
        speed = 1.0,
        range = 237.5,
        tearHeight = 4.0,
        tearFallingSpeed = 3.5,
        luck = 0,
    },
    -- Custom mechanic: convert coins to random pickups
    alchemyMode = true,
}

-- Apply character settings on game start
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
    local player = Isaac.GetPlayer(0)
    if player:GetPlayerType() ~= CHAR_ID then return end

    -- Set starting health
    player:AddMaxHearts(-24)  -- Remove all hearts first
    player:AddSoulHearts(-24)
    player:AddMaxHearts(CharConfig.startingHealth.maxHearts)
    player:AddSoulHearts(CharConfig.startingHealth.soulHearts)
    player:AddHearts(CharConfig.startingHealth.maxHearts)

    -- Give starting items
    for _, itemID in ipairs(CharConfig.startingItems) do
        player:AddCollectible(itemID, 0, false)
    end

    -- Set starting pocket item (trinket)
    player:AddTrinket(TrinketType.TRINKET_LUCKY_ROCK)

    -- Set the character's name in the HUD
    Game():GetHUD():ShowItemText("The Alchemist", "Transmutation begins...")
end)

-- Modify base stats for the character
mod:AddCallback(ModCallbacks.MC_EVALUATE_CACHE, function(_, player, cacheFlag)
    if player:GetPlayerType() ~= CHAR_ID then return end

    -- Only modify on the first cache evaluation (base stats)
    if cacheFlag == CacheFlag.CACHE_DAMAGE then
        -- The Alchemist has lower base damage but scales with coins
        local coins = player:GetNumCoins()
        player.Damage = CharConfig.stats.damage + (coins * 0.05)
    elseif cacheFlag == CacheFlag.CACHE_FIREDELAY then
        player.MaxFireDelay = CharConfig.stats.fireDelay
    elseif cacheFlag == CacheFlag.CACHE_SPEED then
        player.MoveSpeed = CharConfig.stats.speed
    elseif cacheFlag == CacheFlag.CACHE_RANGE then
        player.TearRange = CharConfig.stats.range
    elseif cacheFlag == CacheFlag.CACHE_LUCK then
        player.Luck = CharConfig.stats.luck
    end
end)

-- Custom mechanic: Alchemy — picking up coins has a chance to transmute
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_UPDATE, function(_, pickup)
    local player = Isaac.GetPlayer(0)
    if player:GetPlayerType() ~= CHAR_ID then return end
    if not CharConfig.alchemyMode then return end
    if pickup.Variant ~= PickupVariant.PICKUP_COIN then return end

    -- 10% chance to transmute a coin into a random pickup
    if math.random() < 0.10 then
        local transmuteOptions = {
            { PickupVariant.PICKUP_HEART, 0 },
            { PickupVariant.PICKUP_KEY, 0 },
            { PickupVariant.PICKUP_BOMB, 0 },
            { PickupVariant.PICKUP_TRINKET, 0 },
        }
        local choice = transmuteOptions[math.random(#transmuteOptions)]
        Isaac.Spawn(EntityType.ENTITY_PICKUP, choice[1], choice[2],
            pickup.Position, pickup.Velocity, player)
        pickup:Remove()
    end
end)

-- Custom tears: poison effect
mod:AddCallback(ModCallbacks.MC_POST_FIRE_TEAR, function(_, tear)
    local player = tear.SpawnerEntity:ToPlayer()
    if not player or player:GetPlayerType() ~= CHAR_ID then return end

    -- REPENTOGON: add poison effect to tears
    if tear:GetData() then
        tear:GetData().AlchemistPoison = true
    end

    -- Change tear color to green
    tear:GetSprite().Color = Color(0.2, 1.0, 0.2, 1.0, 0, 0, 0)
end)

-- Apply poison on tear hit
mod:AddCallback(ModCallbacks.MC_ENTITY_TAKE_DMG, function(_, entity, amount, flags, source)
    if source.Type ~= EntityType.ENTITY_TEAR then return end
    local tear = source.Entity
    if not tear or not tear:GetData().AlchemistPoison then return end

    -- Apply poison (simplified: deal damage over time)
    local data = entity:GetData()
    data.AlchemistPoisonTimer = 120  -- 4 seconds at 30fps
end)

-- Poison damage over time
mod:AddCallback(ModCallbacks.MC_POST_NPC_UPDATE, function(_, npc)
    local data = npc:GetData()
    if data.AlchemistPoisonTimer and data.AlchemistPoisonTimer > 0 then
        data.AlchemistPoisonTimer = data.AlchemistPoisonTimer - 1
        if data.AlchemistPoisonTimer % 30 == 0 then  -- Damage every second
            npc:TakeDamage(1, 0, EntityRef(nil), 0)
        end
    end
end)
```

## players_metadata.xml

```xml
<metadata>
  <players>
    <player id="40" name="The Alchemist"
            description="Transmutes coins, poison tears"
            gfx="gfx/characters/alchemist.png"
            portrait="gfx/ui/portraits/alchemist.png"
            nameimage="gfx/ui/names/alchemist_name.png"
            skincolor="1"
            items="Sulfuric Acid,Book of Shadows"
            trinket="Lucky Rock"
            pocketactive="none"
            health="1,1,0,0"
            damage="3.5"
            firerate="15"
            speed="1.0"
            range="237.5"
            tearheight="4.0"
            tearfallingspeed="3.5"
            luck="0" />
  </players>
</metadata>
```

## Key Points
- Use `Isaac.GetPlayerTypeByName()` to get the character ID
- `player:GetPlayerType()` — check which character the player is playing
- `MC_POST_GAME_STARTED` — apply starting items, health, and setup
- `MC_EVALUATE_CACHE` — modify base stats (damage, fireDelay, speed, range, luck)
- Character IDs: `PlayerType.NUM_PLAYER_TYPES + offset`
- Define character metadata in `players_metadata.xml`
- Custom mechanics: use `MC_POST_PICKUP_UPDATE` for pickup interactions
- Custom tears: modify tear properties in `MC_POST_FIRE_TEAR`
- Damage over time: use entity `GetData()` for timers
- Cache flags: `CACHE_DAMAGE`, `CACHE_FIREDELAY`, `CACHE_SPEED`, `CACHE_RANGE`, `CACHE_LUCK`
