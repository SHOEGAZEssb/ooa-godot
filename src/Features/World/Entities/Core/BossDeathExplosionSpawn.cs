using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record BossDeathExplosionSpawn(Vector2 Position, int BossId)
    : RoomEntitySpawn(UpdateThisFrame: true);
