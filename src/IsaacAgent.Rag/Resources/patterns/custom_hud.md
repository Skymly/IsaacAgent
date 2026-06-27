---
title: Custom HUD
category: pattern
tags: hud, render, ui, sprite, draw
---
# Custom HUD

## Overview
This pattern shows how to draw custom HUD elements at fixed screen positions
using `MC_POST_RENDER` and `Isaac.GetSprite()`. It renders a custom status icon
with a numeric counter that follows the player's resource, using `Vector`
offsets to position elements relative to the screen origin.

## main.lua

```lua
local mod = RegisterMod("Custom HUD Mod", 1)

-- Load a custom sprite for the HUD icon
local hudSprite = Sprite()
hudSprite:Load("gfx/ui/custom_hud.anm2", true)
hudSprite:Play("Idle", true)

-- Load the number font for drawing counters
local numberFont = Font()
numberFont:Load("font/pftempestasa.fnt")

-- Track a custom resource (e.g. "Souls")
local SoulCount = 0

-- Add souls when enemies die
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    if npc:IsEnemy() then
        SoulCount = SoulCount + 1
    end
end)

-- Draw the HUD every frame after the game renders
mod:AddCallback(ModCallbacks.MC_POST_RENDER, function()
    local player = Isaac.GetPlayer(0)
    if not player then return end

    -- Get the top-left screen position (accounts for HUD offset setting)
    -- Vector(0, 0) is the top-left of the render area
    local basePos = Vector(10, 40)

    -- Draw the icon sprite at a fixed screen position
    hudSprite:Update()
    hudSprite:RenderLayer(0, basePos, Vector(0, 0), Vector(0, 0))

    -- Draw the soul count next to the icon with a Vector offset
    local textPos = basePos + Vector(20, 2)
    numberFont:DrawStringScaled(tostring(SoulCount), textPos.X, textPos.Y,
        1, 1, KColor(1, 1, 1, 1), 0, true)

    -- Draw a second element: a health bar overlay above the player
    DrawPlayerHealthBar(player)
end)

-- Draw a floating health bar above the player's head
function DrawPlayerHealthBar(player)
    local screenPos = Isaac.WorldToScreen(player.Position)
    local barPos = screenPos + Vector(-15, -30)  -- offset above the player
    local maxHearts = player:GetMaxHearts() / 2
    local curHearts = player:GetHearts() / 2
    local barWidth, barHeight = 30, 4

    -- Draw background bar
    Isaac.RenderScaledText("", barPos.X, barPos.Y, barWidth, barHeight, 1, 1,
        KColor(0.2, 0, 0, 0.8))

    -- Draw the filled portion
    if maxHearts > 0 then
        local fillRatio = curHearts / maxHearts
        Isaac.RenderScaledText("", barPos.X, barPos.Y,
            barWidth * fillRatio, barHeight, 1, 1,
            KColor(1, 0.2, 0.2, 1))
    end
end

-- Reset souls at the start of each run
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
    if not isSave then
        SoulCount = 0
    end
end)
```

## Key Points
- `MC_POST_RENDER` fires every frame after the game finishes drawing — use it for custom HUD overlays
- `Sprite:Load("path.anm2", true)` loads an ANM2 file; `Sprite:RenderLayer(layer, pos, ...)` draws it
- `Font:Load("font/xxx.fnt")` loads a bitmap font for drawing text and numbers
- `Font:DrawStringScaled(text, x, y, scaleX, scaleY, color, align, shadow)` draws scaled text
- Use `Vector(x, y)` offsets to position elements relative to a base screen position
- `Isaac.WorldToScreen(position)` converts a world position to screen coordinates for floating UI
- Screen origin `Vector(0, 0)` is the top-left of the render area; account for HUD offset settings
- Always null-check `Isaac.GetPlayer(0)` before accessing player data in render callbacks
- HUD elements drawn in `MC_POST_RENDER` are purely visual and do not capture input
