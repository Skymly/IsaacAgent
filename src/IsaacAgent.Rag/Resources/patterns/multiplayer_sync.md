---
title: Multiplayer Sync
category: pattern
tags: multiplayer, coop, player, sync, network
---
# Multiplayer Sync

## Overview
This pattern shows how to handle multiplayer/co-op specific logic in
Binding of Isaac: Repentance. It detects co-op mode, maintains per-player
data, and synchronizes effects (such as a shared shield aura) between all
players using `MC_POST_PEFFECT_UPDATE`.

## main.lua

```lua
local mod = RegisterMod("Multiplayer Sync Mod", 1)

-- Per-player data table keyed by player index
local PlayerData = {}
local COLLECTIBLE_COOP_SHIELD = Isaac.GetCollectibleIdByName("Co-op Shield")

local function InitPlayer(_, index)
    PlayerData[index] = { shieldActive = false, shieldTimer = 0, syncedFrom = nil }
end

-- Detect co-op mode and initialize all players on run start
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function()
    local n = Game():GetNumPlayers()
    PlayerData = {}
    for i = 0, n - 1 do InitPlayer(nil, i) end
    if n > 1 then Isaac.DebugString("Co-op detected: " .. n .. " players") end
end)

-- Reinitialize when a player is added mid-run
mod:AddCallback(ModCallbacks.MC_POST_PLAYER_INIT, function(_, player)
    InitPlayer(nil, Isaac.GetPlayerIndex(player))
end)

-- Per-player effect update: maintain and sync the shield aura
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    local index = Isaac.GetPlayerIndex(player)
    local data = PlayerData[index]
    if not data then return end

    if player:HasCollectible(COLLECTIBLE_COOP_SHIELD) then
        data.shieldActive = true
        data.shieldTimer = 30
    elseif data.shieldTimer > 0 then
        data.shieldTimer = data.shieldTimer - 1
    else
        data.shieldActive = false
    end

    -- Sync the shield aura to all other players in co-op
    if data.shieldActive and Game():GetNumPlayers() > 1 then
        for i = 0, Game():GetNumPlayers() - 1 do
            if i ~= index then
                local other = Isaac.GetPlayer(i)
                local od = PlayerData[i]
                if od and not od.shieldActive then
                    od.shieldTimer = 15
                    od.shieldActive = true
                    od.syncedFrom = index
                    Isaac.Spawn(EntityType.ENTITY_EFFECT,
                        EffectVariant.ANGEL_DOT, 0, other.Position, Vector.Zero, player)
                end
            end
        end
    end

    -- Shield visual: brief invulnerability tint
    if data.shieldActive and player:GetDamageCooldown() == 0 then
        player:SetColor(Color(0.5, 0.7, 1.0, 0.3, 0, 0, 0), 2, 0, false, false)
    end
end)

-- Consume the synced shield to block one hit
mod:AddCallback(ModCallbacks.MC_ENTITY_TAKE_DMG, function(_, entity)
    if entity.Type ~= EntityType.ENTITY_PLAYER then return end
    local player = entity:ToPlayer()
    if not player then return end
    local data = PlayerData[Isaac.GetPlayerIndex(player)]
    if not data or not data.shieldActive then return end
    data.shieldActive = false
    data.shieldTimer = 0
    data.syncedFrom = nil
    Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.POOF01, 0,
        player.Position, Vector.Zero, player)
    return false  -- Cancel the damage
end, EntityType.ENTITY_PLAYER)

-- Clean up data on player death
mod:AddCallback(ModCallbacks.MC_POST_PLAYER_DEATH, function(_, player)
    PlayerData[Isaac.GetPlayerIndex(player)] = nil
end)

function IsCoopMode() return Game():GetNumPlayers() > 1 end
```

## Key Points
- `Isaac.GetPlayer(0)` is player 1; `Isaac.GetPlayer(1)` is player 2 (co-op)
- `Game():GetNumPlayers()` returns the total active player count
- `Isaac.GetPlayerIndex(player)` resolves a player entity to its index
- `MC_POST_PEFFECT_UPDATE` fires per frame for each player — ideal for per-player logic
- `MC_POST_PLAYER_INIT` fires when a player is created (including mid-run joins)
- `MC_ENTITY_TAKE_DMG` with `EntityType.ENTITY_PLAYER` intercepts damage per player
- Returning `false` from `MC_ENTITY_TAKE_DMG` cancels the damage entirely
- Store per-player state keyed by index, not by entity pointer; clean up on death
- `MC_POST_GAME_STARTED` is the best place to detect co-op and initialize all players
