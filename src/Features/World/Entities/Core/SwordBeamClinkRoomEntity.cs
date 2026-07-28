using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SwordBeamClinkRoomEntity(ClinkEffect clink)
    : RoomEntityAdapter<ClinkEffect>(clink, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.AdvanceFrameForEntityManager();
}
