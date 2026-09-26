using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class DefeatedMoblinEvent : RoomCutsceneCommandHost, IRoomEntryEvent, IUpdatesDuringDialogueRoomEvent
{
    private readonly List<DefeatedMoblinScriptHost> _actors=[];
    private readonly RemoteMakuMoblinEvent _remote;
    private bool _active;
    private int _fadeUpdates=-1;
    internal DefeatedMoblinDatabase Database {get;}=new();
    public override RoomEventContext Context {get;}
    public bool HasState=>_active;
    public bool BlocksGameplay=>_active && _remote.Stage!=RemoteMakuEventStage.Tail;
    public bool MenusDisabled=>BlocksGameplay;
    public bool AllScreenTransitionsDisabled=>BlocksGameplay;
    internal IReadOnlyList<DefeatedMoblinScriptHost> Actors=>_actors;
    internal RemoteMakuMoblinEvent Remote=>_remote;
    internal DefeatedMoblinEvent(RoomEventContext context) {Context=context;_remote=new(context);}
    public bool Matches(int group,OracleRoomData room)=>group==0 && room.Id==9;
    public void Start(OracleRoomData room)
    {
        Cancel();
        var king=Context.RequireNpc(0,9,InteractionId.KingMoblinDefeated,0,"INTERAC_KING_MOBLIN_DEFEATED");
        if(!Context.Rooms.SaveData.HasGlobalFlag(GlobalFlag.MoblinsKeepDestroyed) || Context.Rooms.SaveData.HasRoomFlag(0,9,OracleSaveData.RoomFlag40))
        {king.SetActive(false);return;}
        _actors.Add(new(this,king,Context.Entities.InteractionSlot(king)));
        _active=true;
    }
    public void UpdateFrame()
    {
        if(!_active)return;
        bool remoteUpdated=false;
        // Live $d2-$df scan: children in earlier freed slots wait until the
        // next update; later children initialize during this same pass.
        for(int slot=2;slot<16;slot++)
        {
            var host=_actors.FirstOrDefault(a=>a.Slot==slot && GodotObject.IsInstanceValid(a.Actor) && a.Actor.Active);
            if(host is null)continue;
            if(host.Remote)
            {
                if(!host.Initialized) {host.Initialized=true;_remote.StartMessage();}
                _remote.UpdateFrame();
                remoteUpdated=true;
                if(_remote.Stage!=RemoteMakuEventStage.Running)host.Actor.SetActive(false);
                continue;
            }
            if(Context.DialogueOpen)continue;
            if(!host.Initialized) {Initialize(host);continue;}
            host.Runner.AdvanceFrame();
            if(!host.Ended) {host.Actor.AdvanceAnimationUpdates(1);continue;}
            if(host.Actor.Record.SubId==0 && !Spawn(2,0,0x68,0))continue;
            host.Actor.SetActive(false);
        }
        if(!remoteUpdated && _remote.Stage==RemoteMakuEventStage.Tail)_remote.UpdateFrame();
        UpdateFade();
        if(!_remote.HasState && _actors.All(a=>!GodotObject.IsInstanceValid(a.Actor)||!a.Actor.Active))
        {
            _active=false;EventResources.UnlockInput();
        }
    }
    private void Initialize(DefeatedMoblinScriptHost host)
    {
        var actor=host.Actor; int subid=actor.Record.SubId,index=actor.Record.Var03;
        host.Initialized=true;actor.SetAnimationRate(0);actor.SetFixedDrawPriority(NpcCharacter.BehindLinkZIndex);
        if(subid==0)
        {
            Context.Player.SetLocalRespawnPosition(Context.Player.Position);
            var facing=Context.Player.FacingVector;
            Context.Rooms.SaveData.SetDeathRespawnPoint(0,9,0,facing==Vector2I.Up?0:facing==Vector2I.Right?1:facing==Vector2I.Down?2:3,
                (int)Context.Player.Position.Y,(int)Context.Player.Position.X);
            EventResources.LockInput();
            Spawn(1,0,0x68,0x38);Spawn(1,0,0x68,0x78);
            for(int address=0xcfd0;address<0xcfd4;address++)Context.Entities.RuntimeState.SetWramByte(address,0);
            EventResources.CaptureFullScreenFade(Context.Hud.ZIndex+1);_fadeUpdates=0;
        }
        if(subid==2)
        {
            var data=Database.Bytes("gorons"); int offset=index*4;
            actor.Position=new(data[offset+1],data[offset]);host.Angle=data[offset+2];
            actor.SetScriptAnimation(Database.Animation(data[offset+3]));
        }
        host.Runner.Start(Database.Commands,Database.Entry(subid,index));
        host.Runner.SetInitialMotionRegisters("Actor",Database.Bytes("speeds")[subid==2?1:0],host.Angle);
        if(subid==2 && index==0)for(int i=1;i<4;i++)Spawn(2,i,0,0);
    }
    private bool Spawn(int subid,int index,int y,int x)
    {
        if(!Context.Entities.InteractionSlotAvailable)return false;
        var actor=Context.Entities.Spawn<NpcCharacter>(new DefeatedMoblinActorSpawn(Database.Actor(subid,index,y,x)));
        _actors.Add(new(this,actor,Context.Entities.InteractionSlot(actor)));return true;
    }
    internal void SpawnRemote()
    {
        if(!Context.Entities.InteractionSlotAvailable)return;
        var actor=Context.Entities.Spawn<NpcCharacter>(new DefeatedMoblinActorSpawn(Database.Actor(0,0,0,0) with {Id=InteractionId.RemoteMakuCutscene,Var03=6}));
        actor.SetScriptVisible(false);
        _actors.Add(new(this,actor,Context.Entities.InteractionSlot(actor),remote:true));
    }
    public void UpdateDuringDialogueFrame() {UpdateFade();_remote.UpdateDuringDialogueFrame();}
    private void UpdateFade()
    {
        if(_fadeUpdates<0)return;
        int step=Math.Min(32,(++_fadeUpdates+1)/2);
        Context.Fade.Color=new Color(1,1,1,1-step/32f);
        if(_fadeUpdates==65){_fadeUpdates=-1;EventResources.ReleaseFullScreenFade(restoreColor:false);}
    }
    public void Cancel()
    {
        foreach(var host in _actors) {host.Runner.Clear();if(GodotObject.IsInstanceValid(host.Actor))host.Actor.SetActive(false);}
        _actors.Clear();_remote.Cancel();EventResources.UnlockInput();EventResources.ReleaseFullScreenFade();
        _fadeUpdates=-1;_active=false;
    }
}
