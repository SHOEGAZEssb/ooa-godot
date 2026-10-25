using Godot;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Oracles;

internal sealed class BlackTowerPossessionEvent(RoomEventContext context) : IRoomEntryEvent
{
    private readonly BlackTowerPossessionDatabase _data=new();
    private readonly HashSet<IRoomEntity> _owned=[];
    private VeranFairyDatabase? _fairyData;
    private VeranFairyDatabase FairyData=>_fairyData??=new();
    internal Action BeginEscapeAttempt {get;set;}=null!;
    public bool HasState {get;private set;}
    public bool BlocksGameplay=>false;
    // ENEMY $61 retains the shared possession fade owner. It advances in
    // RoomEventController's pre-object phase; neither event has a script
    // dispatch in UpdateFrame to advance twice.
    public bool OwnsUpdatesOf(IRoomEvent other)=>other is PalacePossessionEvent;
    public bool Matches(int group,OracleRoomData room)=>group==4&&room.Id==0xfc;
    public void Start(OracleRoomData room)
    {
        HasState=true;
        foreach(var actor in context.Entities.EntityAdapters<BlackTowerPossessionActor>())_owned.Add(actor);
    }
    public void UpdateFrame() {}
    internal IRoomEntity CreateAmbi(NpcRecord record)
    {
        var actor=new BlackTowerPossessionActor(context,_data,this,0x4d,record.SubId,new(record.X,record.Y));
        if(record.SubId==7)_owned.Add(actor);
        return actor;
    }
    internal bool SpawnGhost(int subid,Vector2 position)
    {
        if(!context.Entities.InteractionSlotAvailable)return false;
        var actor=new BlackTowerPossessionActor(context,_data,this,0x3e,subid,position);
        _owned.Add(actor);context.Entities.AddEntity(actor);return true;
    }
    internal void Input(bool enabled)
    {
        NativeScriptInputControl.SetEnabled(context.Entities.RuntimeState, context.Player, enabled);
    }
    internal void Aftermath() {HasState=true;_owned.Clear();context.Transitions.ResetCamera();}
    internal void RequestLinkState()=>context.Player.RequestAmbiUnpossessedCutscene(()=>context.Entities.CreatePalaceExclamation(context.Player.Position+new Vector2(-8,-12),30));
    internal void ShowFairyText(int id)=>context.ShowDialogue(FairyData.Text(id),sourceTextId:id);
    internal VeranFairyRoomEntity? SpawnFairy(Vector2 position)=>
        (VeranFairyRoomEntity?)context.Entities.TryAllocateEnemy(_=>
        {
            var world=new VeranFairyEnvironment(FairyData,context.Entities,context.Transitions,
                (id,text)=>context.ShowDialogue(text,sourceTextId:id),point=>
                {
                    if(!context.Entities.PartSlotAvailable)return;
                    var lightning=new LightningRoomEntity(context,point,signalCutscene:false);
                    _owned.Add(lightning);context.Entities.AddEntity(lightning);
                },(point,z)=>context.Entities.TryCreateInteractionExplosion(point,z),
                ()=>BeginEscapeAttempt(),
                entity=>_owned.Add(entity));
            var fairy=new VeranFairyRoomEntity(new(world,position));_owned.Add(fairy);return fairy;
        });
    public void Cancel()
    {
        if(!HasState)return;
        HasState=false;
        foreach(var actor in _owned)context.Entities.RetireEntity(actor);
        foreach(var part in context.Entities.EntityAdapters<VeranProjectileRoomEntity>().ToArray())context.Entities.RetireEntity(part);
        _owned.Clear();
        context.Transitions.CancelNativeWhiteFade();
        context.Player.EndForcedRoomEntryMovement();
        context.Player.CancelAmbiUnpossessedCutscene();
        context.Player.EndCutsceneControl();
        Input(true);
    }
}
