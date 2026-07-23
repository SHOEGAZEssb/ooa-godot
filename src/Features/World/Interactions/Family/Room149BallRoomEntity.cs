using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class Room149BallRoomEntity(
    Room149Ball ball,
    Room149FamilyInteraction family)
    : RoomEntityAdapter<Room149Ball>(ball, ball.SetTransitionDrawOffset),
        IFixedRoomEntity
{
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => family.UpdateBall();
}
