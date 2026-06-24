---
title: Custom Entity (Familiar)
category: pattern
tags: entity, familiar, companion
---
# Custom Entity — Familiar

## Overview
This pattern shows how to create a custom familiar (companion entity) that follows
the player and provides a passive effect.

## main.lua

```lua
local mod = RegisterMod("Custom Familiar Mod", 1)

-- Custom familiar variant (must be unique)
local FAMILIAR_VARIANT = Isaac.GetEntityVariantByName("Healing Fairy")
if FAMILIAR_VARIANT == -1 then
    FAMILIAR_VARIANT = 840  -- Fallback to a safe range
end

-- Spawn the familiar when the player has the required item
local FAMILIAR_ITEM = Isaac.GetItemIdByName("Fairy Companion")

mod:AddCallback(ModCallbacks.MC_EVALUATE_CACHE, function(_, player, cacheFlag)
    if cacheFlag == CacheFlag.CACHE_FAMILIARS then
        if player:HasCollectible(FAMILIAR_ITEM) then
            -- Check if the familiar already exists
            local hasFamiliar = false
            for _, ent in ipairs(Isaac.FindByType(EntityType.ENTITY_FAMILIAR, FAMILIAR_VARIANT)) do
                if ent:ToFamiliar().Player == player then
                    hasFamiliar = true
                    break
                end
            end

            if not hasFamiliar then
                Isaac.Spawn(EntityType.ENTITY_FAMILIAR, FAMILIAR_VARIANT, 0,
                    player.Position, Vector.Zero, player)
            end
        end
    end
end)

-- Familiar initialization
mod:AddCallback(ModCallbacks.MC_FAMILIAR_INIT, function(_, fam)
    local data = fam:GetData()
    data.HealTimer = 0
    data.OrbitAngle = 0
    data.OrbitRadius = 60
end, FAMILIAR_VARIANT)

-- Familiar update logic
mod:AddCallback(ModCallbacks.MC_FAMILIAR_UPDATE, function(_, fam)
    local player = fam.Player
    if not player then return end

    local data = fam:GetData()
    local sprite = fam:GetSprite()

    -- Orbit around the player
    data.OrbitAngle = data.OrbitAngle + 0.03
    local offset = Vector(math.cos(data.OrbitAngle) * data.OrbitRadius,
                          math.sin(data.OrbitAngle) * data.OrbitRadius)
    fam.Position = player.Position + offset
    fam.Velocity = (fam.Position - fam.Position)  -- Smooth movement

    -- Heal the player every 5 seconds (150 frames at 30fps)
    data.HealTimer = data.HealTimer + 1
    if data.HealTimer >= 150 then
        data.HealTimer = 0
        player:AddHearts(1)  -- Heal half a heart

        -- Visual feedback
        Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.HEART, 0,
            player.Position + Vector(0, -30), Vector.Zero, fam)
        sprite:Play("Heal", true)
    else
        if not sprite:IsPlaying("Heal") then
            sprite:Play("Float", true)
        end
    end

    -- Handle collision with enemies (deal contact damage)
    local enemies = Isaac.FindInRadius(fam.Position, 30, EntityPartition.ENEMY)
    for _, enemy in ipairs(enemies) do
        if enemy:IsVulnerableEnemy() then
            enemy:TakeDamage(2, 0, EntityRef(fam), 0)
        end
    end
end, FAMILIAR_VARIANT)
```

## Key Points
- Use `EntityType.ENTITY_FAMILIAR` with a custom variant
- `MC_FAMILIAR_INIT` — called once when the familiar spawns
- `MC_FAMILIAR_UPDATE` — called every frame while the familiar exists
- `fam.Player` — the player who owns this familiar
- `fam:GetData()` — persistent table for custom state
- Use `CACHE_FAMILIARS` to spawn familiars when the player has the right item
- `Isaac.FindByType` — find all entities of a specific type/variant
- `Isaac.FindInRadius` — find entities within a radius
- Familiar variants should be in a safe range (800+ for custom)
