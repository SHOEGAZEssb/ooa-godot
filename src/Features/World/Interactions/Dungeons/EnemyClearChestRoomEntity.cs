namespace Oracles;

// dungeonStuff.s:$12:$02 runs createChestWhenNoEnemiesScript in states0/1;
// state2 additionally calls objectPreventLinkFromPassing before scriptend.
internal sealed partial class EnemyClearChestRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly RoomEntityManager _owner;
    private readonly RoomSession _rooms;
    private readonly CutsceneCommandRunner _script;
    private int _state, _radiusY, _radiusX;
    public Node2D Node=>this;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>_state==0;
    public bool UpdatesDuringRoomEntityFreeze=>_state==0;
    internal int Counter=>_script.Counter;
    internal bool ScriptBlocked {get;private set;}
    internal bool NoEnemies=>_owner.RoomEnemyCount==0;

    internal EnemyClearChestRoomEntity(DungeonMechanicDatabaseRecord record, DungeonPuzzleDatabase data,
        RoomEntityManager owner, RoomSession rooms)
    {
        if(record is not {Id:InteractionId.DungeonStuff,SubId:2})throw new ArgumentOutOfRangeException(nameof(record));
        _record=record;_owner=owner;_rooms=rooms;
        Name=$"EnemyClearChest_{record.Group}_{record.Room:x2}";Visible=false;
        Position=new((record.PackedPosition&15)*16+8,(record.PackedPosition>>4)*16+8);
        _script=new(new EnemyClearChestScriptHost(this));_script.Start(data.EnemyClearChestCommands);
        TreeExiting+=_script.Clear;
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Advance(frame.Player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException("INTERAC$12:$02 preload requires Link's live death state.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if(player is null)throw new InvalidOperationException("INTERAC$12:$02 preload requires Link's live death state.");
        if(_state==0)Advance(player);
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // Subid02 has no enabled$02 deletion or returnIfScrollMode01Unset.
        if(_state==0)Advance(frame.Player);
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void Advance(Player player)
    {
        if(Finished)return;
        if(_state==0)_state=1;
        else if(_state==2)NpcCharacter.PreventPlayerPassing(player,Position,_radiusY,_radiusX);
        ScriptBlocked=player.IsDying || _owner.IsScriptTextActive();
        _script.AdvanceFrame();
    }
    internal bool RoomFlagSet(int flag)=>_rooms.SaveData.HasRoomFlag(_rooms.ActiveGroup,_rooms.CurrentRoom.Id,checked((byte)flag));
    internal void SetRadii(int y,int x){_radiusY=y;_radiusX=x;}
    internal void Sound(int sound)=>_owner.OnSoundRequested(sound);
    internal void EndScript()=>Finished=true;
    internal void NativeHandler(string handler)
    {
        switch(handler)
        {
            case "CreatePuff":_owner.TryCreatePuzzlePuff(Position);return;
            case "WriteChest":
                if(_rooms.TrySetTile((byte)_record.PackedPosition,ChestDatabase.ClosedTile))_owner.OnRoomTileChanged();
                return;
            case "IncState":_state++;return;
            default:throw new NotSupportedException($"commonScripts.s:createChestWhenNoEnemiesScript: unknown handler '{handler}'.");
        }
    }
}
