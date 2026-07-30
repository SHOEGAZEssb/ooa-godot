using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Fixed-update owner for INTERAC_CLINK effects allocated by sword beams and
/// enemy collision effects.
/// </summary>
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
