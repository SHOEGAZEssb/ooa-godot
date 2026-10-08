using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ItemDropProducerRoomEntity(
    ItemDropProducer producer,
    int killableEnemyIndex)
    : RoomEntityAdapter<ItemDropProducer>(producer, static _ => { }),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyOutcomeSource,
        IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze, INativeEnemyCounter1RoomEntity
{
    private bool _outcomeTaken;

    public bool Finished => Entity.Finished;
    public bool UpdatesDuringDialogue => !Entity.Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Entity.Initialized;
    public int Counter1 { get => Entity.Counter1; set => Entity.Counter1 = value; }
    public bool RetainsCounter1AfterDeletion => false;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(spawns);

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();

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
