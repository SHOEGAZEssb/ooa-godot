using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// These native handlers retain state0: text, object masks and scrolling admit
// them. Outgoing deletion must precede sampling the current room's flags.
internal sealed partial class DungeonRoomFlagMirrorRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly Func<IRoomEntity,bool> _isOutgoing;
    private readonly Func<bool> _flagSet;
    private readonly Action _mirrorFlag;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal DungeonRoomFlagMirrorRoomEntity(DungeonObjectRecord record,Func<IRoomEntity,bool> isOutgoing,
        Func<bool> flagSet,Action mirrorFlag)
    {
        Position = record.Position; Visible = false; Name = $"DungeonRoomFlagMirror_{record.SubId:x2}";
        _isOutgoing = isOutgoing; _flagSet = flagSet; _mirrorFlag = mirrorFlag;
    }
    private void Advance()
    {
        if (Finished) return;
        if (_isOutgoing(this)) { Finished = true; return; }
        if (!_flagSet()) return;
        _mirrorFlag(); Finished = true;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Advance(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
