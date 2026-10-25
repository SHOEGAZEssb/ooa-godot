using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// rabbitMain.s native $4b:$03/$04/$05 ending dispatches.
internal sealed class RestoredRabbitRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NayruRescueWarpDatabase _shared;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    private int _z,_speedZ,_palette;
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int CollisionCounter {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal int Palette=>_palette;
    internal Vector2 PrecisePosition=>_precise;
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal RestoredRabbitRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,NayruRescueWarpDatabase shared,int subid,Vector2 position)
    {Context=context;_data=data;_shared=shared;SubId=subid;_precise=position;_actor=data.Actor(0x4b,subid,position);_runner=new(this);_palette=_actor.Record.Palette;}
    private byte Read(int address)=>Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value)=>Context.Entities.RuntimeState.SetWramByte(address,unchecked((byte)value));
    private bool Decrement(){Counter=(Counter-1)&255;return Counter==0;}
    private void Animation(int index)=>_actor.SetScriptAnimation(_data.Animation(0x4b,index));
    private void SetPalette(int value){_palette=value;_actor.SetBasePalette(value);_actor.SetScriptPaletteOverride(value==6?_shared.StonePalette:null);}
    private void IdleJump(){Animation(7);_speedZ=-0xe0;}
    private void RunningJump(){Angle=0x18;Speed=0x3c;Animation(5);_speedZ=-0x180;}
    private void Move()=>_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _precise,Speed,Angle);
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_actor.ShowInitializedPresentation();
            if(SubId==0){_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);_runner.Start(_data.Commands("listening-rabbit"));return;}
            if(SubId is 4 or 5)
            {
                Write(AgesWramAddress.wLoadedTreeGfxIndex,0x4b);Write(AgesWramAddress.wInteractionIDToLoadExtraGfx,0x4b);
            }
            if(SubId==4){IdleJump();return;}
            Counter=SubId==5?270:120;SetPalette(6);Animation(6);return;
        }
        if(SubId==0){_actor.AdvanceAnimationUpdates(1);_actor.ClearFixedDrawPriority();_actor.UpdateDrawPriority(Context.Player.Position,withTerrainEffects:true);_runner.AdvanceFrame();return;}
        if(SubId==4)UpdateUnstoned();
        else
        {
            if(SubId==5&&CollisionCounter!=0&&--CollisionCounter==0)
            {
                Substate=4;Angle=8;Speed=0x19;_speedZ=-0x1e0;
                if(Context.Entities.InteractionSlotAvailable)
                {
                    Context.Entities.Spawn(new SwordWallClinkSpawn(_precise.Floor(),Flickers:false,ZHigh:_z>>8));
                    Context.Sound.PlaySound(SoundId.SndDamageEnemy);Write(0xcfd1,2);
                }
                Animation(8);
            }
            UpdateRestored();
        }
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
    private void UpdateRestored()
    {
        switch(Substate)
        {
            case 0:
                if(SubId==5){Counter=Math.Max(0,Counter-1);if(Counter!=0)return;}
                else if(!Decrement())return;
                Counter=90;Substate=1;if(SubId==3)Write(0xcfd1,1);Context.Sound.PlaySound(SoundId.SndRestore);return;
            case 1:
                if(!Decrement()){if((FrameCounter&7)==0)SetPalette(_palette^4);return;}
                Substate=2;SetPalette(2);CollisionCounter=32;RunningJump();return;
            case 2:
                _actor.AdvanceAnimationUpdates(1);
                if(_actor.CurrentAnimationParameter!=0){Context.Sound.PlaySound(SoundId.SndJump);Substate=3;}return;
            case 3:
                if((int)_precise.X>=0xd0){Finished=true;_actor.SetActive(false);return;}
                Move();if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)){Substate=2;RunningJump();}return;
            case 4:if(SubId==5){Bounce();return;}break;
            case 5:if(SubId==5){if(Decrement())Write(0xcfdf,255);return;}break;
        }
        throw UnsupportedCommand($"restored rabbit $4b:${SubId:x2} substate ${Substate:x2}");
    }
    private void UpdateUnstoned()
    {
        switch(Substate)
        {
            case 0:
                if(Read(0xcfd1)==1){Substate=2;_runner.Start(_data.Commands("restored-rabbit"));return;}
                _actor.AdvanceAnimationUpdates(1);if(_actor.CurrentAnimationParameter!=0)Substate=1;return;
            case 1:if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20)){Substate=0;IdleJump();}return;
            case 2:
                if(Read(0xcfd1)!=2){_runner.AdvanceFrame();return;}
                Substate=3;Angle=0x18;Speed=0x19;_speedZ=-0x180;Animation(9);return;
            case 3:Bounce();return;
            case 4:return;
            default:throw UnsupportedCommand($"unstoned rabbit $4b:$04 substate ${Substate:x2}");
        }
    }
    private void Bounce()
    {
        Move();if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))return;
        if(OracleObjectMath.NegateAndHalveSpeedZ(ref _speedZ)){Substate++;Counter=60;}
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void InitializeActorCollisionRadii(string actor)=>_actor.SetCollisionRadii(6,6);
    public override void SetActorButtonSensitive(string actor)=>_actor.SetScriptButtonSensitive(true);
    public override bool TryConsumeActorButton(CutsceneActorId actor)=>false;
    public override void SetActorAnimation(string actor,int index,string animation)=>_actor.SetScriptAnimation(animation);
    public override void ScriptEnded() {}
}
