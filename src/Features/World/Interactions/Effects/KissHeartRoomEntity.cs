using Godot;
using System.Collections.Generic;

namespace Oracles;

// common/interactions/kissHeart.s: INTERAC $b7. Subid0 retains bank1's
// common sprites; subid1 loads that sheet through its own GFX78 row.
internal sealed class KissHeartRoomEntity(BlackTowerEndingDatabase data,int subid,Vector2 position) :
    IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor=data.Actor(0xb7,subid,position.Floor());
    internal int Subid=>subid;
    internal int State {get;private set;}
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;_actor.SetScriptAnimation(_actor.Record.UpAnimation);_actor.ShowInitializedPresentation();_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);return;}
        _actor.AdvanceAnimationUpdates(1);
    }
}
