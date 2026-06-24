---
title: Custom Collectible (Active Item)
category: pattern
tags: collectible, active, item
---
# Custom Collectible — Active Item

## Overview
This pattern shows how to register a custom active collectible (item with a charge bar)
that performs an effect when used.

## main.lua

```lua
local mod = RegisterMod("My Custom Item Mod", 1)

-- Item ID (must be unique, use CollectibleType.NUM_COLLECTIBLES + offset)
local ITEM_ID = Isaac.GetItemIdByName("My Custom Active Item")
if ITEM_ID == -1 then
    -- Fallback: use a safe ID range
    ITEM_ID = CollectibleType.NUM_COLLECTIBLES + 100
end

-- Register the item with the game
mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function()
    -- Ensure the item is registered
    local itemConfig = Isaac.GetItemConfig()
    -- Item should be defined in items_metadata.xml
end)

-- Called when the player uses the active item (presses space)
mod:AddCallback(ModCallbacks.MC_USE_ITEM, function(_, item, rng, player, useFlags)
    -- Perform the item effect
    player:AddCoins(5)  -- Example: give 5 coins

    -- Spawn a visual effect
    Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.POOF01, 0, player.Position, Vector.Zero, player)

    -- Return true to consume the charge
    return true
end, ITEM_ID)

-- Optional: modify the item's charge cost
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    -- Custom logic while item is in inventory
end, ITEM_ID)
```

## items_metadata.xml

```xml
<metadata>
  <items>
    <item id="710100" name="My Custom Active Item"
          description="Grants 5 coins when used"
          cache="none"
          gfx="gfx/items/collectibles/my_custom_item.png"
          tags="quest"
          maxcharges="4"
          type="active" />
  </items>
</metadata>
```

## Key Points
- Use `MC_USE_ITEM` callback with the item ID as the optional argument
- Return `true` from the callback to consume the charge
- Define item metadata in `items_metadata.xml` with `type="active"`
- Set `maxcharges` for the charge bar (1-12)
- Item IDs should be `CollectibleType.NUM_COLLECTIBLES + offset` to avoid conflicts
