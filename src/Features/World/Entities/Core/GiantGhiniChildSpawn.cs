using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record GiantGhiniChildSpawn(GiantGhiniBoss Owner, int Index)
    : RoomEntitySpawn(UpdateThisFrame: true);
