using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ItemDropProducerRoomEntity(
    ItemDropProducer producer,
    int killableEnemyIndex)
    : RoomEntityAdapter<ItemDropProducer>(producer, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomKillTrackedEnemy
{
    public bool Finished => Entity.Finished;
    public int KillableEnemyIndex => killableEnemyIndex;
    public bool MarksEnemyKilled => Entity.SpawnedDrop;
    public bool CountsAsDefeat => false;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(spawns);

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
