using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record ShovelDebrisSpawn(Vector2 Position, Vector2I Direction)
    : RoomEntitySpawn(UpdateThisFrame: true);
