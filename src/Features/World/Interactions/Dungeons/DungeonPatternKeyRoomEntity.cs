using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$01/$05/$10.</summary>
internal sealed partial class DungeonPatternKeyRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly int _firstTile;
    private readonly IReadOnlyList<byte>[] _patterns;
    private readonly GroundTreasureGrantRequest _request;
    private readonly Func<bool> _itemFlagSet;
    private readonly Func<GroundTreasureGrantRequest, bool> _createTreasure;
    private readonly Func<IRoomEntity, bool> _isOutgoing;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DungeonPatternKeyRoomEntity(
        DungeonObjectRecord record,
        Func<OracleRoomData> activeRoom,
        int firstTile,
        IReadOnlyList<byte>[] patterns,
        GroundTreasureGrantRequest request,
        Func<bool> itemFlagSet,
        Func<GroundTreasureGrantRequest, bool> createTreasure,
        Func<IRoomEntity, bool> isOutgoing)
    {
        _record = record;
        _activeRoom = activeRoom;
        _firstTile = firstTile;
        _patterns = patterns;
        _request = request;
        _itemFlagSet = itemFlagSet;
        _createTreasure = createTreasure;
        _isOutgoing = isOutgoing;
        Position = record.Position;
        Name = $"DungeonPatternKey_{record.Group}_{record.Room:x2}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance();

    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Advance();
        return ScreenTransitionPresentation.Visible;
    }

    private void Advance()
    {
        // Only $10 calls interactionDeleteAndRetIfEnabled02. The shared
        // $01/$05 handlers read the current room's flags and floor even while
        // retained as outgoing state-$00 objects.
        if (_record.SubId == 0x10 && _isOutgoing(this)) Finished = true;
        if (_itemFlagSet()) Finished = true;
        if (Finished || !DungeonTilePattern.Matches(_activeRoom(), _firstTile, _patterns)) return;
        // createTreasure allocates INTERAC$60 before interactionDelete. A full
        // pool leaves this owner alive to retry on the next eligible dispatch.
        if (_createTreasure(_request)) Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

}
