using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// common/nayruRalphCredits.s:$df, independently ordered signal/RNG producers.
internal sealed class NayruRalphCreditsRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,int subid,Vector2 position) :
    IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    internal NpcCharacter Actor {get;}=data.Actor(0xdf,subid,position);
    private Vector2 _position=position;
    internal int Subid=>subid;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int TurnCounter {get;private set;}
    internal int Direction {get;private set;}
    internal int Speed {get;private set;}
    internal Vector2 PrecisePosition=>_position;
    public Node2D Node=>Actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset)=>Actor.SetTransitionDrawOffset(offset);
    private bool Decrement(){Counter=(Counter-1)&255;return Counter==0;}
    private void Signal(int value)=>context.Entities.RuntimeState.SetWramByte(0xcfd0,(byte)value);
    private void Animation(int value)=>Actor.SetScriptAnimation(data.Animation(0xdf,value));
    private void Move(){Actor.AdvanceAnimationUpdates(1);Actor.Position=NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState,ref _position,Speed,0x18);}
    private void RandomTurn()=>TurnCounter=0x20+((context.Entities.NextRandomValue()&3)<<4);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;Counter=60;Speed=0x14;Actor.SetScriptAnimation(Actor.Record.UpAnimation);
            Actor.SetFixedDrawPriority(subid==0?ObjectDrawPriority.BehindLinkZIndex:ObjectDrawPriority.FixedHighPriorityZIndex,withTerrainEffects:true);return;
        }
        switch(Substate)
        {
            case 0:if(!Decrement())return;Substate=1;goto case 1;
            case 1:
                Move();if((int)Actor.Position.X!=0x68)return;
                Substate=2;Counter=180;if(subid==0)Animation(5);return;
            case 2:if(!Decrement())return;Substate=3;Signal(1);Counter=260;RandomTurn();return;
            case 3:
                Counter=Math.Max(0,Counter-1);
                if(Counter==0){Substate=4;Counter=100;Speed=subid==0?0x14:0x3c;Animation(subid==0?4:2);Signal(2);return;}
                if(subid==0)
                {
                    bool reload=Actor.CurrentAnimationCounter==1;Actor.AdvanceAnimationUpdates(1);
                    // interactionAnimate reload leaves H=$76. LD L,$78 then
                    // DEC reads ROM$10:7678 instead of var38. Import guards it.
                    if(reload)return;
                }
                TurnCounter=(TurnCounter-1)&255;if(TurnCounter!=0)return;
                Direction^=1;Animation(subid*2+Direction);RandomTurn();return;
            case 4:if(Decrement()){Substate=5;Counter=subid==0?160:120;Signal(3);}return;
            case 5:if(Decrement()){Substate=6;Counter=60;Signal(4);}return;
            case 6:Move();if(Decrement())context.Entities.RuntimeState.SetWramByte(0xcfdf,1);return;
            default:throw new NotSupportedException($"nayruRalphCredits.s:$df:${subid:x2} substate${Substate:x2}.");
        }
    }
}
