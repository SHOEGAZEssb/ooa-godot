using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Trigger-chest consumers used by dungeon script $20:$00 and dungeon event
/// $21:$17. The former plays the solve cue, creates a puff, waits 15 updates,
/// installs a permanent chest, and deletes. The latter mirrors trigger state
/// immediately and restores the source layout tile when pressure is released.
/// </summary>
internal sealed partial class TriggerChestRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _triggerState;
    private readonly Func<bool> _itemFlagSet;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly byte _originalTile;
    private bool _initialized;
    private int _counter;

    internal int Id => _record.Id;
    internal int SubId => _record.SubId;
    internal int PackedPosition => _record.PackedPosition;
    internal int TriggerParameter => _record.Parameter;
    internal TriggerPredicate Predicate => _record.Predicate;
    internal int Counter => _counter;
    internal byte OriginalTile => _originalTile;
    public bool Finished { get; private set; }

    internal TriggerChestRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<int> triggerState,
        Func<bool> itemFlagSet,
        Func<long> animationTick,
        Action<int> playSound)
        : base(record, $"TriggerChest_{record.Id:x2}_{record.Order}")
    {
        if (record is not ({ Id: 0x20, SubId: 0x00 } or
            { Id: 0x21, SubId: 0x17 }))
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _room = room;
        _data = data;
        _triggerState = triggerState;
        _itemFlagSet = itemFlagSet;
        _animationTick = animationTick;
        _playSound = playSound;
        _originalTile = room.GetOriginalMetatile(Position);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_record.Id == 0x21)
        {
            UpdateRetractable(spawns);
            return;
        }

        // INTERAC_DUNGEON_SCRIPT executes stopifitemflagset once when its
        // script is selected, then remains parked at its trigger predicate.
        if (!_initialized)
        {
            _initialized = true;
            if (_itemFlagSet())
            {
                Finished = true;
                return;
            }
        }

        if (_counter != 0)
        {
            _counter--;
            if (_counter == 0)
            {
                SetTile((byte)_data.ChestTile);
                Finished = true;
            }
            return;
        }

        if (!TriggerMatches())
            return;

        // spawnChestAfterPuff requests the solve cue before allocating
        // INTERAC_PUFF, then wait 15 reaches zero and executes settilehere in
        // that same update.
        _playSound(_data.SolveSound);
        spawns.Add(new PuzzlePuffSpawn(Position, _data.PuffSound));
        _counter = _data.ChestWait;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private void UpdateRetractable(ICollection<RoomEntitySpawn> spawns)
    {
        // $21:$17 checks ROOMFLAG_ITEM on every update, so opening the dynamic
        // chest retires this controller before it can restore the source tile.
        if (_itemFlagSet())
        {
            Finished = true;
            return;
        }

        byte tile = _room.GetMetatile(Position);
        if (TriggerMatches())
        {
            if (tile == _data.ChestTile)
                return;
            SetTile((byte)_data.ChestTile);
            spawns.Add(new PuzzlePuffSpawn(Position, _data.PuffSound));
            _playSound(_data.SolveSound);
            return;
        }

        if (tile != _data.ChestTile)
            return;
        SetTile(_originalTile);
        spawns.Add(new PuzzlePuffSpawn(Position, _data.PuffSound));
    }

    private bool TriggerMatches() => _record.Predicate switch
    {
        TriggerPredicate.BitSet
            when _record.Parameter is >= 0 and <= 7 =>
                (_triggerState() & (1 << _record.Parameter)) != 0,
        TriggerPredicate.Exact =>
            _triggerState() == _record.Parameter,
        _ => throw new InvalidOperationException(
            $"Unsupported trigger predicate for ${_record.Id:x2}:" +
            $"${_record.SubId:x2} in room {_record.Group:x1}:{_record.Room:x2}.")
    };

    private void SetTile(byte tile) => _room.SetPositionTileAndCollision(
        Position, tile, null, _animationTick());
}
