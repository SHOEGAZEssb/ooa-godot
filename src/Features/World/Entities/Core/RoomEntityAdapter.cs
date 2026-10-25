using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

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
/// Native adapters compose their source state-zero presentation during
/// construction; ordinary actors resolve imported predicates in the manager.
/// Event-owned slots remain hidden until their owner initializes them. Every
/// adapter reports that presentation through one shared transition contract.
/// </summary>
internal abstract class NpcCharacterRoomEntityAdapter
    : RoomEntityAdapter<NpcCharacter>,
        IScreenTransitionPreloadRoomEntity
{
    protected NpcCharacterRoomEntityAdapter(NpcCharacter npc, Action<Vector2> setTransitionDrawOffset)
        : base(npc, setTransitionDrawOffset)
    {
        InitializePresentation(npc);
    }

    internal static void InitializePresentation(NpcCharacter npc)
    {
        // Native adapters compose their resolved state-zero pose in their
        // constructor. Deferred event scripts retain the default hidden slot.
        if (npc.Record.Implementation == NpcImplementationClassification.SpecializedNative)
            npc.ShowInitializedPresentation();
    }

    public virtual ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) => PreparePresentation(Entity);

    internal static ScreenTransitionPresentation PreparePresentation(NpcCharacter npc)
    {
        if (npc.Record.Implementation != NpcImplementationClassification.EventOwned)
            npc.ShowInitializedPresentation();
        return npc.Visible
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;
    }
}
