using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface IFixedRoomEntity
{
    void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns);
}
