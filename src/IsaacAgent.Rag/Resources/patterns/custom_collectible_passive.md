---
title: Custom Collectible (Passive Item)
category: pattern
tags: collectible, passive, item, stat
---
# Custom Collectible — Passive Item

## Overview
This pattern shows how to register a passive collectible that modifies player stats
and triggers effects on pickup.

## main.lua

```lua
local mod = RegisterMod("Passive Item Mod", 1)
local ITEM_ID = Isaac.GetItemIdByName("Lucky Charm")

-- Stat modification: called when the item is picked up or removed
mod:AddCallback(ModCallbacks.MC_EVALUATE_CACHE, function(_, player, cacheFlag)
    if cacheFlag == CacheFlag.CACHE_LUCK then
        -- Add luck for each copy of the item the player has
        local numItems = player:GetCollectibleNum(ITEM_ID)
        player.Luck = player.Luck + (numItems * 2)
    elseif cacheFlag == CacheFlag.CACHE_DAMAGE then
        local numItems = player:GetCollectibleNum(ITEM_ID)
        player.Damage = player.Damage * (1 + numItems * 0.1)
    end
end)

-- Trigger an effect when the item is first picked up
mod:AddCallback(ModCallbacks.MC_POST_ADD_COLLECTIBLE, function(_, itemType, charge, firstTime, slot, data, player)
    if itemType == ITEM_ID and firstTime then
        -- Spawn hearts as a pickup bonus
        Isaac.Spawn(EntityType.ENTITY_PICKUP, PickupVariant.PICKUP_HEART, 0,
            player.Position + Vector(0, 40), Vector.Zero, player)
        Game():GetHUD():ShowItemText("Lucky Charm!", "You feel lucky!")
    end
end, ITEM_ID)

-- Continuous effect while item is held
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    if player:HasCollectible(ITEM_ID) then
        -- 1% chance per frame to spawn a coin
        if math.random() < 0.01 then
            Isaac.Spawn(EntityType.ENTITY_PICKUP, PickupVariant.PICKUP_COIN, 0,
                player.Position + Vector(math.random(-40, 40), math.random(-40, 40)),
                Vector.Zero, player)
        end
    end
end)
```

## items_metadata.xml

```xml
<item id="710101" name="Lucky Charm"
      description="+2 Luck, +10% Damage per copy"
      cache="luck,damage"
      gfx="gfx/items/collectibles/lucky_charm.png"
      tags="good,offensive"
      type="passive" />
```

## Key Points
- Use `MC_EVALUATE_CACHE` with the appropriate `CacheFlag` to modify stats
- Set `cache` attribute in XML to trigger the right cache flags
- Use `MC_POST_ADD_COLLECTIBLE` for one-time pickup effects
- `player:GetCollectibleNum(id)` counts how many copies the player has
- `player:HasCollectible(id)` checks if the player has at least one
- Cache flags: `CACHE_DAMAGE`, `CACHE_FIREDELAY`, `CACHE_SHOTSPEED`, `CACHE_RANGE`, `CACHE_SPEED`, `CACHE_LUCK`, `CACHE_ALL`
