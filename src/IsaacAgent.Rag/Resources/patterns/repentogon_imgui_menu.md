---
title: REPENTOGON ImGui Menu
category: pattern
tags: repentogon, imgui, menu, ui, debug
---
# REPENTOGON ImGui Custom Menu

## Overview
This pattern shows how to create a custom ImGui menu using REPENTOGON's
ImGui integration for debugging and configuration.

## main.lua

```lua
local mod = RegisterMod("ImGui Menu Mod", 1)

-- REPENTOGON ImGui is available via the ImGui global
if not ImGui then
    print("REPENTOGON ImGui not available — mod requires REPENTOGON")
    return
end

-- Menu state
local MenuState = {
    visible = false,
    selectedTab = 0,
    settings = {
        enableDebug = false,
        spawnRate = 1.0,
        showHitboxes = false,
    },
    stats = {
        enemiesKilled = 0,
        roomsEntered = 0,
    }
}

-- Register a menu button in the REPENTOGON menu
ImGui.AddElement("IsaacAgent", "Open Mod Menu", ImGui.ElementFlags.Button,
    function()
        MenuState.visible = true
    end)

-- Main menu draw callback
mod:AddCallback(ModCallbacks.MC_POST_RENDER, function()
    if not MenuState.visible then return end

    -- Begin the ImGui window
    ImGui.Begin("Mod Menu", true, ImGui.WindowFlags.NoCollapse)

    -- Tab bar
    if ImGui.BeginTabBar("ModMenuTabs") then
        -- Settings tab
        if ImGui.BeginTabItem("Settings") then
            MenuState.settings.enableDebug = ImGui.Checkbox("Enable Debug Mode",
                MenuState.settings.enableDebug)

            _, MenuState.settings.spawnRate = ImGui.SliderFloat("Spawn Rate",
                MenuState.settings.spawnRate, 0.1, 5.0, "%.1f")

            MenuState.settings.showHitboxes = ImGui.Checkbox("Show Hitboxes",
                MenuState.settings.showHitboxes)

            if ImGui.Button("Reset to Defaults") then
                MenuState.settings = {
                    enableDebug = false,
                    spawnRate = 1.0,
                    showHitboxes = false,
                }
            end

            ImGui.EndTabItem()
        end

        -- Stats tab
        if ImGui.BeginTabItem("Stats") then
            ImGui.Text("Enemies Killed: " .. MenuState.stats.enemiesKilled)
            ImGui.Text("Rooms Entered: " .. MenuState.stats.roomsEntered)
            ImGui.Text("Current Floor: " .. Game():GetLevel():GetStage())

            if ImGui.Button("Reset Stats") then
                MenuState.stats = { enemiesKilled = 0, roomsEntered = 0 }
            end

            ImGui.EndTabItem()
        end

        -- Actions tab
        if ImGui.BeginTabItem("Actions") then
            if ImGui.Button("Spawn Random Item") then
                local player = Isaac.GetPlayer(0)
                local randomItem = math.random(CollectibleType.NUM_COLLECTIBLES)
                player:AddCollectible(randomItem, 0, false)
            end

            if ImGui.Button("Full Heal") then
                local player = Isaac.GetPlayer(0)
                player:AddHearts(24)
            end

            if ImGui.Button("Kill All Enemies") then
                local enemies = Isaac.GetRoomEntities()
                for _, enemy in ipairs(enemies) do
                    if enemy:IsEnemy() then
                        enemy:Kill()
                    end
                end
            end

            ImGui.EndTabItem()
        end

        -- Debug tab (REPENTOGON specific)
        if ImGui.BeginTabItem("Debug") then
            local player = Isaac.GetPlayer(0)
            ImGui.Text("Player Position: " .. tostring(player.Position))
            ImGui.Text("Player Velocity: " .. tostring(player.Velocity))
            ImGui.Text("Room Type: " .. Game():GetRoom():GetType())
            ImGui.Text("Frame Count: " .. Game():GetFrameCount())

            -- REPENTOGON: list all entities in room
            if ImGui.CollapsingHeader("Entities in Room") then
                local entities = Isaac.GetRoomEntities()
                for i, ent in ipairs(entities) do
                    if i <= 50 then  -- Limit to first 50
                        ImGui.Text(string.format("[%d] Type=%d Var=%d Sub=%d Pos=%s",
                            i, ent.Type, ent.Variant, ent.SubType, tostring(ent.Position)))
                    end
                end
            end

            ImGui.EndTabItem()
        end

        ImGui.EndTabBar()
    end

    -- Close button
    if ImGui.Button("Close Menu") then
        MenuState.visible = false
    end

    ImGui.End()
end)

-- Update stats
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    MenuState.stats.roomsEntered = MenuState.stats.roomsEntered + 1
end)

mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function()
    MenuState.stats.enemiesKilled = MenuState.stats.enemiesKilled + 1
end)

-- Apply debug settings
mod:AddCallback(ModCallbacks.MC_POST_RENDER, function()
    if MenuState.settings.showHitboxes then
        local entities = Isaac.GetRoomEntities()
        for _, ent in ipairs(entities) do
            -- REPENTOGON: render hitbox outline
            if ent:GetSprite() then
                local pos = ent.Position
                local size = ent.Size
                -- Use REPENTOGON's Line rendering
                if ImGui then
                    -- Draw debug circle (REPENTOGON specific)
                end
            end
        end
    end
end)

-- Toggle menu with a key (Tab key)
mod:AddCallback(ModCallbacks.MC_INPUT_IS_ACTION_TRIGGERED, function(_, entity, inputHook, buttonAction)
    if buttonAction == ButtonAction.ACTION_MAP then  -- Tab/MAP key
        if Input.IsButtonTriggered(Keyboard.KEY_TAB, 0) then
            MenuState.visible = not MenuState.visible
        end
    end
end)
```

## Key Points
- REPENTOGON provides `ImGui` global for custom menus
- `ImGui.AddElement(parent, label, flags, callback)` — add to the main menu
- `ImGui.Begin/End` — create a window
- `ImGui.BeginTabBar/BeginTabItem/EndTabItem/EndTabBar` — tabbed interface
- `ImGui.Checkbox`, `ImGui.SliderFloat`, `ImGui.Button` — standard widgets
- `ImGui.Text` — display text
- Use `MC_POST_RENDER` for the draw callback
- Menu state should be stored in a local table
- REPENTOGON-specific: check `if ImGui then` for compatibility
