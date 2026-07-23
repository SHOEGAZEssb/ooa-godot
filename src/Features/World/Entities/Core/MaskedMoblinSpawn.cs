using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record MaskedMoblinSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);
