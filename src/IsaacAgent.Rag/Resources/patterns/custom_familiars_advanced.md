---
title: Advanced Familiar Behaviors
category: pattern
tags: familiar, advanced, orbit, combat, buff
---
# Advanced Familiar Behaviors

## Overview
This pattern demonstrates advanced familiar behaviors: orbiting with trigonometry,
a familiar that shoots its own tears, and a buff aura that empowers nearby allies.

## main.lua

```lua
local mod = RegisterMod("Advanced Familiars Mod", 1)

-- Custom familiar variants (safe range)
local ORBIT_VARIANT   = Isaac.GetEntityVariantByName("Orbit Sentinel")
local SHOOTER_VARIANT = Isaac.GetEntityVariantByName("Shooter Familiar")
local BUFF_VARIANT    = Isaac.GetEntityVariantByName("Buff Aura")
if ORBIT_VARIANT   == -1 then ORBIT_VARIANT   = 841 end
if SHOOTER_VARIANT == -1 then SHOOTER_VARIANT = 842 end
if BUFF_VARIANT    == -1 then BUFF_VARIANT    = 843 end

local ORBIT_ITEM   = Isaac.GetItemIdByName("Orbit Sentinel")
local SHOOTER_ITEM = Isaac.GetItemIdByName("Shooter Familiar")
local BUFF_ITEM    = Isaac.GetItemIdByName("Buff Aura")

-- Spawn familiars when the player owns the corresponding item
mod:AddCallback(ModCallbacks.MC_EVALUATE_CACHE, function(_, player, cacheFlag)
    if cacheFlag ~= CacheFlag.CACHE_FAMILIARS then return end

    local function ensureFamiliar(variant, item)
        if not player:HasCollectible(item) then return end
        for _, ent in ipairs(Isaac.FindByType(EntityType.ENTITY_FAMILIAR, variant)) do
            if ent:ToFamiliar().Player == player then return end
        end
        Isaac.Spawn(EntityType.ENTITY_FAMILIAR, variant, 0,
            player.Position, Vector.Zero, player)
    end

    ensureFamiliar(ORBIT_VARIANT, ORBIT_ITEM)
    ensureFamiliar(SHOOTER_VARIANT, SHOOTER_ITEM)
    ensureFamiliar(BUFF_VARIANT, BUFF_ITEM)
end)

-- Orbiting familiar: moves in a circle around the player
mod:AddCallback(ModCallbacks.MC_FAMILIAR_UPDATE, function(_, fam)
    local player = fam.Player
    if not player then return end
    local data = fam:GetData()
    data.Angle = (data.Angle or 0) + 0.05            -- rotation speed
    local radius = 80                                 -- orbit radius
    local target = player.Position + Vector(math.cos(data.Angle) * radius,
                                            math.sin(data.Angle) * radius)
    -- Smoothly move toward the target point on the orbit
    fam.Position = fam.Position + (target - fam.Position) * 0.25
    fam.Velocity = target - fam.Position

    -- Contact damage to enemies the orbit passes through
    for _, enemy in ipairs(Isaac.FindInRadius(fam.Position, 24, EntityPartition.ENEMY)) do
        if enemy:IsVulnerableEnemy() then
            enemy:TakeDamage(1.5, 0, EntityRef(fam), 0)
        end
    end
end, ORBIT_VARIANT)

-- Shooting familiar: fires tears at the nearest enemy
mod:AddCallback(ModCallbacks.MC_FAMILIAR_UPDATE, function(_, fam)
    local player = fam.Player
    if not player then return end
    local data = fam:GetData()
    data.FireTimer = (data.FireTimer or 0) + 1

    -- Hover slightly behind the player
    local target = player.Position + Vector(0, 20)
    fam.Position = fam.Position + (target - fam.Position) * 0.15

    -- Find the nearest vulnerable enemy
    local nearest, bestDist
    for _, enemy in ipairs(Isaac.GetRoomEntities()) do
        if enemy:IsVulnerableEnemy() then
            local d = enemy.Position:Distance(fam.Position)
            if not bestDist or d < bestDist then
                nearest, bestDist = enemy, d
            end
        end
    end

    -- Fire a tear every 30 frames when an enemy is in range
    if nearest and data.FireTimer >= 30 and bestDist < 400 then
        data.FireTimer = 0
        local dir = (nearest.Position - fam.Position):Normalized()
        local tear = Isaac.Spawn(EntityType.ENTITY_TEAR, TearVariant.BLUE, 0,
            fam.Position, dir * 9, fam):ToTear()
        tear.CollisionDamage = 3.5
        tear.Scale = 1.2
    end
end, SHOOTER_VARIANT)

-- Buff aura familiar: empowers the player and nearby familiars
mod:AddCallback(ModCallbacks.MC_FAMILIAR_UPDATE, function(_, fam)
    local player = fam.Player
    if not player then return end
    -- Stay centered on the player
    fam.Position = player.Position

    -- Visual aura effect
    if not fam:GetData().AuraSpawned then
        fam:GetData().AuraSpawned = true
        Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.ANGEL, 0,
            fam.Position, Vector.Zero, fam)
    end

    -- Buff the player while the aura familiar exists
    player.MoveSpeed = math.min(player.MoveSpeed + 0.1, 2.0)  -- capped

    -- Empower other familiars within the aura radius
    local auraRadius = 120
    for _, ent in ipairs(Isaac.FindByType(EntityType.ENTITY_FAMILIAR, -1, -1, false, false)) do
        if ent:ToFamiliar().Player == player and ent.Index ~= fam.Index then
            if ent.Position:Distance(fam.Position) <= auraRadius then
                -- Mark buffed familiars so their tears hit harder
                ent:GetData().AuraBuffed = true
            end
        end
    end
end, BUFF_VARIANT)
```

## Key Points
- `MC_FAMILIAR_UPDATE` — per-frame logic; filter by variant via the third argument
- Orbit movement uses `math.cos` / `math.sin` with an incrementing angle
- Smooth movement: `fam.Position + (target - fam.Position) * lerpFactor`
- `Isaac.GetRoomEntities()` — iterate to find the nearest enemy for targeting
- `Isaac.Spawn(EntityType.ENTITY_TEAR, ...)` — familiars can fire their own tears
- `tear.CollisionDamage` / `tear.Scale` — tune familiar tear stats
- Buff auras work by scanning `FindByType(ENTITY_FAMILIAR)` within a radius
- `fam:GetData()` — store per-familiar state (timers, angles, flags)
- `CACHE_FAMILIARS` — spawn familiars reactively when the player owns the item
- Cap stat buffs (`math.min`) to avoid runaway values across frames
