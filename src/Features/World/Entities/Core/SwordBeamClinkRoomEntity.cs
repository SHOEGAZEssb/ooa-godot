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
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue => true;
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.AdvanceFrameForEntityManager();
}
