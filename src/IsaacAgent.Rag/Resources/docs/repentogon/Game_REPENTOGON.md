---
tags:
  - Class
  - REPENTOGON
---
# Class "Game" (REPENTOGON Extensions)

REPENTOGON extends the vanilla `Game` class with additional methods for
game state control, rendering, and REPENTOGON-specific features.

## Functions

### GetImGui()
#### ImGui GetImGui ( )
Returns the ImGui instance for custom menu rendering (REPENTOGON specific).

### GetConsole()
#### Console GetConsole ( )
Returns the console object for programmatic console commands.

### GetDebugRenderer()
#### DebugRenderer GetDebugRenderer ( )
Returns the debug renderer for drawing debug overlays.

### GetPersistentGameData()
#### PersistentGameData GetPersistentGameData ( )
Returns the persistent game data (achievements, unlocks, etc.).

### GetPlayerManager()
#### PlayerManager GetPlayerManager ( )
Returns the player manager for multiplayer-related operations.

### GetMusicManager()
#### MusicManager GetMusicManager ( )
Returns the music manager for advanced music control.

### GetFont()
#### Font GetFont ( string fontName )
Returns a font object by name for custom text rendering.

### GetRenderSet()
#### RenderSet GetRenderSet ( )
Returns the current render set for the game.

### GetShader()
#### Shader GetShader ( string shaderName )
Returns a shader by name for custom rendering effects.

### SetMusicState()
#### void SetMusicState ( int state )
Sets the music playback state (0 = stop, 1 = play, 2 = pause).

### GetScore()
#### int GetScore ( )
Returns the current game score.

### SetScore()
#### void SetScore ( int score )
Sets the current game score.

### GetFrameCount()
#### int GetFrameCount ( )
Returns the total number of frames since the game started.

### GetTime()
#### int GetTime ( )
Returns the current game time in milliseconds.

### GetChallenge()
#### int GetChallenge ( )
Returns the current challenge ID (0 = no challenge).

### SetChallenge()
#### void SetChallenge ( int challengeID )
Sets the current challenge ID.

### GetDifficulty()
#### int GetDifficulty ( )
Returns the current difficulty (0 = normal, 1 = hard, 2 = greed, 3 = greedier).

### SetDifficulty()
#### void SetDifficulty ( int difficulty )
Sets the current difficulty.

### GetSeeds()
#### Seeds GetSeeds ( )
Returns the seeds object for seed-related operations.

### GetItemPool()
#### ItemPool GetItemPool ( )
Returns the item pool for controlling item generation.

### GetLevel()
#### Level GetLevel ( )
Returns the level object for floor-related operations.

### GetRoom()
#### Room GetRoom ( )
Returns the current room object.

### GetHUD()
#### HUD GetHUD ( )
Returns the HUD object for UI-related operations.

### GetNumPlayers()
#### int GetNumPlayers ( )
Returns the number of active players (1-4 for co-op).

### GetPlayer()
#### EntityPlayer GetPlayer ( int index )
Returns the player at the specified index (0-based).

### Spawn()
#### Entity Spawn ( EntityType type, int variant, int subtype, Vector position, Vector velocity, Entity spawner )
Spawns an entity in the current room.

### BombDamage()
#### void BombDamage ( Vector position, float damage, Entity source, boolean damageTears, boolean damagePlayers, int tearFlags, DamageFlags damageFlags )
Deals bomb damage at a position (REPENTOGON enhanced version).

### BombPosition()
#### void BombPosition ( Vector position, boolean affectEnemies, boolean affectPlayers, int tearFlags )
Creates a bomb explosion at a position without spawning a bomb entity.

### GetScreenShake()
#### Vector GetScreenShake ( )
Returns the current screen shake offset (REPENTOGON specific).

### SetScreenShake()
#### void SetScreenShake ( float intensity, int duration )
Sets the screen shake intensity and duration.

### GetDarkness()
#### float GetDarkness ( )
Returns the current darkness level (0.0 = full light, 1.0 = full dark).

### SetDarkness()
#### void SetDarkness ( float darkness )
Sets the darkness level for the current room.

### GetPauseState()
#### PauseMenuStates GetPauseState ( )
Returns the current pause menu state.

### SetPauseState()
#### void SetPauseState ( PauseMenuStates state )
Sets the pause menu state.

### IsPaused()
#### bool IsPaused ( )
Returns true if the game is currently paused.

### GetDevilRoomDeals()
#### int GetDevilRoomDeals ( )
Returns the number of devil room deals taken.

### GetAngelRoomChance()
#### float GetAngelRoomChance ( )
Returns the chance of getting an angel room (0.0 to 1.0).

### SetAngelRoomChance()
#### void SetAngelRoomChance ( float chance )
Sets the chance of getting an angel room.

### GetDevilRoomChance()
#### float GetDevilRoomChance ( )
Returns the chance of getting a devil room.

### SetDevilRoomChance()
#### void SetDevilRoomChance ( float chance )
Sets the chance of getting a devil room.

### ShowFloatingText()
#### void ShowFloatingText ( Vector position, string text, Color color )
Shows floating text at a position (REPENTOGON specific).

### ShowDeathScreen()
#### void ShowDeathScreen ( string title, string subtitle )
Shows a custom death screen (REPENTOGON specific).

### GetEnding()
#### Ending GetEnding ( )
Returns the current ending ID.

### SetEnding()
#### void SetEnding ( Ending ending )
Sets the ending to show when the run ends.

## REPENTOGON Properties

### Darkness
#### float Darkness { get; set; }
The current darkness level.

### ScreenShakeIntensity
#### float ScreenShakeIntensity { get; set; }
The current screen shake intensity.

### PauseState
#### PauseMenuStates PauseState { get; set; }
The current pause state.

### Difficulty
#### int Difficulty { get; set; }
The current difficulty level.

### Challenge
#### int Challenge { get; set; }
The current challenge ID.

### Score
#### int Score { get; set; }
The current game score.

### DevilRoomChance
#### float DevilRoomChance { get; set; }
The devil room appearance chance.

### AngelRoomChance
#### float AngelRoomChance { get; set; }
The angel room appearance chance.
