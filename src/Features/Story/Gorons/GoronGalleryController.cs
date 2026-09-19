using Godot;
using System;

namespace oracleofages;

internal sealed class GoronGalleryController(GoronCaveScriptHost host)
{
    private readonly OracleRoomData _room=host.Context.Rooms.CurrentRoom;
    private RoomEventContext Context => host.Context;
    private OracleRuntimeState Wram => Context.Entities.RuntimeState;
    private readonly ShootingGalleryEventDatabase _data=new(host.Actor.Record.SubId);
    private ShootingGalleryGameController? _controller;
    private ShootingGallerySession? _session;
    private int _state=1;
    private bool _result, _equipped, _started;
    private string? _retry;
    internal bool Playing => _state==2;
    internal bool MenusDisabled { get; private set; }
    internal int Score => _session?.Score??0;
    internal bool FinalRound => _session?.Round==10;
    internal ShootingGallerySession? Session => _session;
    internal void Update()
    {
        if(_retry is not null) { host.StartScript(_retry); _retry=null; }
        if(_state==2&&!_result)
        {
            if(_session is {PendingResult:>=0})
            {
                int result=_session.PendingResult; _session.PendingResult=-1;
                _result=true; host.SetInputEnabled(false);
                host.StartCommands(_data.BuildResultCommands(result));
            }
            else if(_session is {GameComplete:true})
            {
                Wram.SetWramByte(0xcfc0,1); Wram.SetWramByte(0xcfdc,0); _state=3;
                host.StartScript(host.Actor.Record.SubId==1?"shootingGalleryScript_goronNpc_gameDone":"shootingGalleryScript_goronElderNpc_gameDone");
            }
            else return;
        }
        host.AdvanceScript();
    }
    internal void ScriptEnded()
    {
        if(_result)
        { _result=false; _controller!.CompleteResultScript(); return; }
        if(_state==1) { _state=2; Wram.SetWramByte(0xcfc0,0); return; }
        if(_state==3)
        {
            _state=1;
            _retry=host.Actor.Record.SubId==1?"shootingGalleryScript_goronNpc@tryAgain":"shootingGalleryScript_goronElderNpc@beginGame";
        }
    }
    internal void Equip(bool biggoron)
    {
        Wram.SetWramByte(0xcfd7,(byte)Context.Inventory.EquippedB);
        Wram.SetWramByte(0xcfd8,(byte)Context.Inventory.EquippedA);
        _equipped=true;
        if(biggoron) Context.Inventory.SetScriptedEquippedItems(InventoryState.ItemBiggoronSword,InventoryState.ItemBiggoronSword);
        else Context.Inventory.SetScriptedEquippedItems(Context.Inventory.EquippedA==5?0:5,Context.Inventory.EquippedA==5?5:0);
    }
    internal void RestoreEquips()
    {
        if(!_equipped) return;
        Context.Inventory.SetScriptedEquippedItems(Wram.ReadWramByte(0xcfd7),Wram.ReadWramByte(0xcfd8));
        _equipped=false;
    }
    internal void Begin()
    {
        _started=true; MenusDisabled=true; Wram.SetWramByte(0xcfdc,1);
        _session=new(){VariantDatabase=_data};
        _controller=Context.Entities.Spawn<ShootingGalleryGameController>(new ShootingGalleryGameControllerSpawn(_session));
    }
    internal void EnableObjects()
    { host.SetInputEnabled(true); MenusDisabled=true; }
    internal void EnableInput() => MenusDisabled=false;
    internal void Entrance(int value)
    {
        SetTile(0x74,value==0?0xe0:0xc6); SetTile(0x75,value==0?0xe1:0xc6);
    }
    internal void RemoveTargets()
    {
        for(int i=0;i<_data.TargetCount;i++) SetTile(_data.Target(i).PackedPosition,_data.Record.FloorTile);
    }
    private void SetTile(int packed,int value) => _room.SetPositionTileAndCollision(
        new Vector2((packed&15)*16+8,(packed>>4)*16+8),(byte)value,null,Context.AnimationTick());
    internal void Cancel()
    {
        RestoreEquips();
        if(_started) { Entrance(0); RemoveTargets(); }
        MenusDisabled=false; _session=null; _controller=null;
    }
}
