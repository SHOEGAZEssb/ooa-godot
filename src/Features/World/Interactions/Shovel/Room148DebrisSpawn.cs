using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed record Room148DebrisSpawn(
    Vector2 Position,
    int Palette,
    int Angle,
    int DrawPriority)
    : RoomEntitySpawn(UpdateThisFrame: true);
