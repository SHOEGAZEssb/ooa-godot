using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_GREENPOOF $02:$00 uses breakTileDebris.s, with its always-update
// bit, silent ID-table cue and fixed visible80 priority.
internal sealed partial class GreenPoofRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IAlwaysUpdateDuringScreenTransitionRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly int _z;
    private bool _initialized;
    internal int Parameter => AnimationParameter;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal GreenPoofRoomEntity(DungeonInteractionVisual visual, Vector2 position, int z)
    {
        InitializeVisual(visual, position.Floor()); _z = z; Visible = false;
        ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    private void Advance()
    {
        if (Finished) return;
        if (!_initialized) { _initialized = true; Visible = true; return; }
        if ((AnimationParameter & 0x80) != 0) { Finished = true; Visible = false; return; }
        AdvanceAnimation(); QueueRedraw();
    }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(CurrentTexture, CurrentDrawOffset + Vector2.Down * _z + TransitionDrawOffset);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
