using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$11-$15, spawnChestAndDeleteSelf.</summary>
internal sealed partial class DungeonPuzzleChestRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly Func<bool> _conditionMet;
    private readonly Func<bool> _itemFlagSet;
    private readonly int _chestTile;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly Func<Vector2, bool> _createPuff;
    private readonly Func<IRoomEntity, bool> _isOutgoing;
    private readonly Action? _queuedWrite;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DungeonPuzzleChestRoomEntity(DungeonObjectRecord record, OracleRoomData room,
        Func<bool> conditionMet, Func<bool> itemFlagSet, int chestTile,
        Action<int> playSound, Action roomTileChanged, Func<long> animationTick,
        Func<Vector2, bool> createPuff, Func<IRoomEntity, bool> isOutgoing, Action? queuedWrite = null)
    {
        _record = record;
        _room = room;
        _conditionMet = conditionMet;
        _itemFlagSet = itemFlagSet;
        _chestTile = chestTile;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _createPuff = createPuff;
        _isOutgoing = isOutgoing;
        _queuedWrite = queuedWrite;
        Position = record.Position;
        Name = "DungeonPuzzleChest";
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
        // These handlers remain state $00. updateInteractions admits that
        // state during scrolling. All except $12 check enabled$02 and delete
        // the outgoing owner before checking room flags or the puzzle.
        if (_record.SubId != 0x12 && _isOutgoing(this)) Finished = true;
        if (_itemFlagSet())
            Finished = true;
        if (Finished || !_conditionMet())
            return;
        _playSound(SoundId.SndSolvePuzzle);
        if (_queuedWrite is not null) _queuedWrite();
        else
        {
            _room.SetPositionTileAndCollision(Position, (byte)_chestTile, null, _animationTick());
            _roomTileChanged();
        }
        // spawnChestAndDeleteSelf attempts objectCreatePuff before deleting
        // this interaction. Its own slot cannot satisfy that allocation.
        _createPuff(Position);
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
