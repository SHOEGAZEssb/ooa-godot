using Godot;
using System.Collections.Generic;

namespace Oracles;

// din.s: INTERAC $aa:$00, the linked ending's independent jump/text signal.
internal sealed class DinRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,Vector2 position,int subid=0) :
    IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor=data.Actor(0xaa,subid,position);
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int ZFixed {get;private set;}
    internal int SpeedZ {get;private set;}
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;_actor.SetScriptAnimation(_actor.Record.UpAnimation);_actor.ShowInitializedPresentation();_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);return;}
        if(subid==1){_actor.AdvanceAnimationUpdates(1);return;}
        switch(Substate)
        {
            case 0:
                _actor.AdvanceAnimationUpdates(1);
                if(context.Entities.RuntimeState.ReadWramByte(0xcfc0)!=4)break;
                Substate=1;Counter=120;SpeedZ=-0x100;_actor.SetScriptAnimation(data.Animation(0xaa,5));break;
            case 1:
                Counter=(Counter-1)&255;
                if(Counter==0){Substate=2;ZFixed&=255;Counter=30;_actor.AdvanceAnimationUpdates(1);break;}
                int z=ZFixed,speed=SpeedZ;bool landed=OracleObjectMath.UpdateSpeedZ(ref z,ref speed,0x20);
                ZFixed=z;SpeedZ=landed?-0x100:speed;break;
            case 2:
                Counter=(Counter-1)&255;
                if(Counter==0){Substate=3;Counter=60;context.ShowDialogue(data.Text(0x3d09),sourceTextId:0x3d09);}
                _actor.AdvanceAnimationUpdates(1);break;
            case 3:
                if(!context.DialogueOpen)
                {
                    Counter=(Counter-1)&255;
                    if(Counter==0){Substate=4;context.Entities.RuntimeState.SetWramByte(0xcfc0,5);}
                }
                _actor.AdvanceAnimationUpdates(1);break;
            case 4:_actor.AdvanceAnimationUpdates(1);break;
            default:throw new System.NotSupportedException($"din.s:$aa:$00 substate${Substate:x2}.");
        }
        _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:(ZFixed>>8)!=0);
        _actor.SetScriptDrawOffset(Vector2.Down*(ZFixed>>8));
    }
}
