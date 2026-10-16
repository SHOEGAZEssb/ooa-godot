using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Native $4d:$01/$02, $40:$08 and the normal-game deletion of $31:$09.
// Companion $36/$37 dispatch stays in its existing actor owner.
internal sealed class BlackTowerEndingActor : RoomCutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private int _z,_speedZ,_jump,_direction;
    private Vector2 _creditsPrecise;
    private bool _scriptEnded;
    internal int Id {get;}
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int ZFixed=>_z;
    internal int Direction=>_direction;
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal BlackTowerEndingActor(RoomEventContext context,BlackTowerEndingDatabase data,int id,int subid,Vector2 position)
    {Context=context;_data=data;Id=id;SubId=subid;_actor=data.Actor(id,subid,position);_runner=new(this);}
    private byte Read(int address)=>Context.Entities.RuntimeState.ReadWramByte(address);
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;
            switch(Id)
            {
                case 0x31:
                    if(IsLinkedGame)throw UnsupportedCommand("INTERAC $31:$09 linked continuation");
                    Finished=true;_actor.SetActive(false);return;
                case 0x40:_actor.SetBasePalette(3);_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);break;
                case 0x4d:
                    _runner.Start(_data.Commands(SubId==2?"credits-ambi":Read(0xcfd0)==0x0b?"ambi-farewell":"ambi"));
                    if(SubId==2){_creditsPrecise=_actor.Position;_runner.SetInitialMotionRegisters(new("Actor"),0,0);_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);}break;
                default:throw UnsupportedCommand($"INTERAC ${Id:x2}:${SubId:x2}");
            }
            _actor.ShowInitializedPresentation();return;
        }
        if(Id==0x4d&&SubId==2)
        {
            _actor.AdvanceAnimationUpdates(1);
            if(Substate==0){_runner.AdvanceFrame();if(_scriptEnded)Substate=1;}
            else if(Substate==1)
            {
                MoveActorAtSpeed("Actor",_runner.ActorSpeed(new("Actor")),_runner.ActorAngle(new("Actor")));
                if(Read(0xcfc0)==6){Substate=2;_creditsPrecise=new(0x40+_creditsPrecise.X%1,0x50+_creditsPrecise.Y%1);_actor.Position=_creditsPrecise.Floor();}
            }
            return;
        }
        if(Id==0x40)
        {
            if((FrameCounter&15)==0)
            {
                int direction=((OracleObjectMovement.Shared.RelativeAngle(_actor.Position,new(Read(0xcfd6),Read(0xcfd5)))+4)&0x18)>>3;
                if(direction!=_direction){_direction=direction;_actor.SetScriptAnimation(_data.Animation(Id,direction));}
            }
            _actor.AdvanceAnimationUpdates(1);return;
        }
        switch(Substate)
        {
            case 0:
                if(Read(0xcfd0)==0x0e){_speedZ=-0x1c0;Context.Sound.PlaySound(SoundId.SndJump);Substate=1;break;}
                _actor.AdvanceAnimationUpdates(1);_runner.AdvanceFrame();break;
            case 1:
                if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)){Substate=2;_jump=(_jump+1)&255;}
                break;
            case 2:_actor.AdvanceAnimationUpdates(1);_runner.AdvanceFrame();break;
            default:throw UnsupportedCommand($"Ambi ending substate ${Substate:x2}");
        }
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override int ReadMemory(string binding)=>binding switch {"EndingSignal"=>Read(0xcfd0),"EndingJump"=>_jump,"CreditsSignal"=>Read(0xcfc0),_=>throw UnsupportedCommand(binding)};
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void WriteMemory(string binding,int value)
    {
        Context.Entities.RuntimeState.SetWramByte(binding switch {"EndingSignal"=>0xcfd0,"CreditsSignal"=>0xcfc0,_=>throw UnsupportedCommand(binding)},(byte)value);
    }
    public override void ShowText(int id,string message,int? position)=>Context.ShowDialogue(message,position,sourceTextId:id);
    public override void PlaySound(int sound)=>Context.Sound.PlaySound(sound);
    // Source scriptend leaves these actors visible and animating.
    public override void ScriptEnded()=>_scriptEnded=true;
    public override void MoveActorAtSpeed(string actor,int speed,int angle)=>_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _creditsPrecise,speed,angle);
    public override bool GateOpen(string gate)=>gate=="PaletteDone"?!Context.Transitions.NativePaletteFadeActive:throw UnsupportedCommand(gate);
    public override void RunNativeHandler(string handler)
    {
        if(handler=="FadeOut")Context.Transitions.BeginNativeWhiteFade(true);
        else if(handler=="FadeIn")Context.Transitions.BeginNativeWhiteFade(false);
        else throw UnsupportedCommand(handler);
    }
}
