using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PumpkinHeadProjectileRoomEntity(PumpkinHeadProjectile projectile)
    : RoomEntityAdapter<PumpkinHeadProjectile>(
        projectile, projectile.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool Finished => Entity.Finished;
    public bool UpdatesDuringDialogue => Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => Entity.InitializationPending;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);
}
