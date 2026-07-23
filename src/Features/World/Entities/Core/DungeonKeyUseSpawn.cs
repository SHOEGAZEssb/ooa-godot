using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record DungeonKeyUseSpawn(
    Vector2 Position,
    TreasureObjectVisualRecord Visual) : RoomEntitySpawn;
