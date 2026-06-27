---
title: Custom Cutscene
category: pattern
tags: cutscene, intro, animation, cinematic
---
# Custom Cutscene

## Overview
This pattern shows how to create a custom intro cutscene that plays when a
new run starts. It freezes player input, plays an animation, displays
narrative text, and then transitions into normal gameplay.

## main.lua

```lua
local mod = RegisterMod("Custom Cutscene Mod", 1)

-- Cutscene state machine
local State = { INACTIVE = 0, FADING_IN = 1, TEXT_1 = 2, TEXT_2 = 3, FADING_OUT = 4, DONE = 5 }
local cutscene = { state = State.INACTIVE, timer = 0, duration = 0 }

-- Start the cutscene right after the game loads
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
    if isSave then return end  -- Only on fresh runs

    local game = Game()
    game:GetLevel():SetStage(LevelStage.STAGE8, StageType.STAGE_ORIGINAL)

    -- Freeze player input and hide HUD
    for i = 0, game:GetNumPlayers() - 1 do
        Isaac.GetPlayer(i).ControlsEnabled = false
    end
    game:GetHUD():SetVisible(false)

    cutscene.state = State.FADING_IN
    cutscene.timer = 0
    cutscene.duration = 60  -- 1 second fade-in at 60 FPS
end)

-- Main cutscene update loop — drives the state machine
mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
    if cutscene.state == State.INACTIVE or cutscene.state == State.DONE then return end
    cutscene.timer = cutscene.timer + 1

    if cutscene.timer < cutscene.duration then return end

    if cutscene.state == State.FADING_IN then
        cutscene.state = State.TEXT_1
        cutscene.duration = 180  -- 3 seconds
        Isaac.ShowForgottenOddText("The darkness awakens...")
    elseif cutscene.state == State.TEXT_1 then
        cutscene.state = State.TEXT_2
        cutscene.duration = 180
        Isaac.ShowForgottenOddText("You are not alone down here.")
    elseif cutscene.state == State.TEXT_2 then
        cutscene.state = State.FADING_OUT
        cutscene.duration = 60
    elseif cutscene.state == State.FADING_OUT then
        EndCutscene()
    end
    cutscene.timer = 0
end)

-- Render the fade overlay each frame
mod:AddCallback(ModCallbacks.MC_POST_RENDER, function()
    if cutscene.state == State.INACTIVE or cutscene.state == State.DONE then return end

    local alpha = 0
    if cutscene.state == State.FADING_IN then
        alpha = 1.0 - (cutscene.timer / cutscene.duration)
    elseif cutscene.state == State.FADING_OUT then
        alpha = cutscene.timer / cutscene.duration
    end

    if alpha > 0 then
        local screen = Isaac.GetScreenSize()
        local color = Color(0, 0, 0, math.min(alpha, 1.0))
        Isaac.RenderScaledText(" ", 0, 0, screen.X, screen.Y, color, 0, true)
    end
end)

-- Restore normal gameplay
function EndCutscene()
    local game = Game()
    game:GetLevel():SetStage(LevelStage.STAGE1_1, StageType.STAGE_ORIGINAL)
    game:ChangeRoom(game:GetLevel():GetCurrentRoomIndex())

    for i = 0, game:GetNumPlayers() - 1 do
        Isaac.GetPlayer(i).ControlsEnabled = true
    end
    game:GetHUD():SetVisible(true)
    cutscene.state = State.DONE
end
```

## Key Points
- `MC_POST_GAME_STARTED` — fires after a run loads; use `isSave` to skip saves
- `Game():GetLevel():SetStage(stage, stageType)` forces a specific stage
- `player.ControlsEnabled = false` freezes player input during the cutscene
- `Game():GetHUD():SetVisible(false)` hides the HUD for a cinematic feel
- `MC_POST_UPDATE` drives the cutscene state machine; `MC_POST_RENDER` draws overlays
- Use a simple state machine (`State`) to sequence phases
- Always restore `ControlsEnabled` and HUD visibility when finished
- `Isaac.ShowForgottenOddText()` displays narrative text to the player
- `Isaac.GetScreenSize()` gives the viewport size for full-screen overlays
