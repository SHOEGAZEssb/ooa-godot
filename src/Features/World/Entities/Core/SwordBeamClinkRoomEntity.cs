using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SwordBeamClinkRoomEntity(ClinkEffect clink)
    : RoomEntityAdapter<ClinkEffect>(clink, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.AdvanceFrameForEntityManager();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
