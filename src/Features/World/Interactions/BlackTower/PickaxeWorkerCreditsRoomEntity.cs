using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// pickaxeWorker.s native $57:$01/$02. Each worker keeps its own interaction
// slot, interpreter counters, animation suppression and movement registers.
internal sealed class PickaxeWorkerCreditsRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private readonly Action _loadMakuBackground;
    private ExplosionWithDebrisDatabase? _debris;
    private Vector2 _precise;
    private bool _ended,_suppressAnimation;
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal NpcCharacter Actor=>_actor;
    internal Vector2 PrecisePosition=>_precise;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal PickaxeWorkerCreditsRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,int subid,Vector2 position,Action loadMakuBackground)
    {
        if(subid is not 1 and not 2)throw new NotSupportedException($"pickaxeWorker.s: credits dispatch $57:${subid:x2}.");
        Context=context;_data=data;SubId=subid;_precise=position;_loadMakuBackground=loadMakuBackground;_actor=data.Actor(0x57,subid,position);_runner=new(this);
    }
    private int Signal=>Context.Entities.RuntimeState.ReadWramByte(0xcfc0);
    private void Start(string phase)
    {
        int speed=_runner.ActorSpeed(new("Actor")),angle=_runner.ActorAngle(new("Actor"));
        _ended=false;_runner.Start(_data.Commands($"credits-worker{SubId}-{phase}"));
        _runner.SetInitialMotionRegisters(new("Actor"),speed,angle);
    }
    private void Animate()
    {if(!_suppressAnimation)_actor.AdvanceAnimationUpdates(1);} // All source speeds here are below SPEED_100.
    private void Run(){Animate();_runner.AdvanceFrame();}
    private void Move()=>MoveActorAtSpeed("Actor",_runner.ActorSpeed(new("Actor")),_runner.ActorAngle(new("Actor")));
    private bool Debris()
    {
        if(_actor.ConsumeAnimationParameter()==0)return true;
        int visible=_actor.ZIndex==ObjectDrawPriority.FixedLowPriorityZIndex?0xc3:0xc1;
        for(int child=4;child>0;child--)
        {
            if(!Context.Entities.InteractionSlotAvailable)return false;
            Context.Entities.AddEntity(new ExplosionWithDebrisRoomEntity(Context.Entities,_debris??=new(),
                _data.Table("explosion-positions"),_data.Table("debris-speeds"),2,child,
                _precise.Floor()+new Vector2(SubId==1?12:-12,0),creatorDrawByte:visible));
        }
        return true;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;Start("start");}
        switch(Substate)
        {
            case 0:
                if(Signal==1){Substate=1;Start("middle");return;}
                Run();if(!Debris())return;
                _actor.SetFixedDrawPriority(_precise.Y>=0x50?ObjectDrawPriority.InFrontOfLinkZIndex:ObjectDrawPriority.FixedLowPriorityZIndex,withTerrainEffects:true);return;
            case 1:
                Run();if(!_ended)return;Substate=2;
                if(SubId==1){Counter=210;return;}
                goto case 2;
            case 2:
                _actor.AdvanceAnimationUpdates(1);Move();
                if(SubId==1)
                {
                    if(--Counter==0){Substate=3;Context.Transitions.BeginNativeWhiteFade(true);}
                    return;
                }
                if(Signal==6){Substate=3;SetActorCoordinates("Actor",0x38,0x8a);Start("end");}return;
            case 3:
                if(SubId==2){Run();return;}
                if(Context.Transitions.NativePaletteFadeActive)return;
                Substate=4;Context.Entities.RuntimeState.SetWramByte(0xcfc0,6);_loadMakuBackground();
                SetActorCoordinates("Actor",0x42,0x7e);Start("end");Context.Transitions.BeginNativeWhiteFade(false);return;
            case 4:Run();return;
            default:throw UnsupportedCommand($"pickaxeWorker.s $57:${SubId:x2} substate ${Substate:x2}");
        }
    }
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override bool GateOpen(string gate)=>gate=="PaletteDone"?!Context.Transitions.NativePaletteFadeActive:throw UnsupportedCommand(gate);
    public override int ReadMemory(string binding)=>binding=="CreditsSignal"?Signal:throw UnsupportedCommand(binding);
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void WriteMemory(string binding,int value)=>Context.Entities.RuntimeState.SetWramByte(binding switch {"CreditsSignal"=>0xcfc0,"CreditsDone"=>0xcfdf,_=>throw UnsupportedCommand(binding)},(byte)value);
    public override void SetActorAnimation(string actor,int index,string animation)=>_actor.SetScriptAnimation(animation);
    public override void MoveActorAtSpeed(string actor,int speed,int angle)
    {_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _precise,speed,angle);}
    public override void SetActorCoordinates(string actor,int y,int x)
    {_precise=new(x+_precise.X%1,y+_precise.Y%1);_actor.Position=_precise.Floor();}
    public override void WriteObjectByte(string actor,int address,int value)
    {if(address!=0x7f)throw UnsupportedCommand($"writeobjectbyte ${address:x2}");_suppressAnimation=value!=0;}
    public override void RunNativeHandler(string handler)
    {
        switch(handler)
        {
            case "AnimationDelay":_actor.SetAnimationCounter((_actor.CurrentAnimationCounter+8)&255);return;
            case "RandomDelay":_runner.WaitForNativeCounter((Context.Entities.Random.Next().Value&31)+44);return;
            case "Priority:c1":_actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex,withTerrainEffects:true);return;
            case "Priority:83":_actor.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);return;
            default:throw UnsupportedCommand(handler);
        }
    }
    public override void ScriptEnded()=>_ended=true;
}
