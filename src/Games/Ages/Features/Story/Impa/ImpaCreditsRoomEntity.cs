using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_IMPA_IN_CUTSCENE $31:$02 retains its independent native dispatch.
internal sealed class ImpaCreditsRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly System.Func<bool> _flash;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private IReadOnlyList<BlueEnergyBeadRoomEntity> _beads=[];
    private Vector2 _position;
    private int _counter,_counter2,_wait,_z,_speedZ,_direction;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter=>_counter;
    internal int Counter2=>_counter2;
    internal int JumpWait=>_wait;
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal NpcCharacter Actor=>_actor;
    internal int SubId {get;}
    internal Vector2 PrecisePosition=>_position;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal ImpaCreditsRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,Vector2 position,System.Func<bool> flash,int subid=2)
    {Context=context;_data=data;_flash=flash;_position=position;SubId=subid;_actor=data.Actor(0x31,subid,position);_runner=new(this);}
    private void Write(int address,int value)=>Context.Entities.RuntimeState.SetWramByte(address,(byte)value);
    private int Read(int address)=>Context.Entities.RuntimeState.ReadWramByte(address);
    private void Visible(bool terrain)=>_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,terrain);
    private void Animation(int animation)=>_actor.SetScriptAnimation(_data.Animation(0x31,animation));
    private void Jump(){Visible(true);_speedZ=-0x180;}
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;Visible(false);
            if(SubId==3){Animation(3);_runner.Start(_data.Commands("reunion-impa"));return;}
            _counter=30;return;
        }
        if(SubId==3){AnimateAndRun();return;}
        switch(Substate)
        {
            case 0:
                if(Context.Transitions.NativePaletteFadeActive||(_counter=(_counter-1)&255)!=0)return;
                _counter=60;Substate=1;_beads=Context.Entities.CreateEnergySwirl(new(0x50,0x60),0x50);return;
            case 1:
                if((_counter=(_counter-1)&255)!=0)return;
                Write(0xcbb3,0);Write(0xcbba,255);Substate=2;return;
            case 2:
                if(!_flash())return;
                Substate=3;_runner.Start(_data.Commands("credits-impa"));Write(0xcfc0,1);
                Context.Transitions.BeginNativeWhiteFade(false);return;
            case 3:AnimateAndRun();break;
            case 4:
                if((_wait=(_wait-1)&255)!=0)return;
                Substate=5;_counter=2;Jump();return;
            case 5:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))break;
                if((_counter=(_counter-1)&255)!=0){Jump();break;}
                Visible(false);_wait=16;Substate=6;break;
            case 6:
                if((_wait=(_wait-1)&255)!=0)return;
                _wait=16;int before=_counter2;_counter2=(_counter2+1)&255;
                if(before==2){_counter2=0;Write(0xcfc0,2);Substate=7;return;}
                Substate=4;Animation(before==0?3:1);return;
            case 7:
                AnimateAndRun();
                if(Read(0xcfc0)>=3&&(FrameCounter&15)==0)
                {
                    int direction=((OracleObjectMovement.Shared.RelativeAngle(_position.Floor(),new(Read(0xcfd6),Read(0xcfd5)))+4)&0x18)>>3;
                    if(direction!=_direction){_direction=direction;Animation(direction);}
                }
                break;
            default:throw UnsupportedCommand($"impaSubid2 substate ${Substate:x2}");
        }
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
    private void AnimateAndRun()
    {
        int speed=_runner.ActorSpeed(new("Actor"));
        _actor.AdvanceAnimationUpdates(_runner.MovementCounter(new("Actor"))==0||speed<0x28?1:speed<0x50?2:speed<0x78?3:4);
        _runner.AdvanceFrame();
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void ShowText(int id,string message)=>ShowText(id,message,null);
    public override void ShowText(int id,string message,int? position)=>Context.ShowDialogue(message,position,sourceTextId:id);
    public override void SetActorAnimation(string actor,int index,string animation)=>Animation(index);
    public override void SetActorMovementAnimation(string actor,int angle,string animation)=>Animation((angle&0x18)>>3);
    public override void MoveActorAtSpeed(string actor,int speed,int angle)
    {_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _position,speed,angle);}
    public override bool GateOpen(string gate)=>gate=="PaletteDone"?!Context.Transitions.NativePaletteFadeActive:throw UnsupportedCommand(gate);
    public override int ReadMemory(string binding)=>binding switch {"CreditsSignal"=>Read(0xcfc0),"AnimationParameter"=>_actor.CurrentAnimationParameter,_=>throw UnsupportedCommand(binding)};
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void WriteMemory(string binding,int value)
    {if(binding!="CreditsSignal")throw UnsupportedCommand(binding);Write(0xcfc0,value);}
    public override void RunNativeHandler(string handler)
    {switch(handler){case "JumpWait":_wait=(_wait+30)&255;break;case "JumpState":Substate++;break;default:throw UnsupportedCommand(handler);}}
    public override void ScriptEnded() {}
    internal void Cancel()
    {
        foreach(var bead in _beads)Context.Entities.RetireEntity(bead);
        _beads=[];
    }
}
