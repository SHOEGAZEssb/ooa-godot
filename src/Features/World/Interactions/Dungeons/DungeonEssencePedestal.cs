using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_ESSENCE $7f:$01, the persistent pedestal.</summary>
internal sealed partial class DungeonEssencePedestal : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomBlocker, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly Func<long> _animationTick;
    internal bool Initialized { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized;

    internal DungeonEssencePedestal(Vector2 position, DungeonInteractionVisual visual,
        OracleRoomData room, Func<long> animationTick)
    {
        _room = room; _animationTick = animationTick;
        InitializeVisual(visual, position);
        Name = "EssencePedestal_7f01";
        ZIndex = NpcCharacter.FixedLowPriorityZIndex; // objectSetVisible83.
        Visible = false;
    }

    internal void InitializeState()
    {
        if (Initialized) return;
        Initialized = true;
        _room.SetPositionTileAndCollision(Position, _room.GetMetatile(Position), 0x0f,
            _animationTick(), preserveRenderedTile: true);
        Visible = true;
    }
    public bool BlocksLink(Vector2 linkCenter)
    {
        Vector2 delta = linkCenter - Position;
        return Initialized && Mathf.Abs(delta.X) < 10 && Mathf.Abs(delta.Y) < 6;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!frame.Player.IsDying) InitializeState();
        // State1 only objectPreventLinkFromPassing; no animation advance.
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { InitializeState(); return ScreenTransitionPresentation.Visible; }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
