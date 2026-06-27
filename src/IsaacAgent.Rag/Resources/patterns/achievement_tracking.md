---
title: Achievement Tracking
category: pattern
tags: achievement, unlock, tracking, save
---
# Achievement Tracking

## Overview
This pattern shows how to track custom achievements and unlock content based on
persistent save data. It uses `MC_POST_GAME_STARTED` to load unlock state,
monitors gameplay events for unlock conditions, and notifies the player when an
achievement is earned. Unlocked content is then enabled for future runs.

## main.lua

```lua
local mod = RegisterMod("Achievement Mod", 1)

-- Achievement definitions
local Achievements = {
    DEFEAT_BOSS_10 = { id = "defeat_boss_10", name = "Boss Slayer", target = 10 },
    CLEAR_BOSS_RUSH = { id = "clear_boss_rush", name = "Rush Master", target = 1 },
}

-- Persistent unlock state (saved to disk)
local SaveData = { bossKills = 0, bossRushCleared = false, unlocked = {} }

-- Load save data on game start
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
    if isSave then
        local data = Isaac.LoadModData(mod)
        if data then
            local ok, parsed = pcall(function() return json.decode(data) end)
            if ok and parsed then SaveData = parsed end
        end
    end

    -- Apply unlock-gated content at run start
    if SaveData.unlocked[Achievements.DEFEAT_BOSS_10.id] then
        mod.UnlockedContent = true
    end
end)

-- Save on exit
mod:AddCallback(ModCallbacks.MC_PRE_GAME_EXIT, function(_, shouldSave)
    if shouldSave then
        Isaac.SaveModData(mod, json.encode(SaveData))
    end
end)

-- Track boss kills
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    if npc:IsBoss() then
        SaveData.bossKills = SaveData.bossKills + 1
        CheckAchievement(Achievements.DEFEAT_BOSS_10, SaveData.bossKills)
    end
end)

-- Track Boss Rush completion
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetCurrentRoom()
    if room:GetType() == RoomType.ROOM_BOSSRUSH and room:IsClear() then
        SaveData.bossRushCleared = true
        CheckAchievement(Achievements.CLEAR_BOSS_RUSH, 1)
    end
end)

-- Check if an achievement's condition is met and unlock it
function CheckAchievement(achievement, currentValue)
    if SaveData.unlocked[achievement.id] then return end
    if currentValue >= achievement.target then
        SaveData.unlocked[achievement.id] = true
        Game():GetHUD():ShowItemText("Achievement Unlocked!", achievement.name)
        Isaac.ConsoleOutput("Achievement Unlocked: " .. achievement.name)
        SFXManager():Play(SoundEffect.SOUND_POWERUP1)
    end
end

-- Gate content behind unlocks: remove locked items if obtained early
mod:AddCallback(ModCallbacks.MC_POST_ADD_COLLECTIBLE, function(_, itemType, _, _, _, _, player)
    if not SaveData.unlocked[Achievements.DEFEAT_BOSS_10.id]
       and itemType == Isaac.GetItemIdByName("Boss Slayer Reward") then
        player:RemoveCollectible(itemType)
    end
end)
```

## Key Points
- Use `Isaac.SaveModData(mod, string)` / `Isaac.LoadModData(mod)` for persistent unlock state
- Load save data in `MC_POST_GAME_STARTED`; save in `MC_PRE_GAME_EXIT` with `json.encode()`
- Track gameplay events (`MC_POST_NPC_DEATH`, `MC_POST_NEW_ROOM`) to check unlock conditions
- Gate content by checking `SaveData.unlocked[achievementId]` before enabling items or features
- `Game():GetHUD():ShowItemText()` and `SFXManager():Play()` provide in-game notification + audio
- Always guard against re-triggering with `if SaveData.unlocked[id] then return end`
