namespace IsaacAgent.Core.Knowledge;

/// <summary>
/// Knowledge base of Binding of Isaac: Repentance mod callbacks.
/// Vanilla callback IDs (0-73) are sourced from the official IsaacDocs
/// <c>enums/ModCallbacks.md</c>. REPENTOGON-only extensions (IDs 1000+)
/// are stored in <see cref="RepentogonCallbacks"/> and
/// <see cref="RepentogonModifiedIds"/> to avoid name collisions with
/// vanilla callbacks that REPENTOGON overrides with enhanced behavior.
/// </summary>
public static class ModCallbacks
{
    /// <summary>
    /// All 74 vanilla callbacks with their canonical IDs (0-73),
    /// ordered by ID to match the official documentation.
    /// </summary>
    public static readonly Dictionary<string, CallbackInfo> Callbacks = new()
    {
        ["MC_NPC_UPDATE"] = new(0, "EntityNPC", "EntityType", "Called after an NPC is updated. Use OptionalArgs to filter by entity type."),
        ["MC_POST_UPDATE"] = new(1, "nil", "nil", "Called after every game update (30fps). No arguments."),
        ["MC_POST_RENDER"] = new(2, "nil", "nil", "Called after every game render frame (60fps). Use for rendering custom graphics."),
        ["MC_USE_ITEM"] = new(3, "CollectibleType, RNG, EntityPlayer, UseFlags, ActiveSlot, int", "CollectibleType", "Called when an active item is used. Return true to show use animation, false to skip. Can also return a table with Discharge/Remove/ShowAnim fields."),
        ["MC_POST_PEFFECT_UPDATE"] = new(4, "EntityPlayer", "PlayerType", "Called for each player each frame after item effects are evaluated."),
        ["MC_USE_CARD"] = new(5, "Card, EntityPlayer, UseFlags", "Card", "Called when a card or rune is used."),
        ["MC_FAMILIAR_UPDATE"] = new(6, "EntityFamiliar", "FamiliarVariant", "Called every frame for each familiar."),
        ["MC_FAMILIAR_INIT"] = new(7, "EntityFamiliar", "FamiliarVariant", "Called just after a familiar is initialized."),
        ["MC_EVALUATE_CACHE"] = new(8, "EntityPlayer, CacheFlag", "CacheFlag", "Called when a player's stats are re-evaluated. OptionalArgs must be a single CacheFlag."),
        ["MC_POST_PLAYER_INIT"] = new(9, "EntityPlayer", "PlayerVariant", "Called when a player entity is initialized."),
        ["MC_USE_PILL"] = new(10, "PillEffect, EntityPlayer, UseFlags", "PillEffect", "Called when a pill is used."),
        ["MC_ENTITY_TAKE_DMG"] = new(11, "Entity, float, DamageFlags, EntityRef, int", "EntityType", "Called before new damage is applied to an entity. Return true/nil to sustain damage, false to ignore."),
        ["MC_POST_CURSE_EVAL"] = new(12, "Curses (int)", "nil", "Called to evaluate level curses. Return int to override curse bitmask."),
        ["MC_INPUT_ACTION"] = new(13, "Entity, InputHook, ButtonAction", "InputHook", "Called for input actions. Return boolean or float to override input."),
        ["MC_LEVEL_GENERATOR"] = new(14, "nil", "nil", "Called during level generation."),
        ["MC_POST_GAME_STARTED"] = new(15, "bool (IsContinued)", "nil", "Called when a game starts. Boolean indicates if it's a continue."),
        ["MC_POST_GAME_END"] = new(16, "bool (IsGameOver)", "nil", "Called when a game ends. Boolean indicates if it's a game over."),
        ["MC_PRE_GAME_EXIT"] = new(17, "bool (ShouldSave)", "nil", "Called before game exits. Boolean indicates whether to save."),
        ["MC_POST_NEW_LEVEL"] = new(18, "nil", "nil", "Called when entering a new level."),
        ["MC_POST_NEW_ROOM"] = new(19, "nil", "nil", "Called when entering a new room."),
        ["MC_GET_CARD"] = new(20, "RNG, Card, bool, bool, bool", "nil", "Called to determine card. Return Card to override."),
        ["MC_GET_SHADER_PARAMS"] = new(21, "string (ShaderName)", "nil", "Called to get shader parameters. Return table."),
        ["MC_EXECUTE_CMD"] = new(22, "string, string", "nil", "Called when a console command is executed. Return string to output."),
        ["MC_PRE_USE_ITEM"] = new(23, "CollectibleType, RNG, EntityPlayer, UseFlags, ActiveSlot, int", "CollectibleType", "Called before item use. Return false to prevent use."),
        ["MC_PRE_ENTITY_SPAWN"] = new(24, "EntityType, int, int, Vector, Vector, Entity, int", "nil", "Called before entity spawn. Return table to override."),
        ["MC_POST_FAMILIAR_RENDER"] = new(25, "EntityFamiliar, Vector", "FamiliarVariant", "Called when rendering a familiar."),
        ["MC_PRE_FAMILIAR_COLLISION"] = new(26, "EntityFamiliar, Entity, bool", "FamiliarVariant", "Called before familiar collision. Return true to skip vanilla collision."),
        ["MC_POST_NPC_INIT"] = new(27, "EntityNPC", "EntityType", "Called when an NPC is initialized."),
        ["MC_POST_NPC_RENDER"] = new(28, "EntityNPC, Vector", "EntityType", "Called when rendering an NPC."),
        ["MC_POST_NPC_DEATH"] = new(29, "EntityNPC", "EntityType", "Called when an NPC dies."),
        ["MC_PRE_NPC_COLLISION"] = new(30, "EntityNPC, Entity, bool", "EntityType", "Called before NPC collision. Return true to skip vanilla collision."),
        ["MC_POST_PLAYER_UPDATE"] = new(31, "EntityPlayer", "PlayerVariant", "Called every update frame for players."),
        ["MC_POST_PLAYER_RENDER"] = new(32, "EntityPlayer, Vector", "PlayerVariant", "Called when rendering a player."),
        ["MC_PRE_PLAYER_COLLISION"] = new(33, "EntityPlayer, Entity, bool", "PlayerVariant", "Called before player collision. Return true to skip vanilla collision."),
        ["MC_POST_PICKUP_INIT"] = new(34, "EntityPickup", "PickupVariant", "Called when a pickup is initialized."),
        ["MC_POST_PICKUP_UPDATE"] = new(35, "EntityPickup", "PickupVariant", "Called every update frame for pickups."),
        ["MC_POST_PICKUP_RENDER"] = new(36, "EntityPickup, Vector", "PickupVariant", "Called when rendering a pickup."),
        ["MC_POST_PICKUP_SELECTION"] = new(37, "EntityPickup, int, int", "nil", "Called to determine pickup selection. Return table with Variant and SubType."),
        ["MC_PRE_PICKUP_COLLISION"] = new(38, "EntityPickup, Entity, bool", "PickupVariant", "Called before pickup collision. Return true to skip vanilla collision."),
        ["MC_POST_TEAR_INIT"] = new(39, "EntityTear", "TearVariant", "Called when a tear is initialized."),
        ["MC_POST_TEAR_UPDATE"] = new(40, "EntityTear", "TearVariant", "Called every update frame for tears."),
        ["MC_POST_TEAR_RENDER"] = new(41, "EntityTear, Vector", "TearVariant", "Called when rendering a tear."),
        ["MC_PRE_TEAR_COLLISION"] = new(42, "EntityTear, Entity, bool", "TearVariant", "Called before tear collision. Return true to skip vanilla collision."),
        ["MC_POST_PROJECTILE_INIT"] = new(43, "EntityProjectile", "ProjectileVariant", "Called when a projectile is initialized."),
        ["MC_POST_PROJECTILE_UPDATE"] = new(44, "EntityProjectile", "ProjectileVariant", "Called every update frame for projectiles."),
        ["MC_POST_PROJECTILE_RENDER"] = new(45, "EntityProjectile, Vector", "ProjectileVariant", "Called when rendering a projectile."),
        ["MC_PRE_PROJECTILE_COLLISION"] = new(46, "EntityProjectile, Entity, bool", "ProjectileVariant", "Called before projectile collision. Return true to skip vanilla collision."),
        ["MC_POST_LASER_INIT"] = new(47, "EntityLaser", "LaserVariant", "Called when a laser is initialized."),
        ["MC_POST_LASER_UPDATE"] = new(48, "EntityLaser", "LaserVariant", "Called every update frame for lasers."),
        ["MC_POST_LASER_RENDER"] = new(49, "EntityLaser, Vector", "LaserVariant", "Called when rendering a laser."),
        ["MC_POST_KNIFE_INIT"] = new(50, "EntityKnife", "KnifeSubType", "Called when a knife is initialized."),
        ["MC_POST_KNIFE_UPDATE"] = new(51, "EntityKnife", "KnifeSubType", "Called every update frame for knives."),
        ["MC_POST_KNIFE_RENDER"] = new(52, "EntityKnife, Vector", "KnifeSubType", "Called when rendering a knife."),
        ["MC_PRE_KNIFE_COLLISION"] = new(53, "EntityKnife, Entity, bool", "KnifeSubType", "Called before knife collision. Return true to skip vanilla collision."),
        ["MC_POST_EFFECT_INIT"] = new(54, "EntityEffect", "EffectVariant", "Called when an effect is initialized."),
        ["MC_POST_EFFECT_UPDATE"] = new(55, "EntityEffect", "EffectVariant", "Called every update frame for effects."),
        ["MC_POST_EFFECT_RENDER"] = new(56, "EntityEffect, Vector", "EffectVariant", "Called when rendering an effect."),
        ["MC_POST_BOMB_INIT"] = new(57, "EntityBomb", "BombVariant", "Called when a bomb is initialized."),
        ["MC_POST_BOMB_UPDATE"] = new(58, "EntityBomb", "BombVariant", "Called every update frame for bombs."),
        ["MC_POST_BOMB_RENDER"] = new(59, "EntityBomb, Vector", "BombVariant", "Called when rendering a bomb."),
        ["MC_PRE_BOMB_COLLISION"] = new(60, "EntityBomb, Entity, bool", "BombVariant", "Called before bomb collision. Return true to skip vanilla collision."),
        ["MC_POST_FIRE_TEAR"] = new(61, "EntityTear", "nil", "Called when a tear is fired."),
        ["MC_PRE_GET_COLLECTIBLE"] = new(62, "RNG, CollectibleType, bool, bool, bool", "nil", "Called when the game selects a collectible. Return CollectibleType to override."),
        ["MC_POST_GET_COLLECTIBLE"] = new(63, "CollectibleType, RNG, bool, bool, bool", "nil", "Called after a collectible is selected."),
        ["MC_GET_PILL_COLOR"] = new(64, "RNG, int, bool", "nil", "Called to determine pill color. Return PillColor to override."),
        ["MC_GET_PILL_EFFECT"] = new(65, "RNG, PillEffect, bool", "nil", "Called to determine pill effect. Return PillEffect to override."),
        ["MC_GET_TRINKET"] = new(66, "RNG, TrinketType", "nil", "Called to determine trinket. Return TrinketType to override."),
        ["MC_POST_ENTITY_REMOVE"] = new(67, "Entity", "EntityType", "Called when an entity is removed."),
        ["MC_POST_ENTITY_KILL"] = new(68, "Entity", "EntityType", "Called when an entity is killed."),
        ["MC_PRE_NPC_UPDATE"] = new(69, "EntityNPC", "EntityType", "Called before NPC update. Return true to skip vanilla update."),
        ["MC_PRE_SPAWN_CLEAN_AWARD"] = new(70, "Entity", "nil", "Called before spawning clean award. Return true to skip."),
        ["MC_PRE_ROOM_ENTITY_SPAWN"] = new(71, "EntityType, int, int", "nil", "Called before room entity spawn. Return table to override."),
        ["MC_PRE_ENTITY_DEVOLVE"] = new(72, "Entity", "EntityType", "Called before an entity devolves."),
        ["MC_PRE_MOD_UNLOAD"] = new(73, "nil", "nil", "Called before a mod is unloaded."),
    };

    /// <summary>
    /// REPENTOGON overrides of vanilla callbacks. These share names with
    /// vanilla callbacks but have new IDs (1000+) and enhanced behavior
    /// (e.g., additional arguments or return table support). The vanilla
    /// entry in <see cref="Callbacks"/> remains the base reference; this
    /// map provides the REPENTOGON-specific ID for tools that need it.
    /// </summary>
    public static readonly Dictionary<string, int> RepentogonModifiedIds = new()
    {
        ["MC_USE_PILL"] = 1064,
        ["MC_PRE_PLAYER_COLLISION"] = 1065,
        ["MC_PRE_TEAR_COLLISION"] = 1232,
        ["MC_PRE_FAMILIAR_COLLISION"] = 1234,
        ["MC_PRE_BOMB_COLLISION"] = 1236,
        ["MC_PRE_PICKUP_COLLISION"] = 1238,
        ["MC_PRE_KNIFE_COLLISION"] = 1242,
        ["MC_PRE_PROJECTILE_COLLISION"] = 1244,
        ["MC_PRE_NPC_COLLISION"] = 1246,
        ["MC_ENTITY_TAKE_DMG"] = 1007,
        ["MC_HUD_UPDATE"] = 1020,
        ["MC_HUD_POST_UPDATE"] = 1021,
    };

    /// <summary>
    /// Callbacks introduced exclusively by REPENTOGON (IDs 1000+).
    /// These do not exist in vanilla Repentance.
    /// </summary>
    public static readonly Dictionary<string, CallbackInfo> RepentogonCallbacks = new()
    {
        ["MC_PRE_ADD_COLLECTIBLE"] = new(1004, "CollectibleType, int, bool, ActiveSlot, int, EntityPlayer", "CollectibleType", "REPENTOGON: Called before a collectible is added to a player. Return table or CollectibleType to override."),
        ["MC_POST_ADD_COLLECTIBLE"] = new(1005, "CollectibleType, int, bool, ActiveSlot, int, EntityPlayer", "CollectibleType", "REPENTOGON: Called after a collectible is added to a player."),
        ["MC_POST_ENTITY_TAKE_DMG"] = new(1006, "Entity, float, DamageFlags, EntityRef, int", "EntityType", "REPENTOGON: Called after an entity takes damage (after MC_ENTITY_TAKE_DMG)."),
        ["MC_PRE_PLAYER_TAKE_DMG"] = new(1008, "EntityPlayer, float, DamageFlags, EntityRef, int", "PlayerVariant", "REPENTOGON: Called before a player takes damage, earlier than MC_ENTITY_TAKE_DMG. Return false to prevent."),
        ["MC_GRID_ROCK_UPDATE"] = new(1010, "GridEntityRock", "nil", "REPENTOGON: Called for grid rock updates."),
        ["MC_POST_GRID_ROCK_DESTROY"] = new(1011, "GridEntityRock, bool", "nil", "REPENTOGON: Called after a grid rock is destroyed."),
        ["MC_GRID_HURT_DAMAGE"] = new(1012, "GridEntity, float, DamageFlags, EntityRef, int", "nil", "REPENTOGON: Called when a grid entity takes damage."),
        ["MC_POST_GRID_HURT_DAMAGE"] = new(1013, "GridEntity, float, DamageFlags, EntityRef, int", "nil", "REPENTOGON: Called after a grid entity takes damage."),
        ["MC_HUD_RENDER"] = new(1022, "nil", "nil", "REPENTOGON: Called when the HUD is rendered."),
        ["MC_MAIN_MENU_RENDER"] = new(1023, "nil", "nil", "REPENTOGON: Called when the main menu is rendered."),
        ["MC_PRE_PAUSE_SCREEN_RENDER"] = new(1218, "nil", "nil", "REPENTOGON: Called before the pause screen is rendered."),
        ["MC_POST_PAUSE_SCREEN_RENDER"] = new(1219, "nil", "nil", "REPENTOGON: Called after the pause screen is rendered."),
        ["MC_PRE_COMPLETION_MARKS_RENDER"] = new(1216, "nil", "nil", "REPENTOGON: Called before completion marks are rendered."),
        ["MC_POST_COMPLETION_MARKS_RENDER"] = new(1217, "nil", "nil", "REPENTOGON: Called after completion marks are rendered."),
        ["MC_PRE_SFX_PLAY"] = new(1030, "int, float, int, bool, bool", "int", "REPENTOGON: Called before a sound effect plays. Return table to override."),
        ["MC_POST_SFX_PLAY"] = new(1031, "int, float, int, bool, bool", "nil", "REPENTOGON: Called after a sound effect plays."),
        ["MC_PRE_MUSIC_PLAY"] = new(1034, "int, float, bool", "int", "REPENTOGON: Called before music plays. Return int to override music ID."),
        ["MC_PRE_MUSIC_LAYER_TOGGLE"] = new(1035, "int, int, bool", "nil", "REPENTOGON: Called before a music layer is toggled."),
        ["MC_PRE_RENDER_PLAYER_HEAD"] = new(1038, "Vector, Vector, EntityPlayer", "nil", "REPENTOGON: Called before rendering a player's head. Return Vector to offset."),
        ["MC_PRE_RENDER_PLAYER_BODY"] = new(1039, "Vector, Vector, EntityPlayer", "nil", "REPENTOGON: Called before rendering a player's body. Return Vector to offset."),
        ["MC_PRE_ENTITY_THROW"] = new(1040, "Entity, Entity, Vector", "nil", "REPENTOGON: Called before an entity is thrown. Return Vector to override velocity."),
        ["MC_POST_ENTITY_THROW"] = new(1041, "Entity, Entity, Vector", "nil", "REPENTOGON: Called after an entity is thrown."),
        ["MC_PLAYER_INIT_POST_LEVEL_INIT_STATS"] = new(1042, "EntityPlayer", "nil", "REPENTOGON: Called after player init and level init stats are applied."),
        ["MC_PRE_ROOM_EXIT"] = new(1043, "nil", "nil", "REPENTOGON: Called before exiting a room."),
        ["MC_PRE_COMPLETION_EVENT"] = new(1049, "int, EntityPlayer", "nil", "REPENTOGON: Called before a completion event triggers. Return true to skip."),
        ["MC_COMPLETION_MARK_GET"] = new(1047, "int, EntityPlayer", "nil", "REPENTOGON: Called when a completion mark is obtained."),
        ["MC_PRE_LEVEL_INIT"] = new(1060, "nil", "nil", "REPENTOGON: Called before level initialization."),
        ["MC_PRE_TRIGGER_PLAYER_DEATH"] = new(1050, "nil", "nil", "REPENTOGON: Called before triggering player death. Return true to prevent."),
        ["MC_PRE_RESTOCK_SHOP"] = new(1070, "nil", "nil", "REPENTOGON: Called before shop restock. Return true to skip."),
        ["MC_POST_RESTOCK_SHOP"] = new(1071, "nil", "nil", "REPENTOGON: Called after shop restock."),
        ["MC_PRE_CHANGE_ROOM"] = new(1061, "nil", "nil", "REPENTOGON: Called before room change."),
        ["MC_POST_PICKUP_SHOP_PURCHASE"] = new(1062, "EntityPickup, EntityPlayer, int", "nil", "REPENTOGON: Called after a shop pickup is purchased."),
        ["MC_GET_FOLLOWER_PRIORITY"] = new(1063, "EntityFamiliar", "nil", "REPENTOGON: Called to get follower priority. Return int."),
        ["MC_PRE_PICKUP_MORPH"] = new(1213, "EntityPickup, EntityType, int, int", "nil", "REPENTOGON: Called before a pickup morphs. Return true to skip."),
        ["MC_POST_PICKUP_MORPH"] = new(1215, "EntityPickup, EntityType, int, int", "nil", "REPENTOGON: Called after a pickup morphs."),
        ["MC_NPC_PICK_TARGET"] = new(1222, "EntityNPC, Entity", "nil", "REPENTOGON: Called when an NPC picks a target. Return Entity to override."),
        ["MC_PRE_NPC_MORPH"] = new(1212, "EntityNPC, EntityType, int, int", "nil", "REPENTOGON: Called before an NPC morphs. Return true to skip."),
        ["MC_POST_NPC_MORPH"] = new(1214, "EntityNPC, EntityType, int, int", "nil", "REPENTOGON: Called after an NPC morphs."),
        ["MC_PRE_USE_CARD"] = new(1064, "Card, EntityPlayer, UseFlags", "Card", "REPENTOGON: Called before a card is used. Return false to prevent."),
        ["MC_PRE_USE_PILL"] = new(1065, "PillEffect, EntityPlayer, UseFlags", "PillEffect", "REPENTOGON: Called before a pill is used. Return false to prevent."),
        ["MC_GET_SHOP_ITEM_PRICE"] = new(1066, "EntityPickup, int", "nil", "REPENTOGON: Called to get shop item price. Return int to override."),
        ["MC_PLAYER_GET_HEALTH_TYPE"] = new(1067, "EntityPlayer", "nil", "REPENTOGON: Called to get player health type. Return int."),
        ["MC_PRE_FAMILIAR_RENDER"] = new(1080, "EntityFamiliar, Vector", "FamiliarVariant", "REPENTOGON: Called before familiar render. Return Vector to offset."),
        ["MC_PRE_NPC_RENDER"] = new(1081, "EntityNPC, Vector", "EntityType", "REPENTOGON: Called before NPC render. Return Vector to offset."),
        ["MC_PRE_PLAYER_RENDER"] = new(1082, "EntityPlayer, Vector", "PlayerVariant", "REPENTOGON: Called before player render. Return Vector to offset."),
        ["MC_PRE_PICKUP_RENDER"] = new(1083, "EntityPickup, Vector", "PickupVariant", "REPENTOGON: Called before pickup render. Return Vector to offset."),
        ["MC_PRE_TEAR_RENDER"] = new(1084, "EntityTear, Vector", "TearVariant", "REPENTOGON: Called before tear render. Return Vector to offset."),
        ["MC_PRE_PROJECTILE_RENDER"] = new(1085, "EntityProjectile, Vector", "ProjectileVariant", "REPENTOGON: Called before projectile render. Return Vector to offset."),
        ["MC_PRE_KNIFE_RENDER"] = new(1086, "EntityKnife, Vector", "KnifeSubType", "REPENTOGON: Called before knife render. Return Vector to offset."),
        ["MC_PRE_EFFECT_RENDER"] = new(1087, "EntityEffect, Vector", "EffectVariant", "REPENTOGON: Called before effect render. Return Vector to offset."),
        ["MC_PRE_BOMB_RENDER"] = new(1088, "EntityBomb, Vector", "BombVariant", "REPENTOGON: Called before bomb render. Return Vector to offset."),
        ["MC_PRE_SLOT_RENDER"] = new(1089, "EntitySlot, Vector", "nil", "REPENTOGON: Called before slot render. Return Vector to offset."),
        ["MC_POST_SLOT_RENDER"] = new(1090, "EntitySlot, Vector", "nil", "REPENTOGON: Called after slot render."),
        ["MC_PRE_GRID_ENTITY_SPAWN"] = new(1100, "int, int, int, int, bool", "nil", "REPENTOGON: Called before a grid entity spawns. Return true to skip."),
        ["MC_PRE_ROOM_GRID_ENTITY_SPAWN"] = new(1192, "int, int, int, int, bool", "nil", "REPENTOGON: Called before a room grid entity spawns. Return true to skip."),
        ["MC_POST_GRID_ENTITY_SPAWN"] = new(1101, "GridEntity", "nil", "REPENTOGON: Called after a grid entity spawns."),
        ["MC_PRE_ROOM_TRIGGER_CLEAR"] = new(1068, "nil", "nil", "REPENTOGON: Called before room triggers clear. Return true to skip."),
        ["MC_PRE_PLAYER_TRIGGER_ROOM_CLEAR"] = new(1069, "EntityPlayer", "nil", "REPENTOGON: Called before player triggers room clear. Return true to skip."),
        ["MC_PLAYER_GET_ACTIVE_MAX_CHARGE"] = new(1072, "EntityPlayer, int", "nil", "REPENTOGON: Called to get active item max charge. Return int."),
        ["MC_PLAYER_GET_ACTIVE_MIN_USABLE_CHARGE"] = new(1073, "EntityPlayer, int", "nil", "REPENTOGON: Called to get active item min usable charge. Return int."),
        ["MC_PRE_PLAYER_USE_BOMB"] = new(1020, "EntityPlayer, Vector", "nil", "REPENTOGON: Called before a player uses a bomb. Return true to skip."),
        ["MC_POST_PLAYER_USE_BOMB"] = new(1021, "EntityPlayer, Vector", "nil", "REPENTOGON: Called after a player uses a bomb."),
        ["MC_PRE_REPLACE_SPRITESHEET"] = new(1116, "int, int", "nil", "REPENTOGON: Called before a spritesheet is replaced."),
        ["MC_POST_REPLACE_SPRITESHEET"] = new(1117, "int, int", "nil", "REPENTOGON: Called after a spritesheet is replaced."),
        ["MC_PLAYER_GET_HEART_LIMIT"] = new(1074, "EntityPlayer, int, int", "nil", "REPENTOGON: Called to get player heart limit. Return int."),
        ["MC_PRE_PLANETARIUM_APPLY_STAGE_PENALTY"] = new(1110, "nil", "nil", "REPENTOGON: Called before planetarium stage penalty. Return true to skip."),
        ["MC_PRE_PLANETARIUM_APPLY_PLANETARIUM_PENALTY"] = new(1111, "nil", "nil", "REPENTOGON: Called before planetarium penalty. Return true to skip."),
        ["MC_PRE_PLANETARIUM_APPLY_TREASURE_PENALTY"] = new(1112, "nil", "nil", "REPENTOGON: Called before planetarium treasure penalty. Return true to skip."),
        ["MC_PRE_PLANETARIUM_APPLY_ITEMS"] = new(1113, "nil", "nil", "REPENTOGON: Called before planetarium item bonuses. Return float to override."),
        ["MC_PRE_PLANETARIUM_APPLY_TELESCOPE_LENS"] = new(1114, "nil", "nil", "REPENTOGON: Called before telescope lens bonus. Return float to override."),
        ["MC_POST_PLANETARIUM_CALCULATE"] = new(1115, "float", "nil", "REPENTOGON: Called after planetarium chance is calculated."),
        ["MC_PRE_SLOT_INIT"] = new(1121, "EntitySlot", "nil", "REPENTOGON: Called when a slot machine is initialized."),
        ["MC_POST_SLOT_UPDATE"] = new(1122, "EntitySlot", "nil", "REPENTOGON: Called every update frame for slot machines."),
        ["MC_PRE_SLOT_COLLISION"] = new(1240, "EntitySlot, Entity, bool", "nil", "REPENTOGON: Called before slot collision. Return true to skip."),
        ["MC_POST_SLOT_COLLISION"] = new(1241, "EntitySlot, Entity, bool", "nil", "REPENTOGON: Called after slot collision."),
        ["MC_PRE_SLOT_CREATE_EXPLOSION_DROPS"] = new(1123, "EntitySlot", "nil", "REPENTOGON: Called before slot creates explosion drops. Return true to skip."),
        ["MC_POST_SLOT_CREATE_EXPLOSION_DROPS"] = new(1124, "EntitySlot", "nil", "REPENTOGON: Called after slot creates explosion drops."),
        ["MC_PRE_SLOT_SET_PRIZE_COLLECTIBLE"] = new(1125, "EntitySlot, CollectibleType", "nil", "REPENTOGON: Called before slot sets prize collectible. Return CollectibleType to override."),
        ["MC_POST_SLOT_SET_PRIZE_COLLECTIBLE"] = new(1126, "EntitySlot, CollectibleType", "nil", "REPENTOGON: Called after slot sets prize collectible."),
        ["MC_POST_PLAYER_COLLISION"] = new(1231, "EntityPlayer, Entity, bool", "PlayerVariant", "REPENTOGON: Called after player collision."),
        ["MC_POST_TEAR_COLLISION"] = new(1233, "EntityTear, Entity, bool", "TearVariant", "REPENTOGON: Called after tear collision."),
        ["MC_POST_FAMILIAR_COLLISION"] = new(1235, "EntityFamiliar, Entity, bool", "FamiliarVariant", "REPENTOGON: Called after familiar collision."),
        ["MC_POST_BOMB_COLLISION"] = new(1237, "EntityBomb, Entity, bool", "BombVariant", "REPENTOGON: Called after bomb collision."),
        ["MC_POST_PICKUP_COLLISION"] = new(1239, "EntityPickup, Entity, bool", "PickupVariant", "REPENTOGON: Called after pickup collision."),
        ["MC_POST_KNIFE_COLLISION"] = new(1243, "EntityKnife, Entity, bool", "KnifeSubType", "REPENTOGON: Called after knife collision."),
        ["MC_POST_PROJECTILE_COLLISION"] = new(1245, "EntityProjectile, Entity, bool", "ProjectileVariant", "REPENTOGON: Called after projectile collision."),
        ["MC_POST_NPC_COLLISION"] = new(1247, "EntityNPC, Entity, bool", "EntityType", "REPENTOGON: Called after NPC collision."),
        ["MC_PRE_LASER_COLLISION"] = new(1248, "EntityLaser, Entity, bool", "LaserVariant", "REPENTOGON: Called before laser collision. Return true to skip."),
        ["MC_POST_LASER_COLLISION"] = new(1249, "EntityLaser, Entity, bool", "LaserVariant", "REPENTOGON: Called after laser collision."),
        ["MC_PRE_DEVIL_APPLY_STAGE_PENALTY"] = new(1131, "nil", "nil", "REPENTOGON: Called before devil room stage penalty. Return true to skip."),
        ["MC_POST_DEVIL_CALCULATE"] = new(1133, "float", "nil", "REPENTOGON: Called after devil room chance is calculated."),
        ["MC_POST_ITEM_OVERLAY_UPDATE"] = new(1075, "int", "nil", "REPENTOGON: Called every update frame for item overlays."),
        ["MC_PRE_ITEM_OVERLAY_SHOW"] = new(1076, "int, EntityPlayer", "nil", "REPENTOGON: Called before item overlay shows. Return true to skip."),
        ["MC_POST_PLAYER_NEW_ROOM_TEMP_EFFECTS"] = new(1077, "EntityPlayer", "nil", "REPENTOGON: Called for player new room temp effects."),
        ["MC_POST_PLAYER_NEW_LEVEL"] = new(1078, "EntityPlayer", "nil", "REPENTOGON: Called when player enters new level."),
        ["MC_POST_PLAYERHUD_RENDER_ACTIVE_ITEM"] = new(1079, "Vector, Vector", "nil", "REPENTOGON: Called when rendering active item in player HUD."),
        ["MC_POST_PLAYERHUD_RENDER_HEARTS"] = new(1091, "EntityPlayer, Vector", "nil", "REPENTOGON: Called when rendering hearts in player HUD."),
        ["MC_PRE_GET_LIGHTING_ALPHA"] = new(1150, "nil", "nil", "REPENTOGON: Called to get lighting alpha. Return float."),
        ["MC_PRE_RENDER_GRID_LIGHTING"] = new(1151, "nil", "nil", "REPENTOGON: Called before rendering grid lighting."),
        ["MC_PRE_RENDER_ENTITY_LIGHTING"] = new(1152, "Entity", "nil", "REPENTOGON: Called before rendering entity lighting."),
        ["MC_PRE_PLAYER_APPLY_INNATE_COLLECTIBLE_NUM"] = new(1092, "EntityPlayer, CollectibleType, int", "nil", "REPENTOGON: Called before applying innate collectible count. Return int to override."),
        ["MC_PRE_PLAYER_HAS_COLLECTIBLE"] = new(1093, "EntityPlayer, CollectibleType", "nil", "REPENTOGON: Called before checking if player has collectible. Return bool to override."),
        ["MC_PRE_MUSIC_PLAY_JINGLE"] = new(1094, "int", "nil", "REPENTOGON: Called before a music jingle plays. Return int to override."),
        ["MC_POST_TRIGGER_COLLECTIBLE_REMOVED"] = new(1095, "EntityPlayer, CollectibleType", "nil", "REPENTOGON: Called after a collectible is removed from player."),
        ["MC_POST_TRIGGER_TRINKET_ADDED"] = new(1096, "EntityPlayer, TrinketType", "nil", "REPENTOGON: Called after a trinket is added to player."),
        ["MC_POST_TRIGGER_TRINKET_REMOVED"] = new(1097, "EntityPlayer, TrinketType", "nil", "REPENTOGON: Called after a trinket is removed from player."),
        ["MC_POST_TRIGGER_WEAPON_FIRED"] = new(1098, "Entity, int", "nil", "REPENTOGON: Called after a weapon is fired."),
        ["MC_POST_LEVEL_LAYOUT_GENERATED"] = new(1099, "table", "nil", "REPENTOGON: Called after level layout is generated."),
        ["MC_POST_NIGHTMARE_SCENE_RENDER"] = new(1102, "nil", "nil", "REPENTOGON: Called when nightmare scene is rendered."),
        ["MC_POST_NIGHTMARE_SCENE_SHOW"] = new(1103, "nil", "nil", "REPENTOGON: Called when nightmare scene is shown."),
        ["MC_POST_WEAPON_FIRE"] = new(1105, "Entity, int", "nil", "REPENTOGON: Called after a weapon fires."),
        ["MC_CONSOLE_AUTOCOMPLETE"] = new(1120, "string, AutocompleteType", "nil", "REPENTOGON: Called for console autocomplete. Return table of strings."),
        ["MC_PLAYER_INIT_PRE_LEVEL_INIT_STATS"] = new(1127, "EntityPlayer", "nil", "REPENTOGON: Called before player init and level init stats are applied."),
        ["MC_POST_SAVESLOT_LOAD"] = new(1140, "int", "nil", "REPENTOGON: Called after a save slot is loaded."),
        ["MC_PRE_NEW_ROOM"] = new(1200, "nil", "nil", "REPENTOGON: Called before entering a new room."),
        ["MC_PRE_MEGA_SATAN_ENDING"] = new(1201, "nil", "nil", "REPENTOGON: Called before Mega Satan ending. Return true to skip."),
        ["MC_POST_MODS_LOADED"] = new(1210, "nil", "nil", "REPENTOGON: Called after all mods are loaded."),
        ["MC_POST_ITEM_OVERLAY_SHOW"] = new(1134, "int, EntityPlayer", "nil", "REPENTOGON: Called after item overlay shows."),
        ["MC_PRE_LEVEL_PLACE_ROOM"] = new(1137, "table", "nil", "REPENTOGON: Called before a room is placed during level generation. Return table to override."),
        ["MC_PRE_M_MORPH_ACTIVE"] = new(1190, "EntityNPC", "nil", "REPENTOGON: Called before M (Delirium boss) morphs. Return true to skip."),
        ["MC_PRE_NPC_SPLIT"] = new(1191, "EntityNPC", "nil", "REPENTOGON: Called before an NPC splits. Return true to skip."),
        ["MC_POST_PLAYER_GET_MULTI_SHOT_PARAMS"] = new(1251, "EntityPlayer", "nil", "REPENTOGON: Called after getting multi-shot params."),
        ["MC_POST_FAMILIAR_FIRE_PROJECTILE"] = new(1252, "EntityFamiliar, EntityTear", "nil", "REPENTOGON: Called after a familiar fires a projectile."),
        ["MC_POST_FIRE_BOMB"] = new(1253, "EntityBomb", "nil", "REPENTOGON: Called after a bomb is fired."),
        ["MC_POST_FIRE_BONE_CLUB"] = new(1254, "EntityKnife", "nil", "REPENTOGON: Called after a bone club is fired."),
        ["MC_POST_FIRE_BRIMSTONE"] = new(1255, "EntityLaser", "nil", "REPENTOGON: Called after brimstone is fired."),
        ["MC_POST_FIRE_BRIMSTONE_BALL"] = new(1256, "EntityEffect", "nil", "REPENTOGON: Called after a brimstone ball is fired."),
        ["MC_POST_FIRE_KNIFE"] = new(1257, "EntityKnife", "nil", "REPENTOGON: Called after a knife is fired."),
        ["MC_POST_FIRE_SWORD"] = new(1258, "EntityEffect", "nil", "REPENTOGON: Called after a sword is fired."),
        ["MC_POST_FIRE_TECH_LASER"] = new(1259, "EntityLaser", "nil", "REPENTOGON: Called after a Tech laser is fired."),
        ["MC_POST_FIRE_TECH_X_LASER"] = new(1260, "EntityLaser", "nil", "REPENTOGON: Called after a Tech X laser is fired."),
        ["MC_POST_FAMILIAR_FIRE_BRIMSTONE"] = new(1261, "EntityFamiliar, EntityLaser", "nil", "REPENTOGON: Called after a familiar fires brimstone."),
        ["MC_POST_FAMILIAR_FIRE_TECH_LASER"] = new(1262, "EntityFamiliar, EntityLaser", "nil", "REPENTOGON: Called after a familiar fires a Tech laser."),
        ["MC_IS_PERSISTENT_ROOM_ENTITY"] = new(1263, "EntityType, int, int", "nil", "REPENTOGON: Called to check if a room entity is persistent. Return bool."),
        ["MC_PRE_PLAYERHUD_TRINKET_RENDER"] = new(1264, "EntityPlayer, Vector, TrinketType", "nil", "REPENTOGON: Called before rendering trinket in player HUD. Return Vector to offset."),
    };

    /// <summary>
    /// Looks up a callback by name across both vanilla and REPENTOGON
    /// dictionaries. Returns the vanilla entry if the name exists in
    /// <see cref="Callbacks"/>, otherwise checks
    /// <see cref="RepentogonCallbacks"/>.
    /// </summary>
    public static CallbackInfo? Lookup(string name)
    {
        if (Callbacks.TryGetValue(name, out var vanilla))
            return vanilla;
        if (RepentogonCallbacks.TryGetValue(name, out var rep))
            return rep;
        return null;
    }

    /// <summary>
    /// Returns the REPENTOGON override ID for a vanilla callback name,
    /// or null if REPENTOGON does not override it.
    /// </summary>
    public static int? GetRepentogonId(string name)
        => RepentogonModifiedIds.TryGetValue(name, out var id) ? id : null;

    /// <summary>
    /// Describes a single mod callback: its numeric ID, argument types,
    /// optional filter argument, and documentation string.
    /// </summary>
    public record CallbackInfo(int Id, string Args, string OptionalArgs, string Description);
}
