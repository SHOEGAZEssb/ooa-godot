using System.Collections.Generic;

namespace Oracles;

internal sealed class BurningEnemyRoomEntity(BurningEnemyPart part)
    : RoomEntityAdapter<BurningEnemyPart>(part, part.SetTransitionDrawOffset), IFixedRoomEntity, IRoomEntityLifetime,
        INativePartHealthRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public bool UpdatesDuringDialogue => Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => Entity.InitializationPending;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (Entity.InitializationPending) Entity.UpdateFrame();
    }
    // partCode12 ignores its own health/status and has no enabled collision.
    public void ClearHealthAndCollision() { }
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
}

internal sealed record BurningEnemySpawn(IBurningEnemyTarget Target) : RoomEntitySpawn;
