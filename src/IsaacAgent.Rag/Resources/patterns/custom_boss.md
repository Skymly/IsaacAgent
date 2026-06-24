---
title: Custom Boss with Phases
category: pattern
tags: boss, npc, phases, combat
---
# Custom Boss with Phases

## Overview
This pattern shows how to create a custom boss NPC with multiple phases,
custom attacks, and phase transitions based on health.

## main.lua

```lua
local mod = RegisterMod("Custom Boss Mod", 1)

local BOSS_VARIANT = Isaac.GetEntityVariantByName("My Custom Boss")
if BOSS_VARIANT == -1 then
    BOSS_VARIANT = 900  -- Safe range for custom NPCs
end

local BOSS_SUBTYPE = 0

-- Boss initialization
mod:AddCallback(ModCallbacks.MC_POST_NPC_INIT, function(_, npc)
    local data = npc:GetData()

    -- Phase 1: normal, Phase 2: enraged, Phase 3: desperate
    data.Phase = 1
    data.AttackTimer = 0
    data.AttackPattern = 1
    data.MaxHP = npc.MaxHitPoints
    data.PhaseTransition = false

    -- Scale boss stats
    npc.MaxHitPoints = 500
    npc.HitPoints = 500
    npc.Scale = 1.5

    -- Set boss music
    Game():GetHUD():ShowItemText("My Custom Boss", "Appears!")

    -- Mark as a boss for the minimap
    npc:SetBossID(1)  -- Custom boss ID
end, EntityType.ENTITY_NPC, BOSS_VARIANT)

-- Boss AI update
mod:AddCallback(ModCallbacks.MC_NPC_UPDATE, function(_, npc)
    if npc.Variant ~= BOSS_VARIANT then return end

    local data = npc:GetData()
    local sprite = npc:GetSprite()
    local player = Isaac.GetPlayer(0)
    local targetPos = player.Position

    -- Phase transitions based on HP percentage
    local hpPercent = npc.HitPoints / data.MaxHP
    if data.Phase == 1 and hpPercent < 0.66 then
        data.Phase = 2
        data.PhaseTransition = true
        sprite:Play("Enrage", true)
        npc.Scale = 1.8
    elseif data.Phase == 2 and hpPercent < 0.33 then
        data.Phase = 3
        data.PhaseTransition = true
        sprite:Play("Desperate", true)
        npc.Scale = 2.0
        npc.MaxHitPoints = npc.MaxHitPoints  -- Don't change max HP
    end

    -- Skip AI during transition animation
    if data.PhaseTransition then
        if not sprite:IsPlaying("Enrage") and not sprite:IsPlaying("Desperate") then
            data.PhaseTransition = false
        else
            return
        end
    end

    -- Movement: chase the player
    local direction = (targetPos - npc.Position):Normalized()
    local speed = 2 + data.Phase  -- Faster in later phases
    npc.Velocity = direction * speed

    -- Attack logic
    data.AttackTimer = data.AttackTimer + 1
    local attackInterval = 60 - (data.Phase * 10)  -- Attack faster in later phases

    if data.AttackTimer >= attackInterval then
        data.AttackTimer = 0
        data.AttackPattern = (data.AttackPattern % 3) + 1

        if data.AttackPattern == 1 then
            -- Attack 1: Spread shot
            for i = 0, 7 do
                local angle = (i / 8) * math.pi * 2
                local velocity = Vector(math.cos(angle) * 5, math.sin(angle) * 5)
                Isaac.Spawn(EntityType.ENTITY_PROJECTILE, 0, 0,
                    npc.Position, velocity, npc)
            end
        elseif data.AttackPattern == 2 then
            -- Attack 2: Aimed shot (more projectiles in phase 2+)
            local count = 1 + data.Phase
            for i = 0, count - 1 do
                local angle = (targetPos - npc.Position):GetAngleDegrees()
                angle = angle + (i - count / 2) * 15
                local rad = angle * math.pi / 180
                local velocity = Vector(math.cos(rad) * 7, math.sin(rad) * 7)
                Isaac.Spawn(EntityType.ENTITY_PROJECTILE, 0, 0,
                    npc.Position, velocity, npc)
            end
        else
            -- Attack 3: Spawn minions (only in phase 2+)
            if data.Phase >= 2 then
                for i = 1, 2 do
                    Isaac.Spawn(EntityType.ENTITY_FLY, 0, 0,
                        npc.Position + Vector(math.random(-30, 30), math.random(-30, 30)),
                        Vector.Zero, npc)
                end
            end
        end
    end

    -- Animation
    if not sprite:IsPlaying("Attack") and data.AttackTimer < 5 then
        sprite:Play("Float", true)
        sprite.FlipX = (targetPos.X < npc.Position.X)
    end
end, EntityType.ENTITY_NPC)

-- Boss death effect
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    if npc.Variant ~= BOSS_VARIANT then return end

    -- Big explosion
    for i = 1, 10 do
        Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.BOMB_EXPLOSION, 0,
            npc.Position + Vector(math.random(-40, 40), math.random(-40, 40)),
            Vector.Zero, npc)
    end

    -- Drop loot
    Isaac.Spawn(EntityType.ENTITY_PICKUP, PickupVariant.PICKUP_COLLECTIBLE,
        CollectibleType.COLLECTIBLE_TREASURE_MAP,
        npc.Position, Vector.Zero, npc)

    -- Show victory text
    Game():GetHUD():ShowItemText("Victory!", "Boss defeated!")
end, EntityType.ENTITY_NPC, BOSS_VARIANT)
```

## Key Points
- Use `EntityType.ENTITY_NPC` with a custom variant (900+ for safety)
- `MC_POST_NPC_INIT` — set up boss stats, data, and intro
- `MC_NPC_UPDATE` — main AI loop: movement, attacks, phase transitions
- `MC_POST_NPC_DEATH` — death effects, loot drops, victory text
- `npc:GetData()` — store phase, timers, and custom state
- `npc:SetBossID(id)` — marks the NPC as a boss for the minimap
- Phase transitions: check HP percentage and change behavior/scale/speed
- Attack patterns: use a rotating counter for variety
- Projectile spawning: `EntityType.ENTITY_PROJECTILE` with velocity vectors
