using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DeathPuffRoomEntity(
    EnemyDeathPuffEffect puff,
    ItemDropDatabase itemDrops,
    OracleRandom random,
    InventoryState? inventory,
    OracleSaveData? saveData,
    bool decrementsRoomCount)
    : RoomEntityAdapter<EnemyDeathPuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
        IRoomEnemyOutcomeSource
{
    private bool _outcomeTaken;

    public bool Finished => Entity.Finished;
    public bool CountsAsEnemy => decrementsRoomCount && !Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);

    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        if (!Finished || !decrementsRoomCount || _outcomeTaken)
        {
            outcome = default;
            return false;
        }

        _outcomeTaken = true;
        outcome = RoomEnemyOutcome.RoomCountDecrement();
        return true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        int? subId = itemDrops.DecideDrop(
            Entity.EnemyId, random, inventory, saveData);
        if (subId.HasValue)
            spawns.Add(new ItemDropSpawn(subId.Value, Entity.Position));
    }
}
