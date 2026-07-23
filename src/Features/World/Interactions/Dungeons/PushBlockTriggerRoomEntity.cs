using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native $13:$01 handler. It temporarily contributes one to the original
/// wNumEnemies count and releases all enemies 30 updates after its block moves.
/// </summary>
internal sealed partial class PushBlockTriggerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _roomEnemyCount;
    private readonly Func<long> _animationTick;
    private int _state;
    private int _counter;
    private byte _originalTile;
    private byte _originalCollision;

    internal int PackedPosition { get; }
    public bool Finished { get; private set; }
    public bool CountsAsEnemy => _state != 0 && !Finished;

    internal PushBlockTriggerRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<int> roomEnemyCount,
        Func<long> animationTick)
        : base(record, $"PushBlockTrigger_{record.Order}")
    {
        if (record is not { Id: 0x13, SubId: 0x01 })
            throw new ArgumentOutOfRangeException(nameof(record));
        _room = room;
        _data = data;
        _roomEnemyCount = roomEnemyCount;
        _animationTick = animationTick;
        PackedPosition = record.PackedPosition;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (_state)
        {
            case 0:
                _state = 1;
                // loadTilesetAndRoomLayout restores the source buffer before
                // this object initializes. OracleWorldData caches mutable room
                // instances, so read that source layout explicitly instead of
                // treating a stale temporary `$1d sentinel as the real block.
                _originalTile = _room.GetOriginalMetatile(Position);
                _originalCollision = _room.GetCollision(_originalTile);
                _room.SetPositionTileAndCollision(
                    Position, (byte)_data.PushableBlock, _originalCollision,
                    _animationTick(), preserveRenderedTile: true);
                return;

            case 1:
                // subid $01 waits until wNumEnemies is no greater than one;
                // this interaction itself is that one synthetic enemy.
                if (_roomEnemyCount() > 1)
                    return;
                _state = 2;
                _room.SetPositionTileAndCollision(
                    Position, _originalTile, _originalCollision,
                    _animationTick(), preserveRenderedTile: true);
                return;

            case 2:
                if (_room.GetMetatile(Position) == _originalTile)
                    return;
                _state = 3;
                _counter = _data.PushDelay;
                return;

            case 3:
                _counter--;
                if (_counter == 0)
                    Finished = true;
                return;

            default:
                throw new InvalidOperationException(
                    $"Push-block trigger at ${PackedPosition:x2} entered state {_state}.");
        }
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
