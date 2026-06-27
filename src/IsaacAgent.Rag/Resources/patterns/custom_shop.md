---
title: Custom Shop
category: pattern
tags: shop, pickup, economy, price
---
# Custom Shop

## Overview
This pattern shows how to create custom shop items, modify shop prices, and
adjust shopkeeper behavior using pickup callbacks and shop item variants.

## main.lua

```lua
local mod = RegisterMod("Custom Shop Mod", 1)

-- Custom collectible to sell in the shop
local SHOP_CUSTOM_ITEM = Isaac.GetItemIdByName("Mystery Wares")

-- Modify shop item prices and contents when a shop item spawns
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_INIT, function(_, pickup)
    -- Only act on shop items
    if pickup.Variant ~= PickupVariant.PICKUP_SHOPITEM then return end
    if pickup.SubType == 0 then return end  -- ignore empty shop slots

    local data = pickup:GetData()
    local rng = pickup:GetDropRNG()

    -- 25% chance to replace the shop item with our custom wares
    if rng:RandomFloat() < 0.25 and SHOP_CUSTOM_ITEM ~= -1 then
        pickup:Morph(EntityType.ENTITY_PICKUP, PickupVariant.PICKUP_SHOPITEM,
            SHOP_CUSTOM_ITEM, true, true, true)
    end

    -- Adjust the price: discount some items, mark others as expensive
    local collectible = pickup.SubType
    local basePrice = pickup.Price
    if basePrice <= 0 then return end  -- already free or special

    -- Items under 7 coins get a 1-coin discount; others get +2
    if basePrice < 7 then
        pickup.Price = math.max(1, basePrice - 1)
    else
        pickup.Price = basePrice + 2
    end

    -- Tag expensive items for a visual cue
    if pickup.Price > 10 then
        data.ExpensiveItem = true
        local sprite = pickup:GetSprite()
        sprite:ReplaceSpritesheet(1, "gfx/ui/shopsign_expensive.png")
        sprite:LoadGraphics()
    end
end, PickupVariant.PICKUP_SHOPITEM)

-- Tint expensive shop items red each frame so the cue stays visible
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_UPDATE, function(_, pickup)
    if not pickup:GetData().ExpensiveItem then return end
    pickup:SetColor(Color(1, 0.4, 0.4, 1, 0, 0, 0), 2, 0, false, false)
end, PickupVariant.PICKUP_SHOPITEM)

-- Modify shopkeeper (Keeper) behavior: make them flee when the player is poor
mod:AddCallback(ModCallbacks.MC_POST_NPC_UPDATE, function(_, npc)
    if npc.Variant ~= 0 then return end  -- only the base shopkeeper
    local player = Isaac.GetPlayer(0)
    if not player then return end

    -- If the player has fewer than 3 coins, the keeper backs away
    if player:GetNumCoins() < 3 then
        local away = (npc.Position - player.Position):Normalized()
        npc.Velocity = away * 2
    end
end, EntityType.ENTITY_SHOPKEEPER)

-- Give the player a small coin refund when buying expensive items
mod:AddCallback(ModCallbacks.MC_POST_PICKUP_PURCHASE, function(_, pickup, player)
    if pickup:GetData().ExpensiveItem then
        player:AddCoins(1)  -- loyalty coin back
        Game():GetHUD():ShowItemText("Loyalty Reward", "+1 coin")
    end
end, PickupVariant.PICKUP_SHOPITEM)

-- Add a chance for a second custom item to appear in every shop room
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, function()
    local room = Game():GetRoom()
    if room:GetType() ~= RoomType.ROOM_SHOP then return end
    if SHOP_CUSTOM_ITEM == -1 then return end

    -- Only add the extra item once per room
    local data = room:GetData()
    if data.ExtraShopItemAdded then return end
    data.ExtraShopItemAdded = true

    local center = room:GetCenterPos()
    local pos = center + Vector(0, 40)
    local pickup = Isaac.Spawn(EntityType.ENTITY_PICKUP,
        PickupVariant.PICKUP_SHOPITEM, SHOP_CUSTOM_ITEM,
        pos, Vector.Zero, nil):ToPickup()
    pickup.Price = 5
    pickup:GetData().ExpensiveItem = false
end)
```

## Key Points
- `MC_POST_PICKUP_INIT` — fires when a pickup spawns; filter by `PICKUP_SHOPITEM`
- `pickup.Price` — read/write the coin cost of a shop item
- `pickup:Morph(...)` — transform a pickup into a different subtype in place
- `pickup:GetDropRNG()` — deterministic RNG tied to the pickup for reproducible rolls
- `MC_POST_PICKUP_UPDATE` — per-frame pickup logic (e.g. keep a tint active)
- `MC_POST_PICKUP_PURCHASE` — fires when the player buys the item
- `RoomType.ROOM_SHOP` — check `room:GetType()` to detect shop rooms
- `room:GetData()` — store per-room flags so logic runs once per visit
- `EntityType.ENTITY_SHOPKEEPER` — modify the Keeper NPC's behavior
- Always guard `Isaac.GetItemIdByName` results against `-1` (item not found)
