---
tags:
  - Class
  - REPENTOGON
---
# Class "Level" (REPENTOGON Extensions)

REPENTOGON extends the vanilla `Level` class with additional methods for
floor manipulation, room layout control, and advanced level generation.

## Functions

### GetRooms()
#### RoomList GetRooms ( )
Returns the list of all rooms on the current floor.

### GetRoomByIdx()
#### RoomDescriptor GetRoomByIdx ( int index )
Returns the room descriptor at the specified index.

### GetCurrentRoomIndex()
#### int GetCurrentRoomIndex ( )
Returns the grid index of the current room.

### GetCurrentRoom()
#### RoomDescriptor GetCurrentRoom ( )
Returns the descriptor for the current room.

### GetRoomByGridIndex()
#### RoomDescriptor GetRoomByGridIndex ( int gridIndex )
Returns the room descriptor at the specified grid index.

### GetStartingRoomIdx()
#### int GetStartingRoomIdx ( )
Returns the grid index of the starting room (where the player enters the floor).

### GetStage()
#### LevelStage GetStage ( )
Returns the current floor stage (1 = Basement, 2 = Caves, etc.).

### SetStage()
#### void SetStage ( LevelStage stage, StageType type )
Sets the current floor stage and type directly.

### GetStageType()
#### StageType GetStageType ( )
Returns the current stage type (0 = normal, 1 = XL, 2 = WOTL alt, etc.).

### GetAbsoluteStage()
#### int GetAbsoluteStage ( )
Returns the absolute stage number (counts all floors, including alternate paths).

### GetCurses()
#### CurseFlag GetCurses ( )
Returns the active curses for this floor.

### SetCurses()
#### void SetCurses ( CurseFlag curses )
Sets the curses for this floor.

### HasCurse()
#### bool HasCurse ( CurseFlag curse )
Returns true if the specified curse is active.

### AddCurse()
#### void AddCurse ( CurseFlag curse, boolean showOverlay )
Adds a curse to the floor. If `showOverlay` is true, shows the curse overlay.

### RemoveCurse()
#### void RemoveCurse ( CurseFlag curse )
Removes a curse from the floor.

### GetDevilRoom()
#### RoomDescriptor GetDevilRoom ( )
Returns the devil room descriptor for this floor.

### GetAngelRoom()
#### RoomDescriptor GetAngelRoom ( )
Returns the angel room descriptor for this floor.

### SetDevilRoom()
#### void SetDevilRoom ( RoomDescriptor room )
Sets the devil room for this floor.

### SetAngelRoom()
#### void SetAngelRoom ( RoomDescriptor room )
Sets the angel room for this floor.

### GetBossRoom()
#### RoomDescriptor GetBossRoom ( )
Returns the boss room descriptor for this floor.

### GetSacrificeRoom()
#### RoomDescriptor GetSacrificeRoom ( )
Returns the sacrifice room descriptor for this floor.

### GetShop()
#### RoomDescriptor GetShop ( )
Returns the shop room descriptor for this floor.

### GetTreasureRoom()
#### RoomDescriptor GetTreasureRoom ( )
Returns the treasure room descriptor for this floor.

### GetMiniBoss()
#### RoomDescriptor GetMiniBoss ( )
Returns the mini-boss room descriptor for this floor.

### GetLibraries()
#### table GetLibraries ( )
Returns a table of all library room descriptors on this floor.

### GetSecretRooms()
#### table GetSecretRooms ( )
Returns a table of all secret room descriptors on this floor.

### GetSuperSecretRooms()
#### table GetSuperSecretRooms ( )
Returns a table of all super secret room descriptors on this floor.

### GetPlanetarium()
#### RoomDescriptor GetPlanetarium ( )
Returns the planetarium room descriptor for this floor.

### CanPlaceRoom()
#### bool CanPlaceRoom ( int gridIndex, Shape shape, int dimension )
Returns true if a room can be placed at the specified grid index with the given shape.

### PlaceRoom()
#### RoomDescriptor PlaceRoom ( RoomConfigRoom room, int gridIndex, int dimension )
Places a room at the specified grid index. Returns the new room descriptor.

### RemoveRoom()
#### void RemoveRoom ( int gridIndex )
Removes the room at the specified grid index.

### ChangeRoom()
#### void ChangeRoom ( int gridIndex )
Teleports the player to the room at the specified grid index.

### SetNextRoom()
#### void SetNextRoom ( int gridIndex )
Sets the next room to enter when the player walks through a door.

### GetDimension()
#### Dimension GetDimension ( )
Returns the current dimension (0 = normal, 1 = mirror world, 2 = death dimension).

### SetDimension()
#### void SetDimension ( Dimension dimension )
Sets the current dimension.

### GetEnterPosition()
#### Vector GetEnterPosition ( )
Returns the position where the player will enter the current room.

### SetEnterPosition()
#### void SetEnterPosition ( Vector position )
Sets the position where the player will enter the current room.

### GetLastRoomIndex()
#### int GetLastRoomIndex ( )
Returns the grid index of the room the player was in before the current one.

### GetPreviousStage()
#### LevelStage GetPreviousStage ( )
Returns the stage the player was on before the current one.

### GetNextStage()
#### LevelStage GetNextStage ( )
Returns the stage the player will go to next.

### IsAscent()
#### bool IsAscent ( )
Returns true if the player is currently in the ascent (going backwards).

### GetAscent()
#### int GetAscent ( )
Returns the current ascent level (0 = not ascending).

### GetGeneratedRoomCount()
#### int GetGeneratedRoomCount ( )
Returns the number of rooms generated on this floor.

### GetMaxRooms()
#### int GetMaxRooms ( )
Returns the maximum number of rooms for this floor.

### SetMaxRooms()
#### void SetMaxRooms ( int maxRooms )
Sets the maximum number of rooms for this floor.

### GetRoomConfig()
#### RoomConfigHolder GetRoomConfig ( )
Returns the room configuration holder (REPENTOGON specific).

### GetLevelGenerator()
#### LevelGenerator GetLevelGenerator ( )
Returns the level generator object (REPENTOGON specific, for custom generation).

### ShowMap()
#### void ShowMap ( bool show )
Shows or hides the entire map for this floor.

### ShowRoom()
#### void ShowRoom ( int gridIndex, bool show )
Shows or hides a specific room on the map.

### IsRoomVisible()
#### bool IsRoomVisible ( int gridIndex )
Returns true if the specified room is visible on the map.

## REPENTOGON Properties

### Stage
#### LevelStage Stage { get; set; }
The current floor stage.

### StageType
#### StageType StageType { get; set; }
The current stage type.

### Curses
#### CurseFlag Curses { get; set; }
The active curses for this floor.

### MaxRooms
#### int MaxRooms { get; set; }
The maximum number of rooms.

### Dimension
#### Dimension Dimension { get; set; }
The current dimension.

### Ascent
#### int Ascent { get; set; }
The current ascent level.
