using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record PumpkinHeadProjectileSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn;
