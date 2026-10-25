using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class TransitionPerformanceProbe(Node2D node, Action? prepare = null) :
    IRoomEntity, IScreenTransitionPreloadRoomEntity
{
    public Node2D Node => node;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        prepare?.Invoke();
        return ScreenTransitionPresentation.Visible;
    }
}
