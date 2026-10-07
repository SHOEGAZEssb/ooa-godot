using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$0e, the current-room $4a:$2a block goal.</summary>
internal sealed partial class MoonlitGrottoFallingKeyRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly GroundTreasureGrantRequest _request;
    private readonly Func<bool> _itemFlagSet;
    private readonly Func<GroundTreasureGrantRequest,bool> _createTreasure;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int GoalPosition => _data.MoonlitKeyGoalPosition;
    internal int GoalTile => _data.MoonlitKeyGoalTile;

    internal MoonlitGrottoFallingKeyRoomEntity(DungeonMechanicDatabaseRecord record,DungeonMechanicDatabase data,
        Func<OracleRoomData> activeRoom,GroundTreasureGrantRequest request,Func<bool> itemFlagSet,
        Func<GroundTreasureGrantRequest,bool> createTreasure)
    {
        if (record.Id != InteractionId.DungeonEvents || record.SubId != 0x0e)
            throw new ArgumentOutOfRangeException(nameof(record));
        _data = data;
        _activeRoom = activeRoom;
        _request = request;
        _itemFlagSet = itemFlagSet;
        _createTreasure = createTreasure;
        Position = new(record.Parameter,record.PackedPosition);
        Name = $"MoonlitFallingKey_{record.Room:x2}";
        Visible = false;
    }

    private void Advance()
    {
        if (_itemFlagSet()) Finished = true;
        if (Finished || _activeRoom().GetPackedStorageMetatile((byte)GoalPosition) != GoalTile) return;
        // Like $09, state0 remains eligible under text/masks/scroll. Read
        // current flags/layout, and delete only after checked $60 allocation.
        if (_createTreasure(_request)) Finished = true;
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Advance();
        return ScreenTransitionPresentation.Hidden;
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
