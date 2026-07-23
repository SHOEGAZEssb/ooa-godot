using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record OverworldKeyUseSpawn(
    Vector2 Position,
    OverworldKeyholeDatabaseRecord Visual,
    ConstantsRecord Constants) : RoomEntitySpawn;
