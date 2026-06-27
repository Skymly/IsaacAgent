---
title: Custom Devil Room
category: pattern
tags: devil, room, deal, pact, evil
---
# Custom Devil Room

## Overview
This pattern shows how to modify devil room deals: replace existing deals with
custom pacts, adjust deal prices based on player stats, and add custom content
when the player enters a devil room.

## main.lua

```lua
local mod = RegisterMod("Custom Devil Room Mod", 1)

-- Custom collectibles offered as devil deals
local CUSTOM_PACT      = Isaac.GetItemIdByName("Blood Pact")
local CUSTOM_CURSED    = Isaac.GetItemIdByName("Cursed Heart")

-- Track whether the current room has already been customized
local function isDevilRoom()
    local room = Game():GetRoom()
    return room:GetType() == RoomType.ROOM_DEVIL
end

-- Modify the devil room when the player enters it
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    if not isDevilRoom() then return end
    local room = Game():GetRoom()
    local data = room:GetData()
    if data.DevilCustomized then return end
    data.DevilCustomized = true

    local player = Isaac.GetPlayer(0)
    local center = room:GetCenterPos()

    -- Replace existing devil-deal collectibles with custom pacts
    for _, ent in ipairs(Isaac.GetRoomEntities()) do
        if ent.Type == EntityType.ENTITY_PICKUP
           and ent.Variant == PickupVariant.PICKUP_COLLECTIBLE
           and ent:ToPickup().Price < 0 then
            -- Negative price marks a devil deal (costs hearts)
            local pickup = ent:ToPickup()
            local rng = pickup:GetDropRNG()
            if rng:RandomFloat() < 0.5 and CUSTOM_PACT ~= -1 then
                pickup:Morph(EntityType.ENTITY_PICKUP,
                    PickupVariant.PICKUP_COLLECTIBLE, CUSTOM_PACT,
                    true, true, true)
                pickup.Price = -3  -- 1.5 hearts (3 half-hearts)
            elseif CUSTOM_CURSED ~= -1 then
                pickup:Morph(EntityType.ENTITY_PICKUP,
                    PickupVariant.PICKUP_COLLECTIBLE, CUSTOM_CURSED,
                    true, true, true)
                pickup.Price = -2  -- 1 heart
            end
        end
    end

    -- Spawn a bonus pact near the center if the player has few red hearts
    if player:GetMaxHearts() <= 2 and CUSTOM_PACT ~= -1 then
        local bonus = Isaac.Spawn(EntityType.ENTITY_PICKUP,
            PickupVariant.PICKUP_COLLECTIBLE, CUSTOM_PACT,
            center + Vector(80, 0), Vector.Zero, nil):ToPickup()
        bonus.Price = -4  -- 2 hearts
        bonus:GetData().BonusDeal = true
    end

    -- Atmospheric cue: darken the room and show flavor text
    Game():GetHUD():ShowItemText("A Pact Awaits", "Blood for power...")
    SFXManager():Play(SoundEffect.SOUND_DEMON_CARD)
end)

-- Adjust devil deal prices based on the player's current health
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_UPDATE, function(_, pickup)
    if not isDevilRoom() then return end
    if pickup.Variant ~= PickupVariant.PICKUP_COLLECTIBLE then return end
    if pickup.Price >= 0 then return end  -- only devil deals (negative price)

    local player = Isaac.GetPlayer(0)
    -- Cheaper deals when the player is low on red hearts
    if player:GetMaxHearts() <= 2 and pickup.Price < -2 then
        pickup.Price = -2  -- cap the cost at 1 heart
    end
end, PickupVariant.PICKUP_COLLECTIBLE)

-- Custom effect when the player takes a custom pact deal
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_PURCHASE, function(_, pickup, player)
    if pickup.SubType == CUSTOM_PACT then
        -- Grant a damage up in exchange for the hearts already paid
        player:AddCacheFlags(CacheFlag.CACHE_DAMAGE)
        player:EvaluateItems()
        Game():GetHUD():ShowItemText("Blood Pact", "+1 Damage")
    elseif pickup.SubType == CUSTOM_CURSED then
        -- Convert one red heart container into a permanent soul heart
        player:AddMaxHearts(-2, true)
        player:AddSoulHearts(2)
        Game():GetHUD():ShowItemText("Cursed Heart", "Flesh becomes spirit")
    end
end, PickupVariant.PICKUP_COLLECTIBLE)

-- Raise the chance of a devil room appearing after a boss fight
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    if npc.Type ~= EntityType.ENTITY_THE_LAMB
       and npc.Type ~= EntityType.ENTITY_ISAAC
       and npc.Type ~= EntityType.ENTITY_SATAN then return end
    local player = Isaac.GetPlayer(0)
    -- Faithless players get a stronger pull toward the devil
    if not player:HasCollectible(CollectibleType.COLLECTIBLE_HOLY_MANTLE) then
        Game():GetLevel():SetDevilRoomDeals(1)  -- ensure deals are available
    end
end)
```

## Key Points
- `RoomType.ROOM_DEVIL` — detect devil rooms via `room:GetType()`
- `MC_POST_NEW_ROOM` — fires on every room entry; guard with `room:GetData()` to run once
- Devil deals use **negative** `pickup.Price` values (hearts, not coins)
- `pickup:Morph(...)` — swap a collectible subtype while preserving the deal slot
- `pickup:GetDropRNG()` — deterministic RNG for reproducible deal selection
- `MC_POST_PICKUP_PURCHASE` — fires when the player accepts the deal
- `player:AddMaxHearts(-2, true)` — remove a red heart container
- `player:AddSoulHearts(n)` — grant soul hearts (cursed-heart conversion)
- `player:AddCacheFlags(...)` + `EvaluateItems()` — recompute stats after a pact
- Always check `Isaac.GetItemIdByName(...) ~= -1` before using custom items
- `Game():GetLevel():SetDevilRoomDeals(n)` — influence devil room availability
