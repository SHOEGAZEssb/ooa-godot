using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class RoomEntityAdapter<T> : IRoomEntity where T : Node2D
{
    private readonly Action<Vector2> _setTransitionDrawOffset;

    protected RoomEntityAdapter(T node, Action<Vector2> setTransitionDrawOffset)
    {
        Entity = node;
        _setTransitionDrawOffset = setTransitionDrawOffset;
    }

    protected T Entity { get; }
    public Node2D Node => Entity;
    public void SetTransitionDrawOffset(Vector2 offset) =>
        _setTransitionDrawOffset(offset);
}

/// <summary>
/// NPC source state 0 and imported visibility predicates are resolved before
/// adapters are created. Every NPC adapter reports that resolved state through
/// one shared transition contract so specialized NPC types cannot omit it.
/// </summary>
internal abstract class NpcCharacterRoomEntityAdapter(
    NpcCharacter npc,
    Action<Vector2> setTransitionDrawOffset)
    : RoomEntityAdapter<NpcCharacter>(npc, setTransitionDrawOffset),
        IScreenTransitionPreloadRoomEntity
{
    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.Visible
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;
}
