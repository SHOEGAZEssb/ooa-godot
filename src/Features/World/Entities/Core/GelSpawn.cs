using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record GelSpawn(
    Vector2 Position,
    string Name = "Gel",
    int KillableEnemyIndex = 0)
    : RoomEntitySpawn;
