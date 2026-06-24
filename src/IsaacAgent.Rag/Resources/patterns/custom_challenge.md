---
title: Custom Challenge
category: pattern
tags: challenge, run, modifier
---
# Custom Challenge

## Overview
This pattern shows how to register a custom challenge run with specific
starting items, modifiers, and rules.

## main.lua

```lua
local mod = RegisterMod("Custom Challenge Mod", 1)

-- Challenge ID (must be unique, use Challenge.NUM_CHALLENGES + offset)
local CHALLENGE_ID = Isaac.GetChallengeIdByName("Pacifist Run")
if CHALLENGE_ID == -1 then
    CHALLENGE_ID = Challenge.NUM_CHALLENGES + 50
end

-- Define challenge parameters
local ChallengeParams = {
    name = "Pacifist Run",
    startingItems = {
        CollectibleType.COLLECTIBLE_CHOCOLATE_MILK,
        CollectibleType.COLLECTIBLE_BOOK_OF_SHADOWS,
    },
    -- Remove all damage upgrades
    disableDamage = true,
    -- Player can only use active items to progress
    pacifistMode = true,
}

-- Apply challenge settings when the run starts
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
    if Game():GetChallenge() ~= CHALLENGE_ID then return end

    local player = Isaac.GetPlayer(0)

    -- Give starting items
    for _, itemID in ipairs(ChallengeParams.startingItems) do
        player:AddCollectible(itemID, 0, false)
    end

    -- Set starting health
    player:AddMaxHearts(-24)  -- Remove all extra heart containers
    player:AddHearts(2)       -- Start with 1 heart container

    -- Apply curse (hide the map)
    Game():GetLevel():SetCursed(CurseFlag.CURSE_OF_BLIND)

    print("Pacifist Run started!")
end)

-- Modify player stats for the challenge
mod:AddCallback(ModCallbacks.MC_EVALUATE_CACHE, function(_, player, cacheFlag)
    if Game():GetChallenge() ~= CHALLENGE_ID then return end

    if cacheFlag == CacheFlag.CACHE_DAMAGE and ChallengeParams.disableDamage then
        -- Reduce damage to 0.5 base
        player.Damage = 0.5
    elseif cacheFlag == CacheFlag.CACHE_SPEED then
        -- Slightly faster movement
        player.MoveSpeed = player.MoveSpeed + 0.2
    end
end)

-- Prevent dealing damage to enemies (pacifist mode)
mod:AddCallback(ModCallbacks.MC_ENTITY_TAKE_DMG, function(_, entity, amount, flags, source, countdown)
    if Game():GetChallenge() ~= CHALLENGE_ID then return end
    if not ChallengeParams.pacifistMode then return end

    -- Only block damage from the player's tears
    if entity:IsEnemy() and source.Type == EntityType.ENTITY_TEAR then
        local tear = source.Entity
        if tear and tear.SpawnerType == EntityType.ENTITY_PLAYER then
            return false  -- Block the damage
        end
    end
end)

-- Track challenge progress
mod:AddCallback(ModCallbacks.MC_POST_NEW_LEVEL, function()
    if Game():GetChallenge() ~= CHALLENGE_ID then return end
    local level = Game():GetLevel()
    print("Pacifist Run - Floor " .. level:GetStage())
end)

-- Check for challenge completion (beat a specific boss)
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    if Game():GetChallenge() ~= CHALLENGE_ID then return end
    if npc.Type == EntityType.ENTITY_THE_LAMB then
        Game():GetHUD():ShowItemText("Challenge Complete!", "Pacifist Run cleared!")
    end
end)
```

## challenges_metadata.xml

```xml
<metadata>
  <challenges>
    <challenge id="45" name="Pacifist Run"
               description="Beat the game without dealing direct damage"
               starting_items="Chocolate Milk,Book of Shadows"
               gfx="gfx/ui/challenge_pacifist.png"
               rules="no_damage_upgrades,pacifist" />
  </challenges>
</metadata>
```

## Key Points
- Use `Isaac.GetChallengeIdByName()` to get the challenge ID
- `Game():GetChallenge()` returns the current challenge ID (0 = no challenge)
- `MC_POST_GAME_STARTED` — apply starting items and settings
- `MC_EVALUATE_CACHE` — modify stats for the challenge
- `MC_ENTITY_TAKE_DMG` — return `false` to block damage
- Challenge IDs should be `Challenge.NUM_CHALLENGES + offset`
- Define challenge metadata in `challenges_metadata.xml`
- Use `CurseFlag` to apply curses (CURSE_OF_BLIND, CURSE_OF_MAZE, etc.)
