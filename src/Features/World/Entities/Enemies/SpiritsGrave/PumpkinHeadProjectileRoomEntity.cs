using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PumpkinHeadProjectileRoomEntity(PumpkinHeadProjectile projectile)
    : RoomEntityAdapter<PumpkinHeadProjectile>(
        projectile, projectile.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
