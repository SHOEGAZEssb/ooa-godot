using System.Collections.Generic;

namespace oracleofages;

internal sealed class SplashRoomEntity(SplashEffect effect)
    : RoomEntityAdapter<SplashEffect>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    public bool Finished => Entity.Finished;
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue => true;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.Advance(1.0 / 60.0);

}
