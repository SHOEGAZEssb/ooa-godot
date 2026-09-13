using System.Collections.Generic;

namespace oracleofages;

internal sealed class BurningEnemyRoomEntity(BurningEnemyPart part)
    : RoomEntityAdapter<BurningEnemyPart>(part, part.SetTransitionDrawOffset), IFixedRoomEntity, IRoomEntityLifetime,
        INativePartHealthRoomEntity
{
    // partCode12 ignores its own health/status and has no enabled collision.
    public void ClearHealthAndCollision() { }
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
}

internal sealed record BurningEnemySpawn(IBurningEnemyTarget Target) : RoomEntitySpawn;
