using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IRoomEntityLifetime
{
    bool Finished { get; }
    void OnFinished(ICollection<RoomEntitySpawn> spawns);
}
