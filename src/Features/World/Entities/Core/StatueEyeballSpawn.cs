using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record StatueEyeballSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);
