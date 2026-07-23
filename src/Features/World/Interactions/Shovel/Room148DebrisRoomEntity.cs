using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class Room148DebrisRoomEntity(Room148PickaxeDebris debris)
    : RoomEntityAdapter<Room148PickaxeDebris>(
        debris, debris.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
