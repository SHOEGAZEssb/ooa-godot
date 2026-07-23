using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal readonly record struct DungeonKeyDoorDatabaseRecord(byte ClosedTile, Vector2I Direction, bool UsesBossKey, int KeyGraphic, byte OpenTile, byte RoomFlag, byte OppositeRoomFlag, int PushCounter, int DoorFrameWait, int DoorSound, int KeySound, int NoKeyTextId, string NoKeyMessage);
