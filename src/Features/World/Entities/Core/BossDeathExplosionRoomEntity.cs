using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BossDeathExplosionRoomEntity(
    BossDeathExplosionEffect explosion,
    ItemDropDatabase itemDrops,
    OracleRandom random,
    InventoryState? inventory,
    OracleSaveData? saveData,
    Func<int> roomEnemyCount)
    : RoomEntityAdapter<BossDeathExplosionEffect>(
        explosion, explosion.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
        IRoomEnemyOutcomeSource
{
    private bool _outcomeTaken;

    public bool Finished => Entity.Finished;
    public bool CountsAsEnemy => !Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        if (!Finished || _outcomeTaken)
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
        // bossDeathExplosion decrements wNumEnemies first. Only the explosion
        // which brought that count to zero resolves the defeated boss ID
        // through decideItemDrop.
        if (roomEnemyCount() != 0)
            return;

        int? subId = itemDrops.DecideDrop(
            Entity.BossId, random, inventory, saveData);
        if (subId.HasValue)
            spawns.Add(new ItemDropSpawn(subId.Value, Entity.Position));
    }
}
