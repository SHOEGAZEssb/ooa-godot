using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface ISeedBurnTarget
{
    bool IsSeedBurning { get; }
    bool FreezesDuringSeedBurn { get; }
    Vector2 SeedBurnPosition { get; }
    void CompleteSeedBurn(ICollection<RoomEntitySpawn> spawns);
}
