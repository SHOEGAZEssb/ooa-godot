using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record EnemyDeathPuffSpawn(
    Vector2 Position,
    bool HighKnockback = false,
    int EnemyId = -1) : RoomEntitySpawn;
