---
title: Custom Room with Layout
category: pattern
tags: room, level, layout, custom
---
# Custom Room with Layout

## Overview
This pattern shows how to register a custom room layout that can appear
in the level generation, including custom doors, enemies, and rewards.

## main.lua

```lua
local mod = RegisterMod("Custom Room Mod", 1)

-- Custom room variant
local ROOM_VARIANT = 840  -- Safe range for custom rooms

-- Register the room to appear in level generation
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function()
    -- Custom rooms are defined in rooms.xml
    -- The game automatically loads them based on the XML
end)

-- Modify room when entered
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetRoom()
    local roomData = room:GetRoomConfig()

    if roomData.Variant == ROOM_VARIANT then
        local player = Isaac.GetPlayer(0)

        -- Custom room intro
        Game():GetHUD():ShowItemText("Mystery Room", "Something feels wrong...")

        -- Change room music
        Game():SetMusicState(0)  -- Stop music

        -- Spawn custom enemies
        local centerPos = room:GetCenterPos()
        for i = 1, 3 do
            local angle = (i / 3) * math.pi * 2
            local pos = centerPos + Vector(math.cos(angle) * 80, math.sin(angle) * 80)
            Isaac.Spawn(EntityType.ENTITY_SPIDER, 0, 0, pos, Vector.Zero, nil)
        end

        -- Lock all doors
        for i = 0, 7 do
            local door = room:GetDoor(i)
            if door then
                door:SetLockStates(true)
            end
        end
    end
end)

-- Check if all enemies are dead to unlock doors and spawn reward
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetRoom()
    local roomData = room:GetRoomConfig()

    if roomData.Variant ~= ROOM_VARIANT then return end

    -- Store initial enemy count
    local enemies = Isaac.GetRoomEntities()
    local enemyCount = 0
    for _, ent in ipairs(enemies) do
        if ent:IsEnemy() then enemyCount = enemyCount + 1 end
    end
    Game():GetRoom():GetData().CustomRoomEnemyCount = enemyCount
end)

-- Monitor enemy deaths in custom room
mod:AddCallback(ModCallbacks.MC_POST_NPC_DEATH, function(_, npc)
    local room = Game():GetRoom()
    local roomData = room:GetRoomConfig()

    if roomData.Variant ~= ROOM_VARIANT then return end

    local data = room:GetData()
    if data.CustomRoomEnemyCount then
        data.CustomRoomEnemyCount = data.CustomRoomEnemyCount - 1

        if data.CustomRoomEnemyCount <= 0 then
            -- All enemies dead — unlock doors and spawn reward
            for i = 0, 7 do
                local door = room:GetDoor(i)
                if door then
                    door:SetLockStates(false)
                end
            end

            -- Spawn a reward
            local centerPos = room:GetCenterPos()
            Isaac.Spawn(EntityType.ENTITY_PICKUP, PickupVariant.PICKUP_COLLECTIBLE,
                CollectibleType.COLLECTIBLE_LUCKY_FOOT,
                centerPos, Vector.Zero, nil)

            -- Play victory sound
            Game():GetHUD():ShowItemText("Cleared!", "The doors open...")

            -- Resume music
            Game():SetMusicState(1)
        end
    end
end)
```

## rooms.xml

```xml
<rooms>
  <room variant="840" name="Mystery Room" type="10" subtype="0"
        width="13" height="7" minshape="0" maxshape="0"
        weight="1.0" difficulty="1">
    <door exists="1" x="6" y="-1"/>
    <door exists="1" x="-1" y="3"/>
    <door exists="1" x="13" y="3"/>
    <door exists="1" x="6" y="7"/>
    <spawn x="6" y="3">
      <entity type="10" variant="0" subtype="0" weight="1.0"/>
    </spawn>
  </room>
</rooms>
```

## Key Points
- Custom rooms are defined in `rooms.xml` with a unique variant ID
- `MC_POST_NEW_ROOM` — fires when entering any room; check `roomData.Variant`
- `room:GetDoor(i)` — access doors by slot (0-7)
- `door:SetLockStates(true/false)` — lock/unlock doors
- `room:GetData()` — persistent table for room-specific state
- `room:GetCenterPos()` — center position of the room
- Room types: 1 = normal, 2 = shop, 3 = treasure, 10 = custom
- Weight controls how often the room appears in generation
- Use `Isaac.GetRoomEntities()` to get all entities in the current room
