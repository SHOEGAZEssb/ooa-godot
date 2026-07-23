using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DeathPuffRoomEntity(
    EnemyDeathPuffEffect puff,
    ItemDropDatabase itemDrops,
    OracleRandom random)
    : RoomEntityAdapter<EnemyDeathPuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        int? subId = itemDrops.DecideDrop(Entity.EnemyId, random);
        if (subId.HasValue)
            spawns.Add(new ItemDropSpawn(subId.Value, Entity.Position));
    }
}
