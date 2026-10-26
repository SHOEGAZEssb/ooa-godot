using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// bird.s $4c:$01..$03 and bear.s $5d:$01 dispatch interactionAnimate.
// Birds initialize speedZ=-$c0 but these subids never call the gravity helper.
internal sealed class ForestAudienceRoomEntity : IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor;
    internal int State {get;private set;}
    internal int Id {get;}
    internal int SubId {get;}
    internal int SpeedZ=>Id==0x4c?-0xc0:0;
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal ForestAudienceRoomEntity(NpcCharacter actor,string animation)
    {
        _actor=actor;Id=actor.Record.Id;SubId=actor.Record.SubId;
        if(!(Id==0x4c&&SubId is >=1 and <=3||Id==0x5d&&SubId==1))throw new NotSupportedException($"Forest audience INTERAC ${Id:x2}:${SubId:x2}.");
        if(Id==0x4c)_actor.SetBasePalette(SubId);
        _actor.SetScriptAnimation(animation);
    }
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;_actor.ShowInitializedPresentation();_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);return;}
        _actor.AdvanceAnimationUpdates(1);
    }
}
