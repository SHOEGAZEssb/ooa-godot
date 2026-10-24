using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// common/horonDogCredits.s:$b9. The low wait byte and retained counter2
// become a word hold for subid0; subid7 reuses counter2 as script movement.
internal sealed class HoronDogCreditsRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _position;
    private int _counter,_counter2,_target,_landed,_angle,_speed;
    internal int Subid {get;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter=>Subid==7&&Substate==2?_runner.NativeCounter1:_counter;
    internal int Counter2=>Subid==7?_runner.MovementCounter(new("Actor")):_counter2;
    internal int Angle {get=>Subid==7&&State!=0?_runner.ActorAngle(new("Actor")):_angle;private set=>_angle=value;}
    internal int Speed {get=>Subid==7&&State!=0?_runner.ActorSpeed(new("Actor")):_speed;private set=>_speed=value;}
    internal int ZFixed {get;private set;}
    internal int SpeedZ {get;private set;}
    internal int Landed=>_landed;
    internal NpcCharacter Actor=>_actor;
    internal Vector2 PrecisePosition=>_position;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal HoronDogCreditsRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,int subid,Vector2 position)
    {Context=context;_data=data;Subid=subid;_position=position;_actor=data.Actor(0xb9,subid,position);_runner=new(this);}
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    private int Word(string table,int index){var bytes=_data.Table(table);return bytes[index*2]|bytes[index*2+1]<<8;}
    private void Jump()=>SpeedZ=unchecked((short)Word("dog-jumps",Subid));
    private bool Decrement(){_counter=(_counter-1)&255;return _counter==0;}
    private void Gravity(int acceleration,bool repeat)
    {
        int z=ZFixed,speed=SpeedZ;bool landed=OracleObjectMath.UpdateSpeedZ(ref z,ref speed,acceleration);
        ZFixed=z;SpeedZ=speed;
        if(landed){if(repeat)Jump();else _landed++;}
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_actor.SetScriptAnimation(_actor.Record.UpAnimation);_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);_actor.Visible=false;
            _counter=_data.Table("dog-waits")[Subid];var targets=_data.Table("dog-targets");_target=targets[Subid*2+1];
            Angle=OracleObjectMovement.Shared.RelativeAngle(_position,new(_target,targets[Subid*2]));Speed=0x28;
            if(Subid==0){Angle=4;_counter=0xe0;_counter2=1;Jump();}
            else if(Subid<=6)
            {
                Jump();if(Subid>=3)
                {
                    Speed=Subid==3?0x3c:0x0a;int word=Word("dog-z",Subid-3);
                    ZFixed=unchecked((short)(((word&255)<<8)|(word>>8))); // Native stores low table byte into zh.
                }
            }
            else if(Subid==7)
            {
                // interactionSetScript clears both counter bytes, replacing
                // the preceding table's250 with a wrapping256-update wait.
                _counter=_counter2=0;_runner.Start(_data.Commands("credits-dog"));_runner.SetInitialMotionRegisters(new("Actor"),_speed,_angle);
            }
            else throw UnsupportedCommand($"horonDogCredits.s subid${Subid:x2}");
        }
        else switch(Substate)
        {
            case 0:if(Decrement()){Substate=1;_actor.Visible=true;}break;
            case 1:
                _actor.AdvanceAnimationUpdates(1);_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _position,Speed,Angle);
                if((int)_actor.Position.X==_target)
                {Substate=2;ZFixed&=255;_actor.SetScriptAnimation(_data.Animation(0xb9,Subid*2+1));}
                else if(Subid<3)Gravity(0x20,true);
                else if(Subid<7&&_landed==0)Gravity(Subid==3?0x10:1,false);
                break;
            case 2:
                if(Subid==0)
                {
                    int word=Math.Max(0,(_counter|_counter2<<8)-1);_counter=word&255;_counter2=word>>8;
                    if(word==0){Context.Entities.RuntimeState.SetWramByte(0xcfdf,1);break;}
                }
                if(Subid==7){_runner.AdvanceFrame();if(Counter2==0)break;}
                _actor.AdvanceAnimationUpdates(1);break;
            default:throw UnsupportedCommand($"horonDogCredits.s substate${Substate:x2}");
        }
        _actor.SetScriptDrawOffset(Vector2.Down*(ZFixed>>8));
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void MoveActorAtSpeed(string actor,int speed,int angle)
    {Speed=speed;Angle=angle;_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _position,speed,angle);}
}
