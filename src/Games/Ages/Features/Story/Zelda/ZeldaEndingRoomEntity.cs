using Godot;
using System.Collections.Generic;

namespace Oracles;

// zelda.s:$ad:$01/$02 retain their source script/animation dispatch. The
// room's placed $00 actor and the Vire rescue keep their existing owners.
internal sealed class ZeldaEndingRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public override RoomEventContext Context {get;}
    private readonly BlackTowerEndingDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _position;
    private bool _animationStopped;
    internal int Subid {get;}
    internal int State {get;private set;}
    internal NpcCharacter Actor=>_actor;
    internal Vector2 PrecisePosition=>_position;
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal ZeldaEndingRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,int subid,Vector2 position)
    {Context=context;_data=data;Subid=subid;_position=position;_actor=data.Actor(0xad,subid,position);_runner=new(this);}
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_actor.SetScriptAnimation(_actor.Record.UpAnimation);_actor.ShowInitializedPresentation();_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);
            _runner.Start(_data.Commands(Subid==1?"reunion-zelda":"credits-zelda"));
        }
        if(!_animationStopped)_actor.AdvanceAnimationUpdates(1);
        _runner.AdvanceFrame();
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void ShowText(int id,string message)=>ShowText(id,message,null);
    public override void ShowText(int id,string message,int? position)=>Context.ShowDialogue(message,position,sourceTextId:id);
    public override void SetActorAnimation(string actor,int animation,string encoded)=>_actor.SetScriptAnimation(_data.Animation(0xad,animation));
    public override void SetActorMovementAnimation(string actor,int angle,string encoded)=>SetActorAnimation(actor,(angle&0x18)>>3,encoded);
    public override void MoveActorAtSpeed(string actor,int speed,int angle)=>_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _position,speed,angle);
    public override int ReadMemory(string binding)=>Context.Entities.RuntimeState.ReadWramByte(binding switch{"CreditsSignal"=>0xcfc0,"CreditsDone"=>0xcfdf,_=>throw UnsupportedCommand(binding)});
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void WriteMemory(string binding,int value)=>Context.Entities.RuntimeState.SetWramByte(binding switch{"CreditsSignal"=>0xcfc0,"CreditsDone"=>0xcfdf,_=>throw UnsupportedCommand(binding)},(byte)value);
    public override void WriteObjectByte(string actor,int address,int value)
    {if(address!=0x79)throw UnsupportedCommand($"zelda.s object byte ${address:x2}");_animationStopped=value!=0;}
    public override void RunNativeHandler(string handler)
    {
        if(handler=="Hide")_actor.Visible=false;
        else if(handler=="Reveal"){_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);_actor.Visible=true;}
        else throw UnsupportedCommand(handler);
    }
    public override void ScriptEnded() { }
}
