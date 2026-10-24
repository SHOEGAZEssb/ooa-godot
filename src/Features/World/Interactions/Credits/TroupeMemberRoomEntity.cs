using Godot;
using System.Collections.Generic;

namespace oracleofages;

// miscellaneous1.s: $6b:$03/$12 initialize/show on their first visit,
// then animate. Neither shares the Link statue's collision/palette handler.
internal sealed class TroupeMemberRoomEntity(BlackTowerEndingDatabase data,int subid,Vector2 position) :
    IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    internal NpcCharacter Actor {get;}=data.Actor(0x6b,subid,position);
    private bool _initialized;
    public Node2D Node=>Actor;
    public bool UpdatesDuringDialogue=>!_initialized;
    public bool UpdatesDuringRoomEntityFreeze=>!_initialized;
    public void SetTransitionDrawOffset(Vector2 offset)=>Actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized)
        {_initialized=true;Actor.SetScriptAnimation(Actor.Record.UpAnimation);Actor.ShowInitializedPresentation();Actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);return;}
        Actor.AdvanceAnimationUpdates(1);
    }
}
