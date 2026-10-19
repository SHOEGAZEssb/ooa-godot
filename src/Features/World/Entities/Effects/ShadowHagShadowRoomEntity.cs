using System.Collections.Generic;

namespace oracleofages;

internal sealed class ShadowHagShadowRoomEntity(ShadowHagShadowEffect effect)
    : RoomEntityAdapter<ShadowHagShadowEffect>(effect, effect.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.LinkTarget ?? frame.Player.EnemyContactPosition);
}

internal sealed record ShadowHagShadowSpawn(
    ShadowHagBoss Owner,
    int AngleIndex) : RoomEntitySpawn(UpdateThisFrame: true);
