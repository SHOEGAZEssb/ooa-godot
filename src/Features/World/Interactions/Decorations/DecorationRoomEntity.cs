using Godot;
using System.Collections.Generic;

namespace oracleofages;

// decoration.s:$80:$00: state zero shows visible83 and returns; only
// subsequent eligible interaction updates animate the imported sprite.
internal sealed partial class DecorationRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal DecorationRoomEntity(DungeonObjectRecord record, DungeonInteractionVisual visual)
    {
        Name = $"Decoration_{record.SubId:x2}";
        InitializeVisual(visual, record.Position, positionedOam: true);
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        Visible = false;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { _initialized = true; Visible = true; }
        else AdvanceAnimation();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        _initialized = true; Visible = true;
        return ScreenTransitionPresentation.Visible;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
