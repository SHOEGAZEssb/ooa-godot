using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed record GroundTreasureSpawn(GroundTreasureDatabaseRecord Record)
    : RoomEntitySpawn;
