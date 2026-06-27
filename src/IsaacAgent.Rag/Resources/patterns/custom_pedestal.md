---
title: Custom Item Pedestal
category: pattern
tags: pedestal, collectible, pickup, greed, reroll
---
# Custom Item Pedestal

## Overview
This pattern shows how to modify item pedestals at spawn time: swapping items,
adding visual effects, rerolling pedestals under conditions, and preventing
Greed mode from stealing pedestal items.

## main.lua

```lua
local mod = RegisterMod("Custom Pedestal Mod", 1)

local REROLL_COLLECTIBLES = {
    CollectibleType.COLLECTIBLE_SAD_ONION,
    CollectibleType.COLLECTIBLE_INNER_EYE,
    CollectibleType.COLLECTIBLE_CUTE_OWL,
}

-- Modify pedestals the moment they spawn
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_INIT, function(_, pickup)
    local collectible = pickup:ToCollectible()
    if not collectible then return end

    local data = collectible:GetData()

    -- Reroll the item 25% of the time on the first floor
    local level = Game():GetLevel()
    if level:GetStage() == LevelStage.STAGE1_1 and math.random() < 0.25 then
        local newId = REROLL_COLLECTIBLES[math.random(#REROLL_COLLECTIBLES)]
        collectible.SubType = newId
        data.Rerolled = true
    end

    -- Add a visual glow effect to rerolled pedestals
    if data.Rerolled then
        local sprite = collectible:GetSprite()
        sprite:ReplaceSpritesheet(0, "gfx/items/collectibles/glow_outline.png")
        sprite:LoadGraphics()
    end

    -- Prevent Greed mode from stealing pedestal items
    if Game():IsGreedMode() then
        data.ProtectedFromGreed = true
    end
end, PickupVariant.PICKUP_COLLECTIBLE)

-- Stop Greed from vanishing with the item while the pedestal is protected
mod:AddCallback(ModCallbacks.MC_PRE_PICKUP_UPDATE, function(_, pickup)
    local collectible = pickup:ToCollectible()
    if not collectible then return end

    local data = collectible:GetData()
    if not data.ProtectedFromGreed then return end

    -- If a Greed enemy is nearby trying to steal, freeze the pedestal
    local entities = Isaac.GetRoomEntities()
    for _, ent in ipairs(entities) do
        if ent.Type == EntityType.ENTITY_GREED then
            -- Mark the pedestal so it cannot be picked up by Greed
            collectible:GetData().GreedBlocked = true
            -- Make the pedestal invisible to Greed by toggling the price
            if collectible.Price ~= 0 then
                collectible.AutoUpdatePrice = false
                collectible.Price = 0
            end
            break
        end
    end
end, PickupVariant.PICKUP_COLLECTIBLE)

-- Spawn a custom particle effect above pedestals every frame
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_UPDATE, function(_, pickup)
    local collectible = pickup:ToCollectible()
    if not collectible then return end

    local data = collectible:GetData()
    if not data.Rerolled then return end

    -- Throttle particle spawn to every 15 frames
    if not data.ParticleTimer then data.ParticleTimer = 0 end
    data.ParticleTimer = data.ParticleTimer + 1
    if data.ParticleTimer < 15 then return end
    data.ParticleTimer = 0

    local pos = collectible.Position + Vector(0, -20)
    Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.POOF01, 0, pos,
        Vector.Zero, collectible)
end, PickupVariant.PICKUP_COLLECTIBLE)

-- Force a specific collectible onto the first pedestal of a treasure room
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_INIT, function(_, pickup)
    local collectible = pickup:ToCollectible()
    if not collectible then return end

    local room = Game():GetRoom()
    if room:GetRoomConfig().Type ~= RoomType.ROOM_TREASURE then return end

    local level = Game():GetLevel()
    if level:GetStage() ~= LevelStage.STAGE1_1 then return end

    -- Only override the first pedestal in the room
    if not room:GetData().FirstPedestalSet then
        collectible.SubType = CollectibleType.COLLECTIBLE_BLOOD_BAG
        room:GetData().FirstPedestalSet = true
    end
end, PickupVariant.PICKUP_COLLECTIBLE)
```

## Key Points
- `MC_POST_PICKUP_INIT` with `PickupVariant.PICKUP_COLLECTIBLE` fires when a pedestal spawns
- `pickup:ToCollectible()` converts the entity; check for nil before using
- `collectible.SubType` is the item ID shown on the pedestal — change it to reroll
- `collectible.Price` and `AutoUpdatePrice` control shop/greed pricing behavior
- `Game():IsGreedMode()` returns true during Greed mode runs
- `MC_PRE_PICKUP_UPDATE` runs before the pickup updates each frame — good for blocking theft
- `EffectVariant.POOF01` is a handy built-in particle for visual flair
- Use `collectible:GetData()` to attach persistent per-pedestal flags
- `room:GetData()` tracks room-level state like "first pedestal already set"
