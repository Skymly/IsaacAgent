---
title: Save Data Persistence
category: pattern
tags: save, data, persistence, run
---
# Save Data Persistence

## Overview
This pattern shows how to save and load custom data that persists across
game sessions using the `MC_POST_GAME_STARTED` and `MC_PRE_GAME_EXIT` callbacks
with `Isaac.SaveModData` / `Isaac.LoadModData`.

## main.lua

```lua
local mod = RegisterMod("Save Data Mod", 1)

-- Default save data structure
local SaveData = {
    totalRuns = 0,
    bestScore = 0,
    unlockedItems = {},
    settings = {
        difficulty = "normal",
        musicVolume = 100,
    }
}

-- Load save data when the game starts
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
    if isSave then
        -- Loading from a save file
        local data = Isaac.LoadModData(mod)
        if data then
            local ok, parsed = pcall(function() return json.decode(data) end)
            if ok and parsed then
                SaveData = parsed
            end
        end
    end

    SaveData.totalRuns = SaveData.totalRuns + 1
    print("Total runs: " .. SaveData.totalRuns)
end)

-- Save data when exiting the game
mod:AddCallback(ModCallbacks.MC_PRE_GAME_EXIT, function(_, shouldSave)
    if shouldSave then
        SaveData.bestScore = math.max(SaveData.bestScore, Game():GetScore())
        local data = json.encode(SaveData)
        Isaac.SaveModData(mod, data)
    end
end)

-- Per-run data (reset each new run)
local RunData = {}

mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    if not RunData.initialized then
        RunData = {
            roomsCleared = 0,
            damageTaken = 0,
            initialized = true,
        }
    end
    RunData.roomsCleared = RunData.roomsCleared + 1
end)

-- Per-entity data using the Entity's GetSprite() / GetData() API
mod:AddCallback(ModCallbacks.MC_POST_NPC_INIT, function(_, npc)
    local data = npc:GetData()
    data.CustomHP = 100
    data.Phase = 1
end)

mod:AddCallback(ModCallbacks.MC_NPC_UPDATE, function(_, npc)
    local data = npc:GetData()
    if data.CustomHP and data.CustomHP <= 0 then
        npc:Kill()
    end
end)
```

## Key Points
- `Isaac.SaveModData(mod, string)` saves a string to disk
- `Isaac.LoadModData(mod)` returns the saved string (or nil)
- Use `json.encode()` / `json.decode()` for structured data (requires the `json` library)
- `MC_POST_GAME_STARTED` with `isSave=true` means loading from a save file
- `MC_PRE_GAME_EXIT` with `shouldSave=true` means the game is saving
- Per-entity data: use `entity:GetData()` — a table that persists for the entity's lifetime
- Per-run data: use a local table, reset it in `MC_POST_NEW_RUN` or `MC_POST_GAME_STARTED`
