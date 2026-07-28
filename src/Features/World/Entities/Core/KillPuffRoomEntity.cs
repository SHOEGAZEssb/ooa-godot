using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KillPuffRoomEntity(KillEnemyPuffEffect puff)
    : RoomEntityAdapter<KillEnemyPuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
}
