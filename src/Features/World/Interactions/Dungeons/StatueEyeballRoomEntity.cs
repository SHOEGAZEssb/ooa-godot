using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class StatueEyeballRoomEntity(StatueEyeball eye)
    : RoomEntityAdapter<StatueEyeball>(eye, eye.SetTransitionDrawOffset),
        IFixedRoomEntity, IScreenTransitionPreloadRoomEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }
}
