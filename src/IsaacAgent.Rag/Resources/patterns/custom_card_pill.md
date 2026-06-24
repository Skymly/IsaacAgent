---
title: Custom Card/Pill
category: pattern
tags: card, pill, pickup, consumable
---
# Custom Card and Pill

## Overview
This pattern shows how to register a custom card and a custom pill with
custom effects when used.

## main.lua

```lua
local mod = RegisterMod("Custom Consumables Mod", 1)

-- Custom card ID
local CARD_ID = Isaac.GetCardIdByName("Chaos Card")
if CARD_ID == -1 then
    CARD_ID = Card.NUM_CARDS + 10
end

-- Custom pill effect ID
local PILL_EFFECT_ID = Isaac.GetPillEffectByName("Mega Growth")
if PILL_EFFECT_ID == -1 then
    PILL_EFFECT_ID = PillEffect.NUM_PILL_EFFECTS + 5
end

-- Card use callback
mod:AddCallback(ModCallbacks.MC_USE_CARD, function(_, card, player, useFlags)
    -- Chaos Card: teleport to a random room and shuffle all items
    local level = Game():GetLevel()
    local rooms = level:GetRooms()
    local randomRoom = rooms:Get(math.random(rooms.Size - 1)).Data
    level:ChangeRoom(randomRoom.SafeGridIndex)

    -- Visual effect
    Isaac.Spawn(EntityType.ENTITY_EFFECT, EffectVariant.POOF01, 0,
        player.Position, Vector.Zero, player)

    Game():GetHUD():ShowItemText("Chaos Card!", "Reality bends...")

    return true
end, CARD_ID)

-- Pill use callback
mod:AddCallback(ModCallbacks.MC_USE_PILL, function(_, pillEffect, player, useFlags)
    -- Mega Growth: grow larger, gain damage and range temporarily
    player.SpriteScale = Vector(1.5, 1.5)
    player.Damage = player.Damage * 2
    player.TearRange = player.TearRange + 200

    -- Revert after 10 seconds (300 frames)
    local data = player:GetData()
    data.MegaGrowthTimer = 300

    Game():GetHUD():ShowItemText("", "You feel enormous!")

    return true
end, PILL_EFFECT_ID)

-- Revert Mega Growth effect
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, function(_, player)
    local data = player:GetData()
    if data.MegaGrowthTimer and data.MegaGrowthTimer > 0 then
        data.MegaGrowthTimer = data.MegaGrowthTimer - 1
        if data.MegaGrowthTimer == 0 then
            player.SpriteScale = Vector(1, 1)
            player.Damage = player.Damage / 2
            player.TearRange = player.TearRange - 200
            data.MegaGrowthTimer = nil
        end
    end
end)

-- Horse pill variant: stronger effect
mod:AddCallback(ModCallbacks.MC_USE_PILL, function(_, pillEffect, player, useFlags)
    -- Check if it's a horse pill
    if useFlags & UseFlag.USE_HORSER == 0 then return end

    -- Double the effect for horse pills
    player.SpriteScale = Vector(2.0, 2.0)
    player.Damage = player.Damage * 3
    player.TearRange = player.TearRange + 400

    local data = player:GetData()
    data.MegaGrowthTimer = 600  -- Lasts longer

    Game():GetHUD():ShowItemText("", "You feel GIGANTIC!")
end, PILL_EFFECT_ID)
```

## cards_metadata.xml

```xml
<metadata>
  <cards>
    <card id="100" name="Chaos Card"
          description="Teleports to a random room"
          gfx="gfx/cards/chaos_card.png"
          tags="special" />
  </cards>
</metadata>
```

## pills_metadata.xml

```xml
<metadata>
  <pills>
    <pill id="50" name="Mega Growth"
          description="Grow larger with boosted stats"
          gfx="gfx/pills/mega_growth.png" />
  </pills>
</metadata>
```

## Key Points
- `MC_USE_CARD` — callback for card use, with card ID as optional filter
- `MC_USE_PILL` — callback for pill use, with pill effect ID as optional filter
- Return `true` to consume the card/pill
- `useFlags` — check `UseFlag.USE_HORSER` for horse pill variants
- Card IDs: `Card.NUM_CARDS + offset`
- Pill effect IDs: `PillEffect.NUM_PILL_EFFECTS + offset`
- Use `player:GetData()` for temporary timed effects
- Define card metadata in `cards_metadata.xml`, pill metadata in `pills_metadata.xml`
