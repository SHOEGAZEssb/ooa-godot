using Godot;
using System.Collections.Generic;
using System;

namespace Oracles;

// decoration.s:$80:$00: state zero shows visible83 and returns; only
// subsequent eligible interaction updates animate the imported sprite.
internal sealed partial class DecorationRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IRoomEntityLifetime
{
    private bool _initialized;
    private readonly Func<bool>? _deleteOnInit;
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal DecorationRoomEntity(DungeonObjectRecord record, DungeonInteractionVisual visual, Func<bool>? deleteOnInit = null)
    {
        _deleteOnInit = deleteOnInit;
        Name = $"Decoration_{record.SubId:x2}";
        InitializeVisual(visual, record.Position, positionedOam: true);
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        Visible = false;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!_initialized) Initialize();
        else AdvanceAnimation();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Initialize();
        return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
    private void Initialize()
    {
        _initialized = true; Finished = _deleteOnInit?.Invoke() == true; Visible = !Finished;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
