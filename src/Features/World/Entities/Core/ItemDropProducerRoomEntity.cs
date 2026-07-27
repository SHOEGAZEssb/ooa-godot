using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ItemDropProducerRoomEntity(
    ItemDropProducer producer,
    int killableEnemyIndex)
    : RoomEntityAdapter<ItemDropProducer>(producer, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyOutcomeSource
{
    private bool _outcomeTaken;

    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(spawns);

    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        if (!Finished || !Entity.SpawnedDrop || _outcomeTaken)
        {
            outcome = default;
            return false;
        }

        _outcomeTaken = true;
        outcome = RoomEnemyOutcome.PlacementConsumed(killableEnemyIndex);
        return true;
    }
}
