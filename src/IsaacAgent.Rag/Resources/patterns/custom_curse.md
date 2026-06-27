---
title: Custom Curse
category: pattern
tags: curse, level, modifier
---
# Custom Curse

## Overview
This pattern shows how to add a custom curse that applies modifiers to a floor —
stat changes, visual effects, and gameplay alterations. It uses
`MC_POST_NEW_LEVEL` to detect floor entry, reads the active curses via
`Game():GetLevel():GetCurses()`, and applies effects for the duration of the
floor.

## main.lua

```lua
local mod = RegisterMod("Custom Curse Mod", 1)

-- Custom curse flag (use a bit beyond vanilla curse flags)
local CUSTOM_CURSE_FLAG = 1 << 16  -- LevelCurse.NUM_CURSES or higher bit

local CurseActive = false

-- Detect when a new level is generated and check curses
mod:AddCallback(ModCallbacks.MC_POST_NEW_LEVEL, function()
    local level = Game():GetLevel()
    local curses = level:GetCurses()

    -- Add our custom curse on floors that already have Curse of the Unknown
    if curses & LevelCurse.CURSE_OF_UNKNOWN ~= 0 then
        curses = curses | CUSTOM_CURSE_FLAG
        CurseActive = true
    else
        CurseActive = false
    end

    -- Notify the player
    if CurseActive then
        Isaac.SetTextScale(1, 1)
        Game():GetHUD():ShowFortuneText("A strange curse grips this floor...")
    end
end)

-- Apply stat modifications while the curse is active
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    if not CurseActive then return end

    -- Reduce fire rate slightly while cursed
    if player:GetFireDelay() < 20 then
        player.MaxFireDelay = player.MaxFireDelay + 0.2
    end

    -- Drain a tiny amount of health each second (every 30 frames)
    if Game():GetFrameCount() % 30 == 0 then
        player:TakeDamage(1, false, EntityRef(player), 0)
    end
end)

-- Visual effect: darken the room while the curse is active
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    if not CurseActive then return end

    local room = Game():GetCurrentRoom()
    -- Shift the room color to a reddish tint
    local color = Color(1, 0.7, 0.7, 1, 0, 0, 0)
    room:SetFloorColor(color)
    room:SetWallColor(color)
end)

-- Reset curse state when exiting the level
mod:AddCallback(ModCallbacks.MC_PRE_LEVEL_EXIT, function()
    CurseActive = false
end)

-- Expose the curse state so other mods can query it
function mod:IsCurseActive()
    return CurseActive
end
```

## Key Points
- `Game():GetLevel():GetCurses()` returns a bitmask of active curse flags
- Use a high bit (e.g. `1 << 16`) to define a custom curse flag that won't collide with vanilla ones
- `MC_POST_NEW_LEVEL` fires after a floor is generated — check curses here
- Apply per-frame effects in `MC_POST_PEFFECT_UPDATE` gated by a `CurseActive` flag
- Reset the flag on `MC_PRE_LEVEL_EXIT` so it doesn't leak to the next floor
- Visual changes (room color) should be re-applied in `MC_POST_NEW_ROOM` since rooms reset
- `LevelCurse` enum defines vanilla curse constants (`CURSE_OF_UNKNOWN`, `CURSE_OF_THE_LOST`, etc.)
- Custom curse flags are not shown in the HUD automatically — notify the player manually
