using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// $3c:$0c / $3a:$0b / $43:$05: original restoration dispatches and
// direct scripts, synchronized only through native cfd1/cfd3/cfdf.
internal sealed class RestoredFamilyRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NayruRescueWarpDatabase _shared;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    private int _z,_speedZ,_var38,_var39,_var3b,_palette;
    internal int Id {get;}
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal int Palette=>_palette;
    internal int Var38=>_var38;
    internal int Var39=>_var39;
    internal Vector2 PrecisePosition=>_precise;
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool Finished=>false;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal RestoredFamilyRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,NayruRescueWarpDatabase shared,
        int id,int subid,Vector2 position)
    {Context=context;_data=data;_shared=shared;Id=id;SubId=subid;_precise=position;_actor=data.Actor(id,subid,position);_runner=new(this);_palette=_actor.Record.Palette;}
    private byte Read(int address)=>Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value)=>Context.Entities.RuntimeState.SetWramByte(address,unchecked((byte)value));
    private bool Decrement(){Counter=(Counter-1)&255;return Counter==0;}
    private void SetPalette(int palette)
    {_palette=palette;_actor.SetBasePalette(palette);_actor.SetScriptPaletteOverride(palette==6?_shared.StonePalette:null);}
    private void Animation(int index)=>_actor.SetScriptAnimation(_data.Animation(Id,index));
    private void Start(string name){_runner.Start(_data.Commands(name));_runner.SetInitialMotionRegisters(new("Actor"),0,0);}
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_actor.ShowInitializedPresentation();
            switch(Id)
            {
                case 0x3c:
                    if(SubId==0){Animation(0);_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);Start("listening-boy");return;}
                    Animation(3);_var38=2;Start("restored-boy");return;
                case 0x3a:SetPalette(6);Animation(13);Counter=60;UpdateFather();return;
                case 0x43:SetPalette(6);return;
                default:throw UnsupportedCommand($"restoration INTERAC ${Id:x2}:${SubId:x2}");
            }
        }
        if(Id==0x3c&&SubId==0){_actor.AdvanceAnimationUpdates(1);_actor.ClearFixedDrawPriority();_actor.UpdateDrawPriority(Context.Player.Position,withTerrainEffects:true);_runner.AdvanceFrame();return;}
        switch(Id){case 0x3c:UpdateBoy();break;case 0x3a:UpdateFather();break;case 0x43:UpdateObserver();break;}
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
    private void UpdateBoy()
    {
        switch(Substate)
        {
            case 0:
                _actor.AdvanceAnimationUpdates(2);if(Read(0xcfd1)!=1)return;
                Substate=1;Counter=120;Animation(3);
                if(Context.Entities.InteractionSlotAvailable)Context.Entities.CreatePalaceExclamation(_actor.Position+new Vector2(8,-12),60);
                return;
            case 1:if(Decrement()){Substate=2;_speedZ=-0x1c0;}return;
            case 2:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))return;
                Write(0xcfd1,2);Substate=3;Counter=120;return;
            case 3:if(Decrement()){Counter=60;Substate=4;}return;
            case 4:_actor.AdvanceAnimationUpdates(2);if(Decrement())Substate=5;return;
            case 5:RunMovingScript();return;
            default:throw UnsupportedCommand($"boy restoration substate ${Substate:x2}");
        }
    }
    private void BeginRestoration(){Counter=120;Context.Sound.PlaySound(SoundId.SndEnergyThing);Substate++;}
    private void UpdateFather()
    {
        switch(Substate)
        {
            case 0:
                if(Context.Transitions.NativePaletteFadeActive||!Decrement())return;
                Write(0xcfd1,1);Context.Sound.PlaySound(SoundId.SndRestore);BeginRestoration();return;
            case 1:
                if(Decrement()){Substate=2;SetPalette(1);Start("restored-father");return;}
                if((FrameCounter&7)==0)SetPalette(((_palette-1)^5)+1);return;
            case 2:RunMovingScript();return;
            default:throw UnsupportedCommand($"father restoration substate ${Substate:x2}");
        }
    }
    private void UpdateObserver()
    {
        switch(Substate)
        {
            case 0:if(Read(0xcfd1)==1)BeginRestoration();return;
            case 1:
                if(Decrement()){Substate=2;SetPalette(2);Counter=420;return;}
                if((FrameCounter&7)==0)SetPalette(_palette^4);return;
            case 2:
                // interactionAnimate returns HL in the ROM OAM-pointer table
                // on a frame reload. pastGuy.s only replaces L before the
                // following decrement, so that update cannot write counter1/2.
                bool reload=_actor.CurrentAnimationCounter==1;_actor.AdvanceAnimationUpdates(1);
                if(!reload){Counter=Math.Max(0,Counter-1);if(Counter==0)Write(0xcfdf,255);}return;
            default:throw UnsupportedCommand($"observer restoration substate ${Substate:x2}");
        }
    }
    private void RunMovingScript()
    {
        if(_var39==0)
        {
            int speed=_runner.ActorSpeed(new("Actor"));
            _actor.AdvanceAnimationUpdates(_runner.MovementCounter(new("Actor"))==0||speed<0x28?1:speed<0x50?2:speed<0x78?3:4);
        }
        _runner.AdvanceFrame();
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void InitializeActorCollisionRadii(string actor)=>_actor.SetCollisionRadii(6,6);
    public override void SetActorButtonSensitive(string actor)=>_actor.SetScriptButtonSensitive(true);
    // Credits call func_3539 without updateInteractionsPost; no collision
    // pass can write this interaction's pressed-A byte.
    public override bool TryConsumeActorButton(CutsceneActorId actor)=>false;
    public override int ReadMemory(string binding)=>binding=="RestorationSignal"?Read(0xcfd1):throw UnsupportedCommand(binding);
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void SetActorAnimation(string actor,int index,string animation)=>_actor.SetScriptAnimation(animation);
    public override void SetActorMovementAnimation(string actor,int angle,string animation)=>_actor.SetScriptAnimation(animation);
    public override void MoveActorAtSpeed(string actor,int speed,int angle)=>_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _precise,speed,angle);
    public override void WriteObjectByte(string actor,int address,int value)
    {if(address==0x79)_var39=value;else if(address==0x7b)_var3b=value;else throw UnsupportedCommand($"restoration object byte ${address:x2}");}
    public override void RunNativeHandler(string handler)
    {
        if(handler.StartsWith("Throw:",StringComparison.Ordinal))
        {_actor.ForceNextAnimationFrame();_var38=(_var38-1)&255;Write(0xcfd3,int.Parse(handler.AsSpan(6)));return;}
        switch(handler)
        {
            case "Accessory":
                if(Context.Entities.InteractionSlotAvailable)Context.Entities.AddEntity(new AccessoryRoomEntity(
                    _shared.CreateActor(0x63,0x3f,Vector2.Zero),_actor,()=>_var3b!=0,()=>_z,index=>_shared.Animation(0x63,index),_shared.Table("bowtie-offsets")));
                return;
            case "Ball":
                if(!Context.Entities.InteractionSlotAvailable)return;
                var ball=new Room149Ball {Name="RestoredFamilyBall",ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex};
                ball.Initialize(new Room149FamilyDatabase().Visual("ball"));ball.SetNativeInitialPosition(new(0x3c,0x4a));
                Context.Entities.AddEntity(new NativeBallRoomEntity(ball,Context.Entities.RuntimeState));return;
            default:throw UnsupportedCommand(handler);
        }
    }
}
