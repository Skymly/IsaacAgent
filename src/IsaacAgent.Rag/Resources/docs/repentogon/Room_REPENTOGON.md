---
tags:
  - Class
  - REPENTOGON
---
# Class "Room" (REPENTOGON Extensions)

REPENTOGON extends the vanilla `Room` class with additional methods for
room manipulation, custom rendering, and advanced gameplay control.

## Functions

### GetRoomConfig()
#### RoomConfigRoom GetRoomConfig ( )
Returns the room configuration data for the current room, including
variant, type, subtype, and dimensions.

### GetRoomDescriptor()
#### RoomDescriptor GetRoomDescriptor ( )
Returns the room descriptor with detailed information about the room's
state, visited status, and connections.

### GetDoors()
#### table GetDoors ( )
Returns a table of all door entities in the room.

### GetDoor()
#### GridEntityDoor GetDoor ( int slot )
Returns the door at the specified slot (0-7). Returns nil if no door exists.

### SetDoor()
#### void SetDoor ( int slot, GridEntityDoor door )
Sets the door at the specified slot.

### GetTiles()
#### table GetTiles ( )
Returns a table of all grid tiles in the room (REPENTOGON specific).

### GetTile()
#### GridEntity GetTile ( int gridIndex )
Returns the grid entity at the specified tile index.

### SetTile()
#### void SetTile ( int gridIndex, GridEntity tile )
Sets the grid entity at the specified tile index.

### GetBackdrop()
#### Backdrop GetBackdrop ( )
Returns the backdrop (background) object for the room.

### SetBackdrop()
#### void SetBackdrop ( Backdrop backdrop )
Sets the backdrop for the room.

### GetCamera()
#### Camera GetCamera ( )
Returns the camera object for the room (REPENTOGON specific).

### SetCamera()
#### void SetCamera ( Camera camera )
Sets the camera for the room.

### IsClear()
#### bool IsClear ( )
Returns true if the room has been cleared (all enemies dead).

### SetClear()
#### void SetClear ( bool clear )
Sets the room's clear state. Use this to force-clear a room.

### GetType()
#### RoomType GetType ( )
Returns the room type (e.g., `RoomType.ROOM_DEFAULT`, `RoomType.ROOM_SHOP`).

### SetType()
#### void SetType ( RoomType type )
Sets the room type. Can be used to change a room's type dynamically.

### GetSubType()
#### int GetSubType ( )
Returns the room's subtype.

### SetSubType()
#### void SetSubType ( int subType )
Sets the room's subtype.

### GetShape()
#### Shape GetShape ( )
Returns the room's shape (e.g., `Shape.ROOMSHAPE_1x1`, `Shape.ROOMSHAPE_2x2`).

### GetWidth()
#### int GetWidth ( )
Returns the room's width in tiles.

### GetHeight()
#### int GetHeight ( )
Returns the room's height in tiles.

### GetCenterPos()
#### Vector GetCenterPos ( )
Returns the center position of the room in world coordinates.

### GetTopLeftPos()
#### Vector GetTopLeftPos ( )
Returns the top-left position of the room.

### GetBottomRightPos()
#### Vector GetBottomRightPos ( )
Returns the bottom-right position of the room.

### GetGridIndex()
#### int GetGridIndex ( Vector position )
Converts a world position to a grid index.

### GetGridPosition()
#### Vector GetGridPosition ( int gridIndex )
Converts a grid index to a world position.

### IsPositionInRoom()
#### bool IsPositionInRoom ( Vector position )
Returns true if the given position is inside the room boundaries.

### GetWaterAmount()
#### float GetWaterAmount ( )
Returns the amount of water/flood in the room (REPENTOGON specific).

### SetWaterAmount()
#### void SetWaterAmount ( float amount )
Sets the water/flood level in the room.

### GetRainIntensity()
#### float GetRainIntensity ( )
Returns the rain intensity in the room.

### SetRainIntensity()
#### void SetRainIntensity ( float intensity )
Sets the rain intensity in the room (0.0 = no rain, 1.0 = heavy rain).

### GetFloorEffect()
#### FloorEffect GetFloorEffect ( )
Returns the current floor effect (e.g., cracks, burning).

### SetFloorEffect()
#### void SetFloorEffect ( FloorEffect effect )
Sets the floor effect for the room.

### GetEntities()
#### EntityList GetEntities ( )
Returns the list of all entities in the room.

### GetNPCs()
#### EntityList GetNPCs ( )
Returns only the NPC entities in the room.

### GetPickups()
#### EntityList GetPickups ( )
Returns only the pickup entities in the room.

### GetPlayers()
#### EntityList GetPlayers ( )
Returns only the player entities in the room.

### GetEffects()
#### EntityList GetEffects ( )
Returns only the effect entities in the room.

### SpawnEntity()
#### Entity SpawnEntity ( EntityType type, int variant, int subtype, Vector position, Vector velocity, Entity spawner )
Spawns an entity in the room with full control over all parameters.

### RemoveEntity()
#### void RemoveEntity ( Entity entity )
Removes an entity from the room immediately.

### GetNumEntities()
#### int GetNumEntities ( )
Returns the total number of entities in the room.

### GetNumNPCs()
#### int GetNumNPCs ( )
Returns the number of NPC entities in the room.

### GetNumPickups()
#### int GetNumPickups ( )
Returns the number of pickup entities in the room.

### GetData()
#### table GetData ( )
Returns the room's persistent data table. Data stored here persists
for the room's lifetime (until the level is left).

### GetFlags()
#### RoomFlags GetFlags ( )
Returns the room's flags (e.g., `RoomFlags.ROOM_FOG`, `RoomFlags.ROOM_WATER`).

### SetFlags()
#### void SetFlags ( RoomFlags flags )
Sets the room's flags.

### HasFlag()
#### bool HasFlag ( RoomFlags flag )
Returns true if the room has the specified flag.

### GetMusic()
#### int GetMusic ( )
Returns the music track ID for the room.

### SetMusic()
#### void SetMusic ( int musicID )
Sets the music track for the room.

### GetAmbient()
#### int GetAmbient ( )
Returns the ambient sound track ID for the room.

### SetAmbient()
#### void SetAmbient ( int ambientID )
Sets the ambient sound track for the room.

## REPENTOGON Properties

### WaterAmount
#### float WaterAmount { get; set; }
The amount of water in the room. Higher values = more flooding.

### RainIntensity
#### float RainIntensity { get; set; }
The rain intensity (0.0 to 1.0).

### FloorEffect
#### FloorEffect FloorEffect { get; set; }
The current floor visual effect.

### RoomFlags
#### RoomFlags RoomFlags { get; set; }
The room's visual and gameplay flags.

### IsClear
#### bool IsClear { get; set; }
Whether the room has been cleared.

### DoorLockStates
#### table DoorLockStates { get; set; }
A table of door lock states (indexed by door slot 0-7).
