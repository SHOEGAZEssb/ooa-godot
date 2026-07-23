using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record OctorokRockSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
