using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// DC:$11 retains state0 and never deletes. Even outgoing/disabled dispatches
// sample the current room's item flag, then OR the shared boss-key bit.
internal sealed partial class DungeonBossKeyMirrorRoomEntity(Func<bool> itemFlagSet,Action grantKey) : Node2D,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    private void Advance() { if (itemFlagSet()) grantKey(); }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Visible=false; Advance(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
