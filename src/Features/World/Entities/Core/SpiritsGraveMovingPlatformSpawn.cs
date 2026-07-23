using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record SpiritsGraveMovingPlatformSpawn(Vector2 Position, int SubId)
    : RoomEntitySpawn(UpdateThisFrame: true);
