using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record SwordBeamSpawn(Vector2 LinkPosition, int Direction)
    : RoomEntitySpawn;
