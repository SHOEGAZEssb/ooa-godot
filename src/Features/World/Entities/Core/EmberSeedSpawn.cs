using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record EmberSeedSpawn(
    Vector2 LinkPosition,
    Vector2I Direction,
    SeedRecord Record,
    int Group)
    : RoomEntitySpawn;
