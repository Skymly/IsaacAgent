---
tags:
  - Class
  - REPENTOGON
---
# Class "EntityPlayer" (REPENTOGON Extensions)

REPENTOGON extends the vanilla `EntityPlayer` class with many additional methods
and properties not available in the base game.

## Functions

### GetMultiShotParams()
#### MultiShotParams GetMultiShotParams ( )
Returns the multi-shot parameters for the player, allowing you to query
how many tears are fired at once and their spread pattern.

### GetHealthType()
#### HealthType GetHealthType ( )
Returns the player's health type (RED, SOUL, BLACK, COIN, BONE, etc.).
Useful for mods that interact differently with different health types.

### GetActiveWeaponEntity()
#### Entity? GetActiveWeaponEntity ( )
Returns the entity representing the player's currently active weapon
(e.g., a tear, laser, or knife). Returns nil if no weapon entity exists.

### GetPlayerIndex()
#### int GetPlayerIndex ( )
Returns the player's unique index (0 for Player 1, 1 for Player 2, etc.).
Useful for multiplayer mods.

### GetPurityState()
#### PurityState GetPurityState ( )
Returns the current purity state (used by the Purity item).

### SetPurityState()
#### void SetPurityState ( PurityState state )
Sets the player's purity state directly.

### GetTearMovementDistance()
#### float GetTearMovementDistance ( )
Returns how far the player's tears travel before disappearing.

### GetTearInheritance()
#### table GetTearInheritance ( )
Returns a table of tear modifiers inherited from items.

### AddTrinketToInventory()
#### void AddTrinketToInventory ( int trinketID, bool firstTime )
Adds a trinket to the player's inventory without spawning it on the ground.

### RemoveTrinket()
#### void RemoveTrinket ( int trinketID )
Removes a trinket from the player's inventory.

### GetTrinketMulti()
#### int GetTrinketMulti ( int trinketID )
Returns the multiplier for a trinket (1 for normal, 2 for golden, 3 for double).

### GetCollectibleCount()
#### int GetCollectibleCount ( int itemID, bool includeTrinkets )
Returns the total number of a specific collectible the player has.

### GetActiveItem()
#### int GetActiveItem ( int slot )
Returns the active item ID in the specified slot (0, 1, 2).

### GetActiveCharge()
#### int GetActiveCharge ( int slot )
Returns the current charge of the active item in the specified slot.

### GetBatteryCharge()
#### int GetBatteryCharge ( int slot )
Returns the battery charge (overcharge) for the active item.

### GetActiveItemDesc()
#### ActiveItemDesc GetActiveItemDesc ( int slot )
Returns the full description of the active item in the specified slot.

### GetPlayerType()
#### PlayerType GetPlayerType ( )
Returns the player's character type (e.g., `PlayerType.PLAYER_ISAAC`).

### GetPlayerSkin()
#### string GetPlayerSkin ( )
Returns the player's current skin name (REPENTOGON specific).

### SetPlayerSkin()
#### void SetPlayerSkin ( string skinName )
Sets the player's skin to a custom skin (REPENTOGON specific).

### GetControllerIndex()
#### int GetControllerIndex ( )
Returns the controller index assigned to this player.

### IsCoopGhost()
#### bool IsCoopGhost ( )
Returns true if the player is a co-op ghost (reviving).

### GetBabySkin()
#### string GetBabySkin ( )
Returns the current baby skin name (for co-op babies).

### GetPlayerForm()
#### PlayerForm GetPlayerForm ( )
Returns the player's current transformation form.

### HasFullCharge()
#### bool HasFullCharge ( )
Returns true if the player's active item is fully charged.

### HasCollectible()
#### bool HasCollectible ( int itemID, bool ignoreModifiers )
Returns true if the player has the specified collectible.
If `ignoreModifiers` is true, only checks actual inventory, not temporary effects.

### HasTrinket()
#### bool HasTrinket ( int trinketID, bool ignoreModifiers )
Returns true if the player has the specified trinket.

### GetCollectibleRNG()
#### RNG GetCollectibleRNG ( int itemID )
Returns the RNG object associated with a specific collectible.
Useful for deterministic random effects tied to items.

### GetTrinketRNG()
#### RNG GetTrinketRNG ( int trinketID )
Returns the RNG object associated with a specific trinket.

### GetDamageMultiplier()
#### float GetDamageMultiplier ( )
Returns the player's total damage multiplier from all sources.

### GetFireDelayMultiplier()
#### float GetFireDelayMultiplier ( )
Returns the player's total fire delay multiplier.

### GetSpeedMultiplier()
#### float GetSpeedMultiplier ( )
Returns the player's total speed multiplier.

### GetTearRangeMultiplier()
#### float GetTearRangeMultiplier ( )
Returns the player's total tear range multiplier.

### GetLuck()
#### float GetLuck ( )
Returns the player's total luck value.

### AddCacheFlags()
#### void AddCacheFlags ( CacheFlag flags, bool updateImmediately )
Adds cache flags to be re-evaluated. If `updateImmediately` is true,
the cache is evaluated right away instead of on the next frame.

### GetPlayerSprite()
#### Sprite GetPlayerSprite ( )
Returns the player's main sprite object (REPENTOGON specific — more reliable
than `GetSprite()` for player-specific animations).

### GetWalkSprite()
#### Sprite GetWalkSprite ( )
Returns the player's walking animation sprite.

### GetHeadSprite()
#### Sprite GetHeadSprite ( )
Returns the player's head animation sprite.

### GetBodySprite()
#### Sprite GetBodySprite ( )
Returns the player's body animation sprite.

## REPENTOGON Properties

### SpriteScale
#### Vector SpriteScale { get; set; }
The player's sprite scale. Default is (1, 1). Can be modified for size effects.

### TearDamageMultiplier
#### float TearDamageMultiplier { get; set; }
Multiplier applied to all tear damage. Stack with item effects.

### TearFallingSpeed
#### float TearFallingSpeed { get; set; }
The falling speed of the player's tears.

### TearHeight
#### float TearHeight { get; set; }
The height of the player's tears (affects range).

### TearRange
#### float TearRange { get; set; }
The range of the player's tears.

### MaxFireDelay
#### int MaxFireDelay { get; set; }
The maximum fire delay (lower = faster tears).

### MoveSpeed
#### float MoveSpeed { get; set; }
The player's movement speed.

### Damage
#### float Damage { get; set; }
The player's base damage stat.

### Luck
#### float Luck { get; set; }
The player's luck stat.
