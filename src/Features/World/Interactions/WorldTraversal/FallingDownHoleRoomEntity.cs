using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FallingDownHoleRoomEntity(FallingDownHoleEffect effect)
    : RoomEntityAdapter<FallingDownHoleEffect>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    public bool Finished => Entity.Finished;
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue => true;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

}
