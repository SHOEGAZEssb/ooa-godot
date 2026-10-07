using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_BRIDGE_SPAWNER $0c; directional half-tile construction.</summary>
internal sealed partial class BridgeSpawnerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<byte,byte,bool> _setTile;
    private readonly Action _roomTileChanged;
    private readonly Action<int> _playSound;
    private int _position;
    private int _remaining;
    private int _counter;
    private readonly int _angle;
    private bool _initialized;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal BridgeSpawnerRoomEntity(BridgeSpawnerSpawn spawn, OracleRoomData room,
        DungeonMechanicDatabase data, Action roomTileChanged, Action<int> playSound,
        Func<byte,byte,bool> setTile)
        : base(spawn.PackedPosition, "BridgeSpawner")
    {
        _position = spawn.PackedPosition;
        if (spawn.Angle is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(spawn));
        if (spawn.HalfSteps is < 0 or > 0xff) throw new ArgumentOutOfRangeException(nameof(spawn));
        _angle = spawn.Angle;
        _remaining = spawn.HalfSteps;
        _room = room;
        _data = data;
        _setTile = setTile;
        _roomTileChanged = roomTileChanged;
        _playSound = playSound;
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance();

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (!_initialized) Advance();
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Advance();
        return ScreenTransitionPresentation.Hidden;
    }

    private void Advance()
    {
        // State 0 falls through and decrements its newly initialized $08.
        if (Finished) return;
        if (!_initialized) { _initialized = true; _counter = _data.BridgeSpawnerWait; }
        if (--_counter != 0)
            return;
        int tile = _data.BridgeSpawnerTile(_angle, (_remaining & 1) != 0);
        var point = new Vector2((_position & 0x0f) * 16 + 8, (_position >> 4) * 16 + 8);
        _room.SetUnderlyingMetatile(point, (byte)tile);
        // The underlying buffer is written before setTile, even when the
        // shared changed-tile queue rejects the live CF/CE write.
        if (_setTile((byte)_position,(byte)tile)) _roomTileChanged();
        _playSound(_data.DoorSound);
        _counter = _data.BridgeSpawnerWait;
        _remaining = unchecked((byte)(_remaining-1));
        if (_remaining == 0) { Finished = true; return; }
        if ((_remaining & 1) == 0)
            _position = (_position + _data.BridgeSpawnerStep(_angle)) & 0xff;
    }
}

internal sealed record BridgeSpawnerSpawn(int PackedPosition, int HalfSteps, int Angle = 1) : RoomEntitySpawn;
