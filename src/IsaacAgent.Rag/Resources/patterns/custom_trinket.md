---
title: Custom Trinket
category: pattern
tags: trinket, passive
---
# Custom Trinket

## Overview
This pattern shows how to register a custom trinket that provides a passive effect
while held.

## main.lua

```lua
local mod = RegisterMod("Custom Trinket Mod", 1)
local TRINKET_ID = Isaac.GetTrinketIdByName("Broken Mirror")

-- Trinket effect: called every frame while the trinket is held
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    if player:HasTrinket(TRINKET_ID) then
        -- Reflect tears back at enemies (simplified)
        local entities = Isaac.GetRoomEntities()
        for _, entity in ipairs(entities) do
            if entity.Type == EntityType.ENTITY_TEAR and entity.SpawnerType == EntityType.ENTITY_PLAYER then
                -- Boost tear damage slightly
                local tear = entity:ToTear()
                if tear then
                    tear.CollisionDamage = tear.CollisionDamage * 1.05
                end
            end
        end
    end
end)

-- Triggered when the trinket is picked up
mod:AddCallback(ModCallbacks.MC_POST_ADD_TRINKET, function(_, trinketType, firstTime, player)
    if trinketType == TRINKET_ID and firstTime then
        Game():GetHUD():ShowItemText("Broken Mirror", "Your tears sharpen...")
    end
end, TRINKET_ID)

-- Golden trinket variant: double the effect
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    if player:HasTrinket(TRINKET_ID, true) then  -- true = check golden variant
        -- Apply doubled effect
        player.TearDamageMultiplier = player.TearDamageMultiplier * 1.1
    end
end)
```

## pocketitems_metadata.xml

```xml
<metadata>
  <trinkets>
    <trinket id="200" name="Broken Mirror"
             description="Tears deal 5% more damage"
             gfx="gfx/items/trinkets/broken_mirror.png"
             tags="offensive" />
  </trinkets>
</metadata>
```

## Key Points
- Use `Isaac.GetTrinketIdByName()` to get the trinket ID
- `player:HasTrinket(id)` checks for the trinket
- `player:HasTrinket(id, true)` checks for the golden variant
- `MC_POST_ADD_TRINKET` fires when the trinket is picked up
- Trinket IDs start at `TrinketType.NUM_TRINKETS + offset`
- Define trinket metadata in `pocketitems_metadata.xml`
