using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// miscellaneous1.s:INTERAC $6b:$10, the unfinished credits statue.
internal sealed class MakuStatueConstructionRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private readonly Action<int> _scrollY;
    private readonly Action _palette;
    private Vector2 _precise;
    private int _z,_speedZ;
    private bool _ended;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal Vector2 PrecisePosition=>_precise;
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal MakuStatueConstructionRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,Vector2 position,Action palette,Action<int> scrollY)
    {
        Context=context;_data=data;_precise=position;_palette=palette;_scrollY=scrollY;_actor=data.Actor(0x6b,0x10,position);
        _actor.SetSourceGrayscaleInverted(new MakuSproutRoomDatabase().Record.StatueSourceInverted);
        _actor.SetScriptAnimation(_actor.Record.DownAnimation);
        _runner=new(this);_runner.Start(data.Commands("credits-statue"));
        _runner.SetInitialMotionRegisters(new("Actor"),0,0);
    }
    private int Signal=>Context.Entities.RuntimeState.ReadWramByte(0xcfc0);
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_palette();
            // PALH_c8 writes OBJ palette $06; retain its original shade order.
            _actor.SetScriptPaletteOverride(Context.Entities.NativeObjectPalettes![6]);
            _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);return;
        }
        switch(Substate)
        {
            case 0:_runner.AdvanceFrame();if(Signal==2){Substate=1;Counter=32;}break;
            case 1:
                if(--Counter==0){Substate=2;Context.Entities.RuntimeState.SetWramByte(0xcfc0,3);}
                else if((Counter&7)==0)_z=unchecked((short)(_z-256));break;
            case 2:_runner.AdvanceFrame();if(_ended)Substate=3;break;
            case 3:
                _actor.AdvanceAnimationUpdates(1);MoveActorAtSpeed("Actor",_runner.ActorSpeed(new("Actor")),_runner.ActorAngle(new("Actor")));
                if(Signal==6){Substate=4;_precise=new(0x84+_precise.X%1,0x40+_precise.Y%1);_actor.Position=_precise.Floor();}break;
            case 4:if(Signal==7)Substate=5;break;
            case 5:
                if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,1))
                {Substate=6;Counter=30;_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);_actor.SetScriptAnimation(_data.Animation(0x6b,5));}break;
            case 6:
                Counter=(Counter-1)&255;
                if(Counter==0){Substate=7;_scrollY(0);}else _scrollY((Counter&1)!=0?1:255);break;
            case 7:break;
            default:throw UnsupportedCommand($"miscellaneous1.s $6b:$10 substate ${Substate:x2}");
        }
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override bool MemoryEquals(string binding,int value)=>binding=="CreditsSignal"?Signal==value:throw UnsupportedCommand(binding);
    public override void WriteMemory(string binding,int value)
    {if(binding!="CreditsSignal")throw UnsupportedCommand(binding);Context.Entities.RuntimeState.SetWramByte(0xcfc0,(byte)value);}
    public override void SetActorAnimation(string actor,int index,string animation)=>_actor.SetScriptAnimation(animation);
    public override void MoveActorAtSpeed(string actor,int speed,int angle)=>_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _precise,speed,angle);
    public override void ScriptEnded()=>_ended=true;
}
