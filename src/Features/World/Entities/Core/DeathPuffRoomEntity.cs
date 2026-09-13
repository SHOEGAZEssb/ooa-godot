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
    bool decrementsRoomCount,
    bool dropsItem = true)
    : RoomEntityAdapter<EnemyDeathPuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
        IRoomEnemyOutcomeSource, IRoomPartReplacementSource,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        INativePartHealthRoomEntity
{
    private bool _outcomeTaken;
    private bool _replacementTaken;
    public bool UpdatesDuringDialogue => Entity.ElapsedFrames == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.ElapsedFrames == 0;
    // partCode02 ignores its own health/status and has no collision enabled.
    public void ClearHealthAndCollision() { }

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

    public bool TryTakePartReplacement(out RoomEntitySpawn replacement)
    {
        replacement = null!;
        if (!Finished || _replacementTaken) return false;
        _replacementTaken = true;
        if (!dropsItem) return false;
        int? subId = itemDrops.DecideDrop(
            Entity.EnemyId, random, inventory, saveData);
        if (!subId.HasValue) return false;
        // objectReplaceWithID preserves only the high coordinate bytes.
        replacement = new ItemDropSpawn(subId.Value, Entity.Position.Floor());
        return true;
    }
}
