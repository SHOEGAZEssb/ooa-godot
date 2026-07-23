using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface ISeedBurnTarget
{
    bool IsSeedBurning { get; }
    Vector2 SeedBurnPosition { get; }
    void CompleteSeedBurn(ICollection<RoomEntitySpawn> spawns);
}
