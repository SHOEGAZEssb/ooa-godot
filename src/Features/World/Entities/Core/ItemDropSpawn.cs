using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record ItemDropSpawn(
    int SubId,
    Vector2 Position,
    int Angle = 0,
    bool DugUp = false,
    bool UpdateThisFrame = false) : RoomEntitySpawn(UpdateThisFrame);
