---
title: Custom Door Behavior
category: pattern
tags: door, room, lock, access, level
---
# Custom Door Behavior

## Overview
This pattern shows how to create custom doors and modify door behavior at runtime:
locking/unlocking doors, changing door destinations, and gating access behind
conditions such as keys or enemy clears.

## main.lua

```lua
local mod = RegisterMod("Custom Door Mod", 1)

-- Door slots are 0-7 (Up, Right, Down, Left, LeftUp, RightUp, LeftDown, RightDown)
local SLOT_UP    = DoorSlot.LEFT_UP_SLOT    -- 0
local SLOT_RIGHT = DoorSlot.RIGHT0_SLOT     -- 1
local SLOT_DOWN  = DoorSlot.DOWN0_SLOT      -- 2
local SLOT_LEFT  = DoorSlot.LEFT0_SLOT      -- 3

-- Track which rooms have had their doors configured
local configuredRooms = {}

-- Configure doors whenever a new room is entered
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetRoom()
    local level = Game():GetLevel()
    local roomIdx = level:GetCurrentRoomIndex()

    -- Only configure once per room visit
    if configuredRooms[roomIdx] then return end
    configuredRooms[roomIdx] = true

    -- Lock all doors in treasure rooms unless the player holds a key
    local roomData = room:GetRoomConfig()
    if roomData.Type == RoomType.ROOM_TREASURE then
        local player = Isaac.GetPlayer(0)
        local hasKey = player:GetNumKeys() > 0

        for i = 0, 7 do
            local door = room:GetDoor(i)
            if door then
                -- Lock the door; it will be unlocked when conditions are met
                door:SetLockStates(not hasKey)
            end
        end

        if not hasKey then
            Game():GetHUD():ShowItemText("Locked", "Find a key to leave...")
        end
    end
end)

-- Unlock doors once all enemies in the current room are dead
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    local room = Game():GetRoom()

    -- Count remaining enemies
    local enemies = Isaac.GetRoomEntities()
    local remaining = 0
    for _, ent in ipairs(enemies) do
        if ent:IsEnemy() and not ent:HasEntityFlags(EntityFlag.FLAG_FRIENDLY) then
            remaining = remaining + 1
        end
    end

    if remaining == 0 then
        for i = 0, 7 do
            local door = room:GetDoor(i)
            if door then
                door:SetLockStates(false)
            end
        end
        Game():GetHUD():ShowItemText("Cleared!", "The doors creak open...")
    end
end)

-- Change a door's destination: redirect the left door to a secret room
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetRoom()
    local level = Game():GetLevel()
    local roomData = room:GetRoomConfig()

    -- Only redirect doors in normal rooms on the first floor
    if roomData.Type ~= RoomType.ROOM_DEFAULT then return end
    if level:GetStage() ~= LevelStage.STAGE1_1 then return end

    local door = room:GetDoor(SLOT_LEFT)
    if door and door.TargetRoomIndex < 0 then
        -- TargetRoomIndex < 0 means it leads to a special room;
        -- swap it to point at the current secret room index instead
        local secretIdx = level:GetRooms():GetRoomByIdx(-3, 0).SafeGridIndex
        if secretIdx >= 0 then
            door.TargetRoomIndex = secretIdx
        end
    end
end)

-- Conditional access: block the down door unless the player has 50 cents
mod:AddCallback(ModCallbacks.MC_PRE_ROOM_ENTITY_SPAWN, function()
    local room = Game():GetRoom()
    local player = Isaac.GetPlayer(0)

    local door = room:GetDoor(SLOT_DOWN)
    if door and player:GetNumCoins() < 50 then
        door:SetLockStates(true)
    end
end)
```

## Key Points
- `MC_POST_NEW_ROOM` fires every time the player enters a room; use it to configure doors
- `room:GetDoor(slot)` returns the `GridEntityDoor` at that slot (0-7); nil if none
- `door:SetLockStates(true/false)` locks or unlocks the door visually and mechanically
- `door.TargetRoomIndex` can be changed to redirect where a door leads
- `DoorSlot` constants: `LEFT0_SLOT`, `RIGHT0_SLOT`, `DOWN0_SLOT`, `UP0_SLOT`, plus diagonal slots
- `room:GetRoomConfig().Type` gives the `RoomType` (DEFAULT, TREASURE, SHOP, etc.)
- Track per-room state with a table keyed by `level:GetCurrentRoomIndex()`
- `MC_POST_NPC_DEATH` is useful for unlocking doors after a room clear
- `MC_PRE_ROOM_ENTITY_SPAWN` runs before entities populate — good for early door gating
