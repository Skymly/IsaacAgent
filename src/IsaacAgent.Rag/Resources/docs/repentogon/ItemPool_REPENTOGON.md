---
tags:
  - Class
  - REPENTOGON
---
# Class "ItemPool" (REPENTOGON Extensions)

REPENTOGON extends the vanilla `ItemPool` class with additional methods for
controlling item generation, pool manipulation, and custom item pools.

## Functions

### GetPool()
#### ItemPoolType GetPool ( Vector position )
Returns the item pool type that would be used at the given position
(e.g., `POOL_TREASURE`, `POOL_SHOP`, `POOL_DEVIL`).

### GetItem()
#### CollectibleType GetItem ( ItemPoolType poolType, boolean decrease, int seed )
Returns a random collectible from the specified pool.
If `decrease` is true, the item's weight in the pool is reduced.

### GetCard()
#### Card GetCard ( int seed, boolean allowNonCards, boolean allowMods )
Returns a random card from the card pool.

### GetTrinket()
#### TrinketType GetTrinket ( int seed, boolean allowMods )
Returns a random trinket from the trinket pool.

### GetPill()
#### PillEffect GetPill ( int seed, boolean horsePill )
Returns a random pill effect.

### AddItemToPool()
#### void AddItemToPool ( ItemPoolType poolType, CollectibleType itemID, float weight )
Adds a custom item to a specific pool with a given weight.
Higher weight = more likely to appear.

### RemoveItemFromPool()
#### void RemoveItemFromPool ( ItemPoolType poolType, CollectibleType itemID )
Removes an item from a specific pool.

### GetPoolItems()
#### table GetPoolItems ( ItemPoolType poolType )
Returns a table of all items in the specified pool with their weights.

### GetPoolWeight()
#### float GetPoolWeight ( ItemPoolType poolType, CollectibleType itemID )
Returns the current weight of an item in a pool.

### SetPoolWeight()
#### void SetPoolWeight ( ItemPoolType poolType, CollectibleType itemID, float weight )
Sets the weight of an item in a pool.

### GetPoolSize()
#### int GetPoolSize ( ItemPoolType poolType )
Returns the number of items in the specified pool.

### GetUnseenItem()
#### CollectibleType GetUnseenItem ( ItemPoolType poolType, int seed )
Returns a random item from the pool that the player hasn't seen yet.

### HasSeenItem()
#### bool HasSeenItem ( CollectibleType itemID )
Returns true if the player has seen this item before in any run.

### MarkItemSeen()
#### void MarkItemSeen ( CollectibleType itemID )
Marks an item as seen by the player.

### GetRemovedItems()
#### table GetRemovedItems ( )
Returns a table of items that have been removed from all pools
(e.g., by guppy's paw, sacrificial altar, etc.).

### RemoveItem()
#### void RemoveItem ( CollectibleType itemID )
Removes an item from ALL pools permanently (for this run).

### RestoreItem()
#### void RestoreItem ( CollectibleType itemID )
Restores a previously removed item to all pools.

### IsItemRemoved()
#### bool IsItemRemoved ( CollectibleType itemID )
Returns true if the item has been removed from pools.

### GetItemConfig()
#### ItemConfig GetItemConfig ( )
Returns the item configuration object.

### GetCollectibleFromPool()
#### CollectibleType GetCollectibleFromPool ( ItemPoolType pool, int seed, boolean decrease, boolean allowRemoved )
Returns a collectible from the specified pool with full control over options.

### GetCollectibleEx()
#### CollectibleType GetCollectibleEx ( ItemPoolType pool, int seed, RNG rng, boolean decrease, boolean allowRemoved )
Extended version of GetCollectibleFromPool with RNG control.

### GetPedestalItem()
#### CollectibleType GetPedestalItem ( int seed, Vector position, boolean decrease )
Returns the item that would appear on a pedestal at the given position.

### SpawnPedestalItem()
#### EntityPickup SpawnPedestalItem ( Vector position, CollectibleType itemID, int seed )
Spawns an item pedestal with a specific item at the given position.

### GetShopItem()
#### CollectibleType GetShopItem ( int seed, Vector position, boolean decrease )
Returns the item that would appear in a shop at the given position.

### GetDevilItem()
#### CollectibleType GetDevilItem ( int seed, boolean decrease )
Returns an item from the devil room pool.

### GetAngelItem()
#### CollectibleType GetAngelItem ( int seed, boolean decrease )
Returns an item from the angel room pool.

### GetCurseRoomItem()
#### CollectibleType GetCurseRoomItem ( int seed, boolean decrease )
Returns an item from the curse room pool.

### GetSecretRoomItem()
#### CollectibleType GetSecretRoomItem ( int seed, boolean decrease )
Returns an item from the secret room pool.

### GetLibraryItem()
#### CollectibleType GetLibraryItem ( int seed, boolean decrease )
Returns an item from the library pool.

### GetBossItem()
#### CollectibleType GetBossItem ( int seed, boolean decrease )
Returns an item from the boss pool.

### GetTreasureItem()
#### CollectibleType GetTreasureItem ( int seed, boolean decrease )
Returns an item from the treasure room pool.

### GetPlanetariumItem()
#### CollectibleType GetPlanetariumItem ( int seed, boolean decrease )
Returns an item from the planetarium pool.

### GetOldChestItem()
#### CollectibleType GetOldChestItem ( int seed, boolean decrease )
Returns an item from the old chest pool.

### GetBeggarItem()
#### CollectibleType GetBeggarItem ( int seed, boolean decrease )
Returns an item from the beggar pool.

### GetDemonBeggarItem()
#### CollectibleType GetDemonBeggarItem ( int seed, boolean decrease )
Returns an item from the demon beggar pool.

### GetKeyMasterItem()
#### CollectibleType GetKeyMasterItem ( int seed, boolean decrease )
Returns an item from the key master pool.

### GetBatteryBeggarItem()
#### CollectibleType GetBatteryBeggarItem ( int seed, boolean decrease )
Returns an item from the battery beggar pool.

### GetRottenBeggarItem()
#### CollectibleType GetRottenBeggarItem ( int seed, boolean decrease )
Returns an item from the rotten beggar pool (REPENTANCE+).

## Item Pool Types

| Pool | Description |
|------|-------------|
| `POOL_TREASURE` | Treasure room items |
| `POOL_SHOP` | Shop items |
| `POOL_DEVIL` | Devil room items |
| `POOL_ANGEL` | Angel room items |
| `POOL_SECRET` | Secret room items |
| `POOL_SACRIFICE` | Sacrifice room items |
| `POOL_LIBRARY` | Library items |
| `POOL_CURSE` | Curse room items |
| `POOL_BOSS` | Boss room items |
| `POOL_PLANETARIUM` | Planetarium items |
| `POOL_OLD_CHEST` | Old chest items |
| `POOL_BEGGAR` | Beggar items |
| `POOL_DEMON_BEGGAR` | Demon beggar items |
| `POOL_KEY_MASTER` | Key master items |
| `POOL_BATTERY_BEGGAR` | Battery beggar items |
| `POOL_ROTTEN_BEGGAR` | Rotten beggar items |
| `POOL_MEGA_CHEST` | Mega chest items |
| `POOL_RED_CHEST` | Red chest items |
| `POOL_GOLDEN_CHEST` | Golden chest items |
| `POOL_WOODEN_CHEST` | Wooden chest items |
| `POOL_ETERNAL_CHEST` | Eternal chest items |
| `POOL_SPiked_CHEST` | Spiked chest items |
| `POOL_MIMIC_CHEST` | Mimic chest items |
| `POOL_ULLA` | Ulla items |
| `POOL_BOMB_CHEST` | Bomb chest items |
| `POOL_DAILY_RUN` | Daily run items |
| `POOL_ULTRA_SECRET` | Ultra secret room items |
| `POOL_BOMB_BUM` | Bomb bum items |
| `POOL_TRINKET` | Trinket pool |
| `POOL_CARD` | Card pool |
| `POOL_PILL` | Pill pool |
