using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $a8:$03 publishes SPECIALOBJECT_MAPLE_CUTSCENE $12:$00 after
// w1Companion has already dispatched. The loader frees its interaction slot.
internal sealed class CreditsCompanionLoaderRoomEntity(RoomEventContext context,Vector2 position) :
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public Node2D Node {get;}=new(){Visible=false};
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public void SetTransitionDrawOffset(Vector2 offset) {}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {context.Entities.AddEntity(new MapleCreditsRoomEntity(context,position));Finished=true;}
}
