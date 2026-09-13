using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Fixed-update owner for INTERAC_CLINK effects allocated by sword beams and
/// enemy collision effects.
/// </summary>
internal sealed class SwordBeamClinkRoomEntity(ClinkEffect clink, Action? initializeOnUpdate = null)
    : RoomEntityAdapter<ClinkEffect>(clink, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    private Action? _initializeOnUpdate = initializeOnUpdate;
    public bool Finished => Entity.Finished;
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_initializeOnUpdate is { } initialize)
        {
            _initializeOnUpdate = null;
            Entity.Visible = true;
            initialize();
            return;
        }
        Entity.AdvanceFrameForEntityManager();
    }
}
