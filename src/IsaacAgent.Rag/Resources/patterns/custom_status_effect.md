---
title: Custom Status Effects
category: pattern
tags: status, effect, dot, poison, burn, freeze, enemy
---
# Custom Status Effects

## Overview
This pattern shows how to apply custom status effects (poison, burn, freeze, stun)
to enemies using `MC_POST_NPC_UPDATE` and per-entity `GetData()`. Includes damage
over time, freeze handling, and visual indicators.

## main.lua

```lua
local mod = RegisterMod("Custom Status Effects Mod", 1)

-- Status constants
local STATUS = {
    NONE   = 0,
    POISON = 1,
    BURN   = 2,
    FREEZE = 3,
    STUN   = 4,
}

-- Duration in frames (30 fps)
local DEFAULT_DURATION = 180  -- 6 seconds

-- Apply a status effect to an enemy
local function ApplyStatus(npc, status, duration, damagePerTick)
    local data = npc:GetData()
    if not data.Status then
        data.Status = STATUS.NONE
        data.StatusTimer = 0
        data.StatusDamage = 0
    end

    -- Freeze overrides other statuses (enemies cannot act)
    if status == STATUS.FREEZE then
        data.Status = STATUS.FREEZE
        data.StatusTimer = duration or DEFAULT_DURATION
        npc:AddEntityFlags(EntityFlag.FLAG_FREEZE)
        return
    end

    -- Don't overwrite a freeze with a weaker status
    if data.Status == STATUS.FREEZE then return end

    data.Status = status
    data.StatusTimer = duration or DEFAULT_DURATION
    data.StatusDamage = damagePerTick or 0.5
end

-- Expose ApplyStatus globally so other callbacks can use it
Isaac.GetModByName = Isaac.GetModByName or function() return mod end
mod.ApplyStatus = ApplyStatus

-- Main per-frame status processing
mod:AddCallback(ModCallbacks.MC_POST_NPC_UPDATE, function(_, npc)
    local data = npc:GetData()
    if not data.Status or data.Status == STATUS.NONE then return end

    data.StatusTimer = data.StatusTimer - 1

    -- Status expired — clean up
    if data.StatusTimer <= 0 then
        if data.Status == STATUS.FREEZE then
            npc:ClearEntityFlags(EntityFlag.FLAG_FREEZE)
        end
        data.Status = STATUS.NONE
        data.StatusDamage = 0
        return
    end

    -- Apply damage over time every 30 frames (once per second)
    if data.Status == STATUS.POISON or data.Status == STATUS.BURN then
        if not data.DotTimer then data.DotTimer = 0 end
        data.DotTimer = data.DotTimer + 1
        if data.DotTimer >= 30 then
            data.DotTimer = 0
            npc:TakeDamage(data.StatusDamage, 0, EntityRef(npc), 0)
        end
    end

    -- Stun: prevent movement and attacks
    if data.Status == STATUS.STUN then
        npc.Velocity = Vector.Zero
        npc:SetSpriteFrame("Stun")  -- play stun animation if available
    end

    -- Visual indicators
    if not data.VfxTimer then data.VfxTimer = 0 end
    data.VfxTimer = data.VfxTimer + 1
    if data.VfxTimer >= 10 then
        data.VfxTimer = 0
        local pos = npc.Position + Vector(0, -10)
        if data.Status == STATUS.POISON then
            Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.POOF01, 0, pos,
                Vector.Zero, npc)
        elseif data.Status == STATUS.BURN then
            Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.HOT_BOMB_FIRE, 0,
                pos, Vector.Zero, npc)
        elseif data.Status == STATUS.FREEZE then
            Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.ICE_PARTICLE, 0,
                pos, Vector.Zero, npc)
        end
    end
end)

-- Example trigger: apply poison when the player's tear hits an enemy
mod:AddCallback(ModCallbacks.MC_POST_ENTITY_TAKE_DMG, function(_, tookDamage, amount, flags, source)
    local npc = tookDamage:ToNPC()
    if not npc then return end

    if source and source.Type == EntityType.ENTITY_TEAR then
        local tear = source.Entity:ToTear()
        if tear and tear.SpawnerType == EntityType.ENTITY_PLAYER then
            -- 20% chance to poison on tear hit
            if math.random() < 0.2 then
                ApplyStatus(npc, STATUS.POISON, 150, 0.4)
            end
        end
    end
end)

-- Example trigger: freeze all enemies when using an active item
mod:AddCallback(ModCallbacks.MC_USE_ITEM, function(_, item, rng, player, useFlags)
    local enemies = Isaac.GetRoomEntities()
    for _, ent in ipairs(enemies) do
        if ent:IsEnemy() and not ent:HasEntityFlags(EntityFlag.FLAG_FRIENDLY) then
            ApplyStatus(ent:ToNPC(), STATUS.FREEZE, 120, 0)
        end
    end
    Game():GetHUD():ShowItemText("Time Stop!", "Enemies frozen!")
end, CollectibleType.COLLECTIBLE_D20)

-- Clean up freeze flags if the NPC dies while frozen
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    local data = npc:GetData()
    if data.Status == STATUS.FREEZE then
        npc:ClearEntityFlags(EntityFlag.FLAG_FREEZE)
    end
end)
```

## Key Points
- `MC_POST_NPC_UPDATE` runs every frame for each NPC — ideal for status ticking
- `npc:GetData()` returns a persistent table for per-entity custom state
- Store status type, timer, and DOT damage in the data table
- `npc:TakeDamage(dmg, 0, EntityRef(npc), 0)` deals damage without knockback
- `EntityFlag.FLAG_FREEZE` stops NPC animation and movement natively
- For stun, zero out `npc.Velocity` and override the sprite frame each update
- Use `EffectVariant` constants (POOF01, HOT_BOMB_FIRE, etc.) for visual feedback
- `MC_POST_ENTITY_TAKE_DMG` lets you react to hits and apply statuses from tears
- `MC_USE_ITEM` with a collectible filter triggers status application from actives
- Always clean up entity flags in `MC_POST_NPC_DEATH` to avoid leaks
