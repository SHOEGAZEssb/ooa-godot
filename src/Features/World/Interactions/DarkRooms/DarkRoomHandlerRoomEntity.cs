using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_DARK_ROOM_HANDLER $08. State 0 scans all 176 wRoomLayout bytes in
/// address order; state 1 reacts only when its shared lit count changes.
/// </summary>
internal sealed partial class DarkRoomHandlerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly DarkRoomDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DarkRoomDatabase _data;
    private readonly DarkRoomState _state;
    private bool _initialized;
    private int _lastLitCount;

    public Node2D Node => this;
    internal bool Initialized => _initialized;
    internal int LastLitCount => _lastLitCount;
    internal DarkRoomState State => _state;

    internal DarkRoomHandlerRoomEntity(
        DarkRoomDatabaseRecord record,
        OracleRoomData room,
        DarkRoomDatabase data,
        DarkRoomState state)
    {
        if (record is not
            { Kind: DarkRoomDatabaseObjectKind.Handler, Id: 0x08, SubId: 0x00 })
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        Name = $"DarkRoomHandler_{record.Order}";
        _record = record;
        _room = room;
        _data = data;
        _state = state;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_state.FadeActive)
        {
            _state.AdvanceFade();
            return;
        }
        if (!_initialized)
        {
            InitializeTorches(spawns);
            _initialized = true;
        }

        int lit = _state.LitCount;
        int previous = _lastLitCount;
        if (lit == previous)
            return;
        _lastLitCount = lit;
        if (lit == 0)
        {
            _state.BeginDarken(_data.FullDarkParameter);
            return;
        }
        if (lit == _state.TotalTorches)
        {
            _state.BeginBrighten(0);
            return;
        }
        if (_state.Parameter == _data.PartialDarkParameter)
            return;
        if (lit >= previous)
            _state.BeginBrighten(_data.PartialDarkParameter);
        else
            _state.BeginDarken(_data.PartialDarkParameter);
    }

    private void InitializeTorches(ICollection<RoomEntitySpawn> spawns)
    {
        if (_room.Layout.Length != 176)
        {
            throw new InvalidOperationException(
                $"PART_DARK_ROOM_HANDLER $08 in room {_record.Group:x1}:" +
                $"{_record.Room:x2} requires the 176-byte large-room layout.");
        }
        int count = 0;
        for (int index = 0; index < _room.Layout.Length; index++)
        {
            if (_room.Layout[index] != _data.UnlitTile)
                continue;
            int packedPosition = (index / 16 << 4) | index % 16;
            spawns.Add(new LightableTorchSpawn(_state, packedPosition));
            count++;
        }
        _state.SetTotalTorches(count);
    }
}
