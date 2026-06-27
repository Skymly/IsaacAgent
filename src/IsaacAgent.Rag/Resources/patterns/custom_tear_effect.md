---
title: Custom Tear Effect
category: pattern
tags: tear, projectile, combat, effect
---
# Custom Tear Effect

## Overview
This pattern shows how to create custom tear effects — modifying tear appearance,
adding knockback on hit, and applying status effects to enemies struck by tears.

## main.lua

```lua
local mod = RegisterMod("Custom Tear Effect Mod", 1)

-- Custom tear variant (must be unique, use safe range)
local TEAR_VARIANT = Isaac.GetEntityVariantByName("Poison Tear")
if TEAR_VARIANT == -1 then
    TEAR_VARIANT = 840  -- Fallback to a safe range
end

-- Trigger item that grants the custom tear effect
local TEAR_ITEM = Isaac.GetItemIdByName("Venom Shots")

-- Called after the player fires a tear — modify its appearance and properties
mod:AddCallback(ModCallbacks.MC_POST_FIRE_TEAR, function(_, tear)
    local player = tear:GetLastParent():ToPlayer()
    if not player or not player:HasCollectible(TEAR_ITEM) then return end

    -- Recolor / resprite the tear
    local sprite = tear:GetSprite()
    sprite:ReplaceSpritesheet(0, "gfx/tears/venom_tear.png")
    sprite:LoadGraphics()

    -- Tint the tear green as a quick visual cue
    local color = Color(0.2, 1.0, 0.2, 1, 0, 0, 0)
    tear:SetColor(color, -1, 0, false, false)

    -- Scale up the tear slightly for impact
    tear.Size = tear.Size * 1.3
    tear.CollisionDamage = tear.CollisionDamage * 1.2

    -- Tag the tear so we can detect it on collision
    tear:GetData().IsVenomTear = true
end)

-- Called before a tear collides — return true to ignore the collision
mod:AddCallback(ModCallbacks.MC_PRE_TEAR_COLLISION, function(_, tear, collider, low)
    local data = tear:GetData()
    if not data.IsVenomTear then return end

    -- Only apply bonus effects to enemies
    if collider and collider:IsVulnerableEnemy() then
        -- Apply poison status for 3 seconds (90 frames)
        collider:AddPoison(EntityRef(tear), 90, 1.0)

        -- Strong knockback away from the player
        local player = tear:GetLastParent():ToPlayer()
        if player then
            local dir = (collider.Position - player.Position):Normalized()
            collider:AddVelocity(dir * 8)
        end

        -- Spawn a small poison cloud effect on hit
        Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.POOF01, 0,
            collider.Position, Vector.Zero, tear)

        -- Deal bonus damage
        collider:TakeDamage(2, 0, EntityRef(tear), 0)
    end

    -- Return false so the tear still pops normally
    return false
end, TearVariant)

-- Note: MC_PRE_TEAR_COLLISION can be filtered by tear variant.
-- To filter by our custom variant, pass TEAR_VARIANT as the third arg:
-- end, TEAR_VARIANT)

-- Reset tear color when the effect is removed (item lost)
mod:AddCallback(ModCallbacks.MC_EVALUATE_CACHE, function(_, player, cacheFlag)
    if cacheFlag == CacheFlag.CACHE_FIRERATE then
        -- Re-evaluation happens when items change; nothing extra needed here
        -- but this is a good hook to react to item gain/loss
    end
end)
```

## Key Points
- `MC_POST_FIRE_TEAR` — fires right after the player shoots a tear
- `MC_PRE_TEAR_COLLISION` — fires before a tear hits something; return `true` to skip collision
- `tear:GetLastParent():ToPlayer()` — get the player who fired the tear
- `tear:SetColor(...)` — tint the tear without replacing sprites
- `sprite:ReplaceSpritesheet(...)` + `sprite:LoadGraphics()` — swap the tear sprite
- `collider:AddPoison(EntityRef, duration, damage)` — apply poison status
- `collider:AddVelocity(vector)` — apply knockback
- `tear:GetData()` — tag tears so collision logic can identify them
- Filter collision callbacks by passing the tear variant as the third argument
- `tear.Size` and `tear.CollisionDamage` — scale tear visuals and damage
