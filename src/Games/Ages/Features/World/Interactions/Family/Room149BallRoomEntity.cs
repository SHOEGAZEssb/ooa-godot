using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class Room149BallRoomEntity(
    Room149Ball ball,
    Room149FamilyInteraction family)
    : RoomEntityAdapter<Room149Ball>(ball, ball.SetTransitionDrawOffset),
        IFixedRoomEntity, IScreenTransitionPreloadRoomEntity
{
    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        // boy.s:@initSubid0e creates $95 only outside the stone branch;
        // ball.s:@state0 then sets visible80 without advancing catch or flight.
        // The family constructor already resolves both presentations.
        return Entity.Visible
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => family.UpdateBall();
}
