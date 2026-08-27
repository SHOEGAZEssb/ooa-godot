using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_EXTENDABLE_BRIDGE $23. A selected wSwitchState bit starts a
/// source-ordered bridge creation or removal stream at ten-update intervals.
/// </summary>
internal sealed partial class ExtendableBridgeRoomEntity :
    DungeonMechanicRoomEntity, IFixedRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly IReadOnlyList<DungeonTilePatternRecord> _creation;
    private readonly IReadOnlyList<DungeonTilePatternRecord> _removal;
    private readonly Func<long> _animationTick;
    private readonly Action _roomTileChanged;
    private readonly Action<int> _playSound;
    private bool _initialized;
    private bool _bridgePresent;
    private bool _updatingTiles;
    private byte _lastSwitchState;
    private int _counter;
    private int _patternIndex;

    internal int ToggleMask => 1 << (_record.SubId & 0x07);
    internal int PackedPosition => _record.PackedPosition;
    internal int PatternVariant => _record.Parameter;
    internal bool BridgePresent => _bridgePresent;
    internal bool UpdatingTiles => _updatingTiles;
    internal int Counter => _counter;
    internal int PatternIndex => _patternIndex;

    internal ExtendableBridgeRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        OracleRuntimeState runtime,
        Func<long> animationTick,
        Action roomTileChanged,
        Action<int> playSound)
        : base(record, $"ExtendableBridge_{record.Parameter}_{record.Order}")
    {
        if (record.Id != 0x23 || record.SubId > 0x07 || record.Parameter > 6)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _runtime = runtime;
        _creation = data.TilePattern(0x23, record.Parameter);
        _removal = data.TilePattern(0x23, 0x80 | record.Parameter);
        if (_creation.Count == 0 || _removal.Count != _creation.Count)
        {
            throw new InvalidOperationException(
                $"Room {record.Group:x1}:{record.Room:x2} bridge variant " +
                $"{record.Parameter} has incomplete creation/removal patterns.");
        }
        _animationTick = animationTick;
        _roomTileChanged = roomTileChanged;
        _playSound = playSound;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            int tile = _room.GetMetatile(Position);
            _bridgePresent = tile >= _data.BridgeFirstTile &&
                tile < _data.BridgeFirstTile + _data.BridgeTileCount;
            _lastSwitchState = SwitchState();
            return;
        }

        if (!_updatingTiles)
        {
            byte current = SwitchState();
            if (((current ^ _lastSwitchState) & ToggleMask) == 0)
                return;
            _lastSwitchState = current;
            _updatingTiles = true;
            _patternIndex = 0;
            _counter = _data.BridgeStepWait;
            return;
        }

        _counter--;
        if (_counter != 0)
            return;
        _counter = _data.BridgeStepWait;

        IReadOnlyList<DungeonTilePatternRecord> pattern =
            _bridgePresent ? _removal : _creation;
        if (_patternIndex >= pattern.Count)
        {
            _bridgePresent = !_bridgePresent;
            _updatingTiles = false;
            _counter = 0;
            return;
        }

        DungeonTilePatternRecord step = pattern[_patternIndex++];
        _room.SetPositionTileAndCollision(
            Point(step.PackedPosition),
            (byte)step.Tile,
            null,
            _animationTick());
        _roomTileChanged();
        _playSound(_data.DoorSound);
    }

    private byte SwitchState() => _runtime.ReadWramByte(
        OracleRuntimeState.SwitchStateAddress);

    private static Vector2 Point(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
