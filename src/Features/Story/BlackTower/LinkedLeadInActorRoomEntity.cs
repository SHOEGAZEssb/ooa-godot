using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Independently allocated actors in linkedGameCutscenes.s. Their scripts
// run in the INTERACTION pass, after the native cutscene's signal writes.
internal sealed class LinkedLeadInActorRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _position;
    private int _z,_speedZ,_publishPosition;
    private int _counter=120;
    internal int Id {get;}
    internal int SubId {get;}
    internal int State {get;private set;}
    internal NpcCharacter Actor=>_actor;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    private bool Always=>Id==0xb0||Id==0x36&&SubId==0x12||Id==0xaa;
    public bool UpdatesDuringDialogue=>State==0||Always;
    public bool UpdatesDuringRoomEntityFreeze=>State==0||Always;
    internal LinkedLeadInActorRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,int id,int subid,Vector2 position)
    {Context=context;_data=data;Id=id;SubId=subid;_position=position;_actor=data.Actor(id,subid,position);_runner=new(this);}
    private int Read(int address)=>Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value)=>Context.Entities.RuntimeState.SetWramByte(address,(byte)value);
    private void Animation(int animation)=>_actor.SetScriptAnimation(_data.Animation(Id,animation));
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_actor.SetScriptAnimation(_actor.Record.DownAnimation);_actor.ShowInitializedPresentation();_actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            string? script=null;
            switch(Id)
            {
                case 0x31:
                    Animation(SubId==0x0a?2:3);
                    if(SubId!=0x0a)script=SubId==9?"lead-impa":"kidnap-impa";break;
                case 0xad:script="kidnap-zelda";break;
                case 0x3c:script="kidnap-boy";break;
                case 0xb0:_actor.SetBasePalette(SubId==2?1:2);if(SubId==3)Animation(1);break;
                case 0x36:
                    if(SubId==0x12){_position=new(0x70,0x48);_actor.Position=_position;}
                    else {Animation(0);Write(0xcfd5,(int)_position.Y);Write(0xcfd6,(int)_position.X);script="prerites-nayru";}break;
                case 0x37:Animation(1);break;
                case 0xaa:_position=new(0x30,0x48);_actor.Position=_position;_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);break;
                case 0x3a:_actor.SetBasePalette(6);Animation(13);break;
                case 0x43:_actor.SetBasePalette(6);break;
                case 0x4d:break; // ambiSubid09 is a native RET.
                case 0x6b:_actor.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(0x81));Context.Sound.PlayNativeSound(0x72);break;
                default:throw UnsupportedCommand($"linked lead-in actor ${Id:x2}:${SubId:x2}");
            }
            if(script is not null)_runner.Start(_data.Commands(script));
            // zelda_state0 falls through into its ordinary animation/script.
            if(Id!=0xad&&!(Id==0x31&&SubId is 8 or 9))return;
        }
        if(_publishPosition!=0){Write(0xcfd5,(int)_position.Y);Write(0xcfd6,(int)_position.X);}
        if(Id==0x6b&&--_counter==0){Finished=true;_actor.SetActive(false);return;}
        if(Id is 0x3a or 0x43)return;
        int speed=_runner.ActorSpeed(new("Actor"));
        int updates=Id is 0x31 or 0x3c&&_runner.MovementCounter(new("Actor"))!=0?
            speed<0x28?1:speed<0x50?2:speed<0x78?3:4:1;
        if(Id==0x3c)
        {
            _runner.AdvanceFrame();if(Finished)return;
            speed=_runner.ActorSpeed(new("Actor"));
            updates=_runner.MovementCounter(new("Actor"))==0||speed<0x28?1:speed<0x50?2:speed<0x78?3:4;
            _actor.AdvanceAnimationUpdates(updates);_actor.ClearFixedDrawPriority();
            _actor.PushPlayerAwayAndUpdateDrawPriority(Context.Player);
        }
        else {_actor.AdvanceAnimationUpdates(updates);_runner.AdvanceFrame();}
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void SetActorAnimation(string actor,int animation,string encodedAnimation)=>_actor.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor,int angle,string encodedAnimation)=>_actor.SetScriptAnimation(encodedAnimation);
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override int ReadMemory(string binding)=>binding.StartsWith("Bit:",StringComparison.Ordinal)?(Read(0xcfc0)>>int.Parse(binding[4..]))&1:
        binding=="EndingSignal"?Read(0xcfd0):throw UnsupportedCommand(binding);
    public override void WriteMemory(string binding,int value)
    {if(binding!="EndingSignal")throw UnsupportedCommand(binding);Write(0xcfd0,value);}
    public override void WriteObjectByte(string actor,int address,int value)
    {if(address!=0x78)throw UnsupportedCommand($"object byte${address:x2}");_publishPosition=value;}
    public override void MoveActorAtSpeed(string actor,int speed,int angle)=>_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _position,speed,angle);
    public override void ShowText(int id,string message,int? position)=>Context.ShowDialogue(message,position,sourceTextId:id);
    public override void RunNativeHandler(string handler)
    {
        if(handler.StartsWith("Toggle:",StringComparison.Ordinal)){Write(0xcfc0,Read(0xcfc0)^(1<<int.Parse(handler[7..])));return;}
        switch(handler)
        {
            case "Exclamation":if(Context.Entities.InteractionSlotAvailable)Context.Entities.Spawn(new ExclamationMarkSpawn(_position+new Vector2(0,-13),Duration:30));return;
            case "FaceLinkUp":Context.Player.Face(Vector2I.Up);return;
            case "FaceLinkLeft":Context.Player.Face(Vector2I.Left);return;
            case "KidnapText":Context.ShowDialogue(_data.Text(0x0131),textboxFlags:2,sourceTextId:0x0131);return;
            default:throw UnsupportedCommand(handler);
        }
    }
    public override bool UpdateNativeHandler(string handler,CutsceneActorId? actor,int update,int frames,string payload)
    {
        if(handler!="Jump")throw UnsupportedCommand(handler);
        // callscript changes the script cursor and yields. Its first asm15
        // executes on the following update, then gravity runs immediately.
        if(update==0)return false;
        if(update==1){_speedZ=-0x200;Context.Sound.PlaySound(SoundId.SndJump);}
        return OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x30);
    }
    public override bool GateOpen(string gate)=>gate=="PaletteDone"?!Context.Transitions.NativePaletteFadeActive:throw UnsupportedCommand(gate);
    public override void ScriptEnded(){if(Id==0x3c||Id==0x31&&SubId==8){Finished=true;_actor.SetActive(false);}}
}
