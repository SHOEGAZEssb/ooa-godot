using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// boy.s:boyRunSubid08/09, also called by boy2.s:$3f:$03.
internal sealed class BoyPatternRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,int id,int subid,Vector2 position) :
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    internal NpcCharacter Actor {get;}=data.Actor(id,subid,position);
    private Vector2 _position=position;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int PatternState {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int ZFixed {get;private set;}
    internal int SpeedZ {get;private set;}
    internal Vector2 PrecisePosition=>_position;
    public Node2D Node=>Actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset)=>Actor.SetTransitionDrawOffset(offset);
    private bool Decrement(){Counter=(Counter-1)&255;return Counter==0;}
    private int Signal=>context.Entities.RuntimeState.ReadWramByte(0xcfd0);
    private void Animation(int index)=>Actor.SetScriptAnimation(data.Animation(id,index));
    private void Face()=>Animation(((Angle+4)&0x18)>>3);
    private void Jump()=>SpeedZ=-0xe0;
    private void Hop()
    {int z=ZFixed,speed=SpeedZ;bool land=OracleObjectMath.UpdateSpeedZ(ref z,ref speed,0x20);ZFixed=z;SpeedZ=speed;if(land)Jump();}
    private void Move(){Actor.AdvanceAnimationUpdates(2);Actor.Position=NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState,ref _position,Speed,Angle);}
    private void Pattern()
    {
        var bytes=data.Table("boy-patterns");int start=(id==0x3c?subid-8:2)*9,index=start+Counter2*2;
        Counter=bytes[index];Angle=bytes[index+1];Counter2=bytes[index+2]==0?0:Counter2+1;Face();
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;Counter=120;Actor.SetScriptAnimation(Actor.Record.UpAnimation);
            if(id==0x3f)Actor.SetBasePalette(2);
            Actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex,withTerrainEffects:true);return;
        }
        switch(Substate)
        {
            case 0:if(Decrement()){Substate=1;Counter=0x66;Speed=0x32;Angle=0x18;Face();}break;
            case 1:Move();if(Decrement()){Counter=30+(context.Entities.NextRandomValue()&15);Angle=8;Face();Substate=2;}break;
            case 2:if(Decrement()){Jump();Substate=3;}break;
            case 3:Hop();if(Signal==1&&(ZFixed>>8)==0){Substate=4;Counter=30;}break;
            case 4:if(Decrement()){Speed=0x50;Pattern();Substate=5;}break;
            case 5:
                if(Signal==2&&(ZFixed>>8)==0){Substate=6;Counter=10;Angle=0x18;Face();break;}
                switch(PatternState)
                {
                    case 0:Move();if(Decrement()){Counter=10;PatternState=1;Animation(Actor.Position.X<0x68?1:3);}break;
                    case 1:if(Decrement()){Counter=30;PatternState=2;Jump();}break;
                    case 2:Hop();if(Decrement()){ZFixed=0;PatternState=0;Pattern();}break;
                    default:throw new NotSupportedException($"boyRunSubid08: var37${PatternState:x2}.");
                }
                break;
            case 6:if(Decrement()){Counter=id==0x3f?0x34:0x20;Speed=0x3c;Substate=7;}break;
            case 7:Move();if(Decrement()){Counter=(context.Entities.NextRandomValue()&7)+1;Animation(1);Substate=8;}break;
            case 8:if(Signal==3&&Decrement()){Substate=9;Jump();}break;
            case 9:Hop();if(Signal==4&&(ZFixed>>8)==0){Substate=10;Counter=12;}break;
            case 10:if(Decrement()){Substate=11;Counter=80;Speed=0x3c;Animation(3);}break;
            case 11:Move();if(Decrement())Finished=true;break;
            default:throw new NotSupportedException($"boyRunSubid08:${id:x2}:${subid:x2} substate${Substate:x2}.");
        }
        Actor.SetScriptDrawOffset(Vector2.Down*(ZFixed>>8));
    }
}
