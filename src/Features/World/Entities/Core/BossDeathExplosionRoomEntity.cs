using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BossDeathExplosionRoomEntity(BossDeathExplosionEffect explosion)
    : RoomEntityAdapter<BossDeathExplosionEffect>(
        explosion, explosion.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    public bool Finished => Entity.Finished;
    public bool CountsAsEnemy => !Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
