using Godot;
using System.Collections.Generic;

namespace oracleofages;

// accessory.s: var03=0, position offset $f400. Deletion consumes var3b in
// the child's own slot, so the parent cannot reuse that capacity early.
internal sealed partial class TokayHeldAccessoryRoomEntity(TokayHoldingItemCharacter parent)
    : TransitionOffsetNode2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _initialized=true;
        if (!parent.Active || !parent.HeldItemVisible) { Finished=true; Visible=false; return; }
        Position=parent.HeldItemPosition;
        Visible=parent.ScriptVisible;
        ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex;
        QueueRedraw();
    }
    public override void _Draw() => parent.DrawHeldItem(this);
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
