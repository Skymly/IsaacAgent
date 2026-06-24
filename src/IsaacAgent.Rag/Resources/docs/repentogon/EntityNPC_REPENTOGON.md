---
tags:
  - Class
  - REPENTOGON
---
# Class "EntityNPC" (REPENTOGON Extensions)

REPENTOGON extends the vanilla `EntityNPC` class with additional methods for
NPC AI control, custom boss behavior, and advanced entity manipulation.

## Functions

### GetBossID()
#### int GetBossID ( )
Returns the boss ID if this NPC is a boss, or 0 if it's not a boss.

### SetBossID()
#### void SetBossID ( int bossID )
Sets the boss ID for this NPC. This marks it as a boss for the minimap
and boss door logic.

### GetNPCState()
#### int GetNPCState ( )
Returns the NPC's current AI state index.

### SetNPCState()
#### void SetNPCState ( int state )
Sets the NPC's AI state directly. Useful for forcing specific behaviors.

### GetSprite()
#### Sprite GetSprite ( )
Returns the NPC's sprite object for animation control.

### GetPathFinder()
#### PathFinder GetPathFinder ( )
Returns the pathfinder for this NPC (used for navigation).

### GetShadowSize()
#### float GetShadowSize ( )
Returns the shadow size of the NPC.

### SetShadowSize()
#### void SetShadowSize ( float size )
Sets the shadow size of the NPC.

### GetScale()
#### float GetScale ( )
Returns the NPC's render scale.

### SetScale()
#### void SetScale ( float scale )
Sets the NPC's render scale. Larger values make the NPC appear bigger.

### GetSize()
#### float GetSize ( )
Returns the NPC's collision size.

### SetSize()
#### void SetSize ( float size )
Sets the NPC's collision size.

### GetCollisionDamage()
#### float GetCollisionDamage ( )
Returns the damage dealt to players on contact.

### SetCollisionDamage()
#### void SetCollisionDamage ( float damage )
Sets the contact damage value.

### GetMaxHitPoints()
#### float GetMaxHitPoints ( )
Returns the NPC's maximum HP.

### SetMaxHitPoints()
#### void SetMaxHitPoints ( float hp )
Sets the NPC's maximum HP.

### GetHitPoints()
#### float GetHitPoints ( )
Returns the NPC's current HP.

### SetHitPoints()
#### void SetHitPoints ( float hp )
Sets the NPC's current HP directly.

### GetVelocity()
#### Vector GetVelocity ( )
Returns the NPC's current velocity.

### SetVelocity()
#### void SetVelocity ( Vector velocity )
Sets the NPC's velocity directly.

### GetPosition()
#### Vector GetPosition ( )
Returns the NPC's current position.

### SetPosition()
#### void SetPosition ( Vector position )
Sets the NPC's position directly.

### GetSpawner()
#### Entity GetSpawner ( )
Returns the entity that spawned this NPC (if any).

### SetSpawner()
#### void SetSpawner ( Entity spawner )
Sets the spawner entity for this NPC.

### GetSpawnerType()
#### EntityType GetSpawnerType ( )
Returns the entity type of the spawner.

### GetSpawnerVariant()
#### int GetSpawnerVariant ( )
Returns the variant of the spawner.

### GetFlags()
#### int GetFlags ( )
Returns the NPC's entity flags.

### SetFlags()
#### void SetFlags ( int flags )
Sets the NPC's entity flags.

### AddFlags()
#### void AddFlags ( int flags )
Adds flags to the NPC.

### ClearFlags()
#### void ClearFlags ( int flags )
Removes flags from the NPC.

### HasFlags()
#### bool HasFlags ( int flags )
Returns true if the NPC has all the specified flags.

### IsBoss()
#### bool IsBoss ( )
Returns true if this NPC is a boss.

### IsVulnerableEnemy()
#### bool IsVulnerableEnemy ( )
Returns true if the NPC is an enemy that can take damage.

### IsFlying()
#### bool IsFlying ( )
Returns true if the NPC is flying (ignores grid entities).

### SetFlying()
#### void SetFlying ( bool flying )
Sets whether the NPC is flying.

### GetColor()
#### Color GetColor ( )
Returns the NPC's current color modifier.

### SetColor()
#### void SetColor ( Color color, int duration, int priority, boolean fade, boolean share )
Sets the NPC's color with duration and priority.

### GetControllerId()
#### int GetControllerId ( )
Returns the controller ID for this NPC (for player-controlled NPCs).

### GetPlayerIndex()
#### int GetPlayerIndex ( )
Returns the player index if this NPC is player-controlled.

### GetData()
#### table GetData ( )
Returns the NPC's persistent data table. Data stored here persists
for the NPC's lifetime.

### GetEntityConfig()
#### EntityConfigEntity GetEntityConfig ( )
Returns the entity configuration for this NPC (REPENTOGON specific).

### CanShutDoors()
#### bool CanShutDoors ( )
Returns true if this NPC can cause doors to close (i.e., it's a threat).

### SetShutDoors()
#### void SetShutDoors ( bool canShut )
Sets whether this NPC can cause doors to close.

### GetProjectileCooldown()
#### int GetProjectileCooldown ( )
Returns the cooldown before the NPC can fire another projectile.

### SetProjectileCooldown()
#### void SetProjectileCooldown ( int cooldown )
Sets the projectile cooldown.

### GetChildNPCs()
#### table GetChildNPCs ( )
Returns a table of child NPCs spawned by this NPC.

### AddChildNPC()
#### void AddChildNPC ( EntityNPC child )
Adds a child NPC to this NPC's child list.

### RemoveChildNPC()
#### void RemoveChildNPC ( EntityNPC child )
Removes a child NPC from this NPC's child list.

### GetAnimationData()
#### AnimationData GetAnimationData ( )
Returns the animation data for this NPC (REPENTOGON specific).

### GetAnimationFrame()
#### int GetAnimationFrame ( )
Returns the current animation frame index.

### GetAnimationName()
#### string GetAnimationName ( )
Returns the name of the currently playing animation.

### PlayAnimation()
#### void PlayAnimation ( string name, bool force )
Plays the specified animation. If `force` is true, restarts even if already playing.

### IsAnimationPlaying()
#### bool IsAnimationPlaying ( string name )
Returns true if the specified animation is currently playing.

### Kill()
#### void Kill ( )
Instantly kills the NPC, triggering death effects.

### Remove()
#### void Remove ( )
Removes the NPC immediately without death effects.

### TakeDamage()
#### bool TakeDamage ( float damage, int flags, EntityRef source, int countdown )
Deals damage to the NPC. Returns true if damage was applied.

### BloodExplode()
#### void BloodExplode ( )
Creates a blood explosion effect at the NPC's position.

### AddBurn()
#### void AddBurn ( EntityRef source, int duration, float damage )
Adds a burn effect to the NPC (damage over time).

### AddFreeze()
#### void AddFreeze ( EntityRef source, int duration )
Freezes the NPC for the specified duration.

### AddPoison()
#### void AddPoison ( EntityRef source, int duration, float damage )
Adds a poison effect to the NPC.

### AddSlowing()
#### void AddSlowing ( EntityRef source, int duration, float slowFactor )
Slows the NPC for the specified duration.

### AddCharmed()
#### void AddCharmed ( EntityRef source, int duration )
Charms the NPC (makes it fight for the player) for the specified duration.

### AddConfusion()
#### void AddConfusion ( EntityRef source, int duration, boolean ignoreBosses )
Confuses the NPC for the specified duration.

### AddFear()
#### void AddFear ( EntityRef source, int duration )
Causes the NPC to flee in fear for the specified duration.

### AddMidasFreeze()
#### void AddMidasFreeze ( EntityRef source, int duration )
Freezes the NPC in gold (Midas freeze) for the specified duration.

## REPENTOGON Properties

### Scale
#### float Scale { get; set; }
The NPC's render scale.

### Size
#### float Size { get; set; }
The NPC's collision size.

### ShadowSize
#### float ShadowSize { get; set; }
The NPC's shadow size.

### CollisionDamage
#### float CollisionDamage { get; set; }
The damage dealt on contact.

### MaxHitPoints
#### float MaxHitPoints { get; set; }
The NPC's maximum HP.

### HitPoints
#### float HitPoints { get; set; }
The NPC's current HP.

### Velocity
#### Vector Velocity { get; set; }
The NPC's velocity.

### Position
#### Vector Position { get; set; }
The NPC's position.

### Flying
#### bool Flying { get; set; }
Whether the NPC is flying.

### Color
#### Color Color { get; set; }
The NPC's color modifier.

### ProjectileCooldown
#### int ProjectileCooldown { get; set; }
The projectile firing cooldown.
