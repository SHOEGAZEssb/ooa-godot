using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record SwordBeamClinkSpawn(Vector2 Position)
    : RoomEntitySpawn;
