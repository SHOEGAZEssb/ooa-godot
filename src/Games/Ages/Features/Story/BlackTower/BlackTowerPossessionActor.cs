using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class BlackTowerPossessionActor : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity,IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly BlackTowerPossessionDatabase _data;
    private readonly BlackTowerPossessionEvent _owner;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    private bool _ended;
    private int _counter;
    private VeranFairyRoomEntity? _fairy;
    public override RoomEventContext Context {get;}
    internal int Id {get;}
    internal int SubId {get;}
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter=>_counter;
    internal Vector2 PrecisePosition=>_precise;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0||Id==0x3e&&SubId==1;
    public bool UpdatesDuringRoomEntityFreeze=>State==0||Id==0x3e&&SubId==1;
    internal BlackTowerPossessionActor(RoomEventContext context,BlackTowerPossessionDatabase data,BlackTowerPossessionEvent owner,int id,int subid,Vector2 position)
    {
        Context=context;_data=data;_owner=owner;Id=id;SubId=subid;_precise=position;_actor=data.Actor(id,subid,position);_runner=new(this);
    }
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // bank0.updateInteractions admits state0 during scrolling. Resolve
        // Ambi's deletion/visibility and her same-pass ghost allocation here.
        if(State==0)Initialize();
        return _actor.Visible?ScreenTransitionPresentation.Visible:ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // ghostVeran.s:subid1 sets the always-update bit. Ambi freezes after
        // initialization; an unsuccessful ghost allocation retains state0.
        if(State==0||Id==0x3e&&SubId==1)UpdateFrame(frame,Array.Empty<RoomEntitySpawn>());
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){Initialize();return;}
        if(Id==0x3e&&SubId==2) {UpdateRescueGhost();return;}
        if(Id!=0x3e||!Context.DialogueOpen)_runner.AdvanceFrame();
        if(_ended) {if(Id==0x4d)Write(AgesWramAddress.wLoadedTreeGfxIndex,1);Finish();return;}
        _actor.AdvanceAnimationUpdates(1);
    }
    private void Initialize()
    {
        State=1;_actor.ShowInitializedPresentation();
        _actor.SetFixedDrawPriority(Id==0x3e?ObjectDrawPriority.FixedLowPriorityZIndex:ObjectDrawPriority.BehindLinkZIndex);
        if(Id==0x4d)
        {
            if(SubId==6)
            {
                if(Context.Rooms.SaveData.HasRoomFlag(4,0xfc,0x80)){Finish();return;}
                Context.Entities.SetNativeRoomEnemyCount(1);
                if(!_owner.SpawnGhost(1,_actor.Position+new Vector2(0,-16))) {State=0;return;}
                Context.Sound.PlaySound(SoundId.SndCtrlStopMusic);Context.Sound.SetNativeActiveMusic(0xff);
                Context.Player.Face(Vector2I.Up);Context.Entities.EnableLinkCollisionsAndMenu();Write(0xcfc0,0);Context.Entities.BeginBossEntrySignal();
                Context.Player.RequestNativeForcedMovement(0x16,0,0);
            }
            _runner.Start(SubId==6?_data.Intro:_data.Rescue);_actor.SetStatePosition(_actor.Position);
        }
        else if(SubId==1)
        {
            _actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);_runner.Start(_data.Ghost);
            _runner.SetInitialMotionRegisters(new("Actor"),0x1e,0x10);
        }
        else Context.Sound.PlaySound(SoundId.SndBeam);
    }
    private void UpdateRescueGhost()
    {
        if(Substate==0)
        {
            Vector2 target=new(0x78,0x58);
            if(Mathf.Abs(_actor.Position.X-target.X)<=1&&Mathf.Abs(_actor.Position.Y-target.Y)<=1)
            {_precise=target+new Vector2(_precise.X%1,_precise.Y%1);_actor.Position=target;Substate=1;_counter=60;}
            else MoveActorAtSpeed("Actor",0x50,OracleObjectMovement.Shared.RelativeAngle(_actor.Position,target));
            _actor.AdvanceAnimationUpdates(1);
        }
        else if(Substate==1)
        {
            if(--_counter!=0){_actor.AdvanceAnimationUpdates(1);return;}
            Substate=2;
            _owner.ShowFairyText(0x560e);
        }
        else if(Substate==2)
        {
            _fairy=_owner.SpawnFairy(_actor.Position);
            if(_fairy is null)return;
            Substate=3;_counter=61;
        }
        else if(Substate==3)
        {
            if(--_counter==0){Write(AgesWramAddress.wLoadedTreeGfxIndex,1);Finish();return;}
            _actor.SetScriptVisible(!_fairy!.Actor.Visible);
            if(!_fairy.Actor.Visible)_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        }
    }
    private void Finish(){Finished=true;_actor.SetActive(false);}
    private void Write(int address,int value)=>Context.Entities.RuntimeState.SetWramByte(address,unchecked((byte)value));
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void SetInputEnabled(bool enabled)=>_owner.Input(enabled);
    public override int ReadMemory(string binding)=>binding switch
    {"GhostSignal"=>Context.Entities.RuntimeState.ReadWramByte(0xcfc0)&1,"BossShutters"=>Context.Entities.BossEntrySignal,_=>throw UnsupportedCommand(binding)};
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void MoveActorAtSpeed(string actor,int speed,int angle)=>_actor.Position=NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _precise,speed,angle);
    public override void SetActorMovementAnimation(string actor,int angle,string encodedAnimation)=>_actor.SetScriptAnimation(encodedAnimation);
    public override void ShowText(int id,string message,int? position)=>Context.ShowDialogue(message,position,sourceTextId:id);
    public override void ScriptEnded()=>_ended=true;
    public override void RunNativeHandler(string handler)
    {
        switch(handler)
        {
            case "SpawnBoss":Context.Entities.TryCreatePalacePossessionEnemy(_actor.Position,1);return;
            case "ToggleGhost":Write(0xcfc0,ReadMemory("GhostSignal")^1);return;
            case "LinkAmbiState":_owner.RequestLinkState();return;
            case "SpawnGhost":_owner.SpawnGhost(2,new(0x28,0));return;
            default:throw UnsupportedCommand(handler);
        }
    }
}
