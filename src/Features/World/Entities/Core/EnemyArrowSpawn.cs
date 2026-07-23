using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record EnemyArrowSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
