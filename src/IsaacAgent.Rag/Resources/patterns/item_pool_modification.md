---
title: Item Pool Modification
category: pattern
tags: item, pool, loot
---
# Item Pool Modification

## Overview
This pattern shows how to modify item pools at runtime — adding custom items to
specific pools, removing vanilla items, and adjusting pool weights. It uses
`MC_POST_GET_ITEM_POOL` to obtain the `ItemPool` object and the
`ItemPool:AddItem` / `ItemPool:RemoveItem` methods to shape the loot table.

## main.lua

```lua
local mod = RegisterMod("Item Pool Mod", 1)

local COLLECTIBLE_ID = Isaac.GetItemIdByName("Lucky Rock")
local COLLECTIBLE_VANILLA = CollectibleType.COLLECTIBLE_SAD_ONION

-- Table of items to add to each pool with a weight
local PoolAdditions = {
    [ItemPoolType.POOL_TREASURE] = {
        { id = COLLECTIBLE_ID, weight = 1.0 },
    },
    [ItemPoolType.POOL_SHOP] = {
        { id = COLLECTIBLE_ID, weight = 0.5 },
    },
    [ItemPoolType.POOL_DEVIL] = {
        { id = COLLECTIBLE_ID, weight = 0.3 },
    },
}

-- Vanilla items to remove from specific pools
local PoolRemovals = {
    [ItemPoolType.POOL_TREASURE] = {
        CollectibleType.COLLECTIBLE_IBS,
    },
    [ItemPoolType.POOL_ANGEL] = {
        CollectibleType.COLLECTIBLE_SAD_ONION,
    },
}

-- Called once per run after the item pool is generated
mod:AddCallback(ModCallbacks.MC_POST_GET_ITEM_POOL, function(_, itemPool)
    if not itemPool then return end

    -- Add custom items to pools
    for poolType, items in pairs(PoolAdditions) do
        for _, entry in ipairs(items) do
            itemPool:AddItem(entry.id, poolType, entry.weight)
        end
    end

    -- Remove unwanted items from pools
    for poolType, items in pairs(PoolRemovals) do
        for _, itemId in ipairs(items) do
            itemPool:RemoveItem(itemId, poolType)
        end
    end

    -- Optional: reduce weight of a vanilla item without fully removing it
    itemPool:AddItem(COLLECTIBLE_VANILLA, ItemPoolType.POOL_TREASURE, -0.5)
end)

-- Track what the player actually receives for debugging / balance
mod:AddCallback(ModCallbacks.MC_POST_ADD_COLLECTIBLE, function(_, itemType, charge, firstTime, slot, varData, player)
    local pool = Game():GetItemPool()
    local currentPool = pool:GetCurrentPoolType()
    print("Player got item " .. tostring(itemType) .. " from pool " .. tostring(currentPool))
end)
```

## Key Points
- `MC_POST_GET_ITEM_POOL` fires after the item pool is (re)generated each run
- `game:GetItemPool()` returns the active `ItemPool` object at any time
- `ItemPool:AddItem(id, poolType, weight)` adds an item to a pool — a negative weight reduces the existing weight
- `ItemPool:RemoveItem(id, poolType)` fully removes an item from a pool
- Pool types are defined in the `ItemPoolType` enum (e.g. `POOL_TREASURE`, `POOL_SHOP`, `POOL_DEVIL`)
- Use `MC_POST_ADD_COLLECTIBLE` to observe which items players actually receive
- Custom items must be registered (via `items_metadata.xml` or Repentogon) before they can be added to pools
- Weights are relative — higher weight means higher chance of being picked
