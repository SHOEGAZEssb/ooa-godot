using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record RockDebrisSpawn(
    Vector2 Position,
    int InteractionId = 0x06)
    : RoomEntitySpawn(UpdateThisFrame: true);
