using Godot;
using System.Collections.Generic;

namespace Oracles;

// SPECIALOBJECT_MAPLE_CUTSCENE $12 retains w1Companion's pre-Link dispatch.
internal sealed class MapleCreditsRoomEntity : IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    internal MapleEncounter Maple {get;}=new();
    public Node2D Node=>Maple;
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    internal MapleCreditsRoomEntity(RoomEventContext context,Vector2 position)=>Maple.InitializeCredits(context,position);
    public void SetTransitionDrawOffset(Vector2 offset)=>Maple.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Maple.UpdateCredits(frame.Counter);
}
