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
        IRoomEnemyOutcomeSource, INativePartHealthRoomEntity, IRoomPartReplacementSource
{
    // partCode04 ignores its own health/status and has no enabled collision.
    public void ClearHealthAndCollision() { }
    private bool _outcomeTaken;
    private bool _replacementTaken;

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

    public bool TryTakePartReplacement(out RoomEntitySpawn replacement)
    {
        replacement = null!;
        if (!Finished || _replacementTaken) return false;
        _replacementTaken = true;
        // bossDeathExplosion decrements wNumEnemies first. Only the explosion
        // which brought that count to zero resolves the defeated boss ID
        // through decideItemDrop.
        if (roomEnemyCount() != 0)
            return false;

        int? subId = itemDrops.DecideDrop(
            Entity.BossId, random, inventory, saveData);
        if (!subId.HasValue) return false;
        // objectReplaceWithID reuses this PART slot and copies only high XYZ.
        // Its replacement first runs on the following part pass.
        replacement = new ItemDropSpawn(subId.Value, Entity.Position.Floor(), ZHigh: Entity.ZHigh);
        return true;
    }
}
