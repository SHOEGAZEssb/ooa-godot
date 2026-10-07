using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$09, verifyTilesAndDropSmallKey.</summary>
internal sealed partial class DungeonTilePatternFallingKeyRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly IReadOnlyList<DungeonTilePatternRecord> _pattern;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly GroundTreasureGrantRequest _request;
    private readonly Func<bool> _itemFlagSet;
    private readonly Func<GroundTreasureGrantRequest,bool> _createTreasure;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DungeonTilePatternFallingKeyRoomEntity(DungeonMechanicDatabaseRecord record,
        IReadOnlyList<DungeonTilePatternRecord> pattern,Func<OracleRoomData> activeRoom,
        GroundTreasureGrantRequest request,Func<bool> itemFlagSet,Func<GroundTreasureGrantRequest,bool> createTreasure)
    {
        if (record.Id != InteractionId.DungeonEvents || record.SubId != 0x09 || pattern.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(record));
        foreach (DungeonTilePatternRecord cell in pattern)
            if (cell.Id != record.Id || cell.SubId != record.SubId)
                throw new ArgumentException("The tile pattern does not belong to the interaction.",nameof(pattern));
        _pattern = pattern;
        _activeRoom = activeRoom;
        _request = request;
        _itemFlagSet = itemFlagSet;
        _createTreasure = createTreasure;
        Position = new(record.Parameter,record.PackedPosition);
        Name = $"DungeonTilePatternKey_{record.Room:x2}_{record.Order}";
        Visible = false;
    }

    private void Advance()
    {
        if (_itemFlagSet()) Finished = true;
        if (Finished) return;
        OracleRoomData room = _activeRoom();
        foreach (DungeonTilePatternRecord cell in _pattern)
            if (room.GetPackedStorageMetatile((byte)cell.PackedPosition) != cell.Tile) return;
        // createTreasure must succeed before interactionDelete. A failed
        // checked allocation leaves the state-zero controller alive to retry.
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
