using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_BRIDGE_SPAWNER $0c; directional half-tile construction.</summary>
internal sealed partial class BridgeSpawnerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<long> _animationTick;
    private readonly Action _roomTileChanged;
    private readonly Action<int> _playSound;
    private int _position;
    private int _remaining;
    private int _counter;
    private readonly int _angle;
    public bool Finished => _remaining == 0;

    internal BridgeSpawnerRoomEntity(BridgeSpawnerSpawn spawn, OracleRoomData room,
        DungeonMechanicDatabase data, Func<long> animationTick,
        Action roomTileChanged, Action<int> playSound)
        : base(spawn.PackedPosition, "BridgeSpawner")
    {
        _position = spawn.PackedPosition;
        if (spawn.Angle is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(spawn));
        _angle = spawn.Angle;
        _remaining = spawn.HalfSteps;
        _counter = data.BridgeSpawnerWait;
        _room = room;
        _data = data;
        _animationTick = animationTick;
        _roomTileChanged = roomTileChanged;
        _playSound = playSound;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // State 0 falls through and decrements its newly initialized $08.
        if (Finished || --_counter != 0)
            return;
        int tile = _data.BridgeSpawnerTile(_angle, (_remaining & 1) != 0);
        var point = new Vector2((_position & 0x0f) * 16 + 8, (_position >> 4) * 16 + 8);
        _room.SetUnderlyingMetatile(point, (byte)tile);
        _room.SetPositionTileAndCollision(
            point,
            (byte)tile, null, _animationTick());
        _roomTileChanged();
        _playSound(_data.DoorSound);
        _counter = _data.BridgeSpawnerWait;
        _remaining--;
        if ((_remaining & 1) == 0)
            _position = (_position + _data.BridgeSpawnerStep(_angle)) & 0xff;
    }
}

internal sealed record BridgeSpawnerSpawn(int PackedPosition, int HalfSteps, int Angle = 1) : RoomEntitySpawn;
