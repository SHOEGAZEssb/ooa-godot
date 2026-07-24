using System.Collections.Generic;

namespace oracleofages;

internal sealed class SplashRoomEntity(SplashEffect effect)
    : RoomEntityAdapter<SplashEffect>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.Advance(1.0 / 60.0);

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
