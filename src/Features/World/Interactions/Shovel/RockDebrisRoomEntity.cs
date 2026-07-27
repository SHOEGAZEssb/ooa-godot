using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RockDebrisRoomEntity(RockDebrisEffect debris)
    : RoomEntityAdapter<RockDebrisEffect>(
        debris, debris.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    public bool Finished => Entity.Finished;
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue => true;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

}
