using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native $13:$01 handler. It temporarily contributes one to the original
/// wNumEnemies count and releases all enemies 30 updates after its block moves.
/// </summary>
internal sealed partial class PushBlockTriggerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<int> _roomEnemyCount;
    private readonly Func<long> _animationTick;
    private readonly Action _clearRoomEnemyCount;
    private readonly Func<IRoomEntity,bool> _isOutgoing;
    private int _state;
    private int _counter;
    private byte _originalTile;

    internal int PackedPosition { get; }
    public bool Finished { get; private set; }
    public bool CountsAsEnemy => _state != 0 && !Finished;
    public bool UpdatesDuringDialogue => _state == 0;
    public bool UpdatesDuringRoomEntityFreeze => _state == 0;

    internal PushBlockTriggerRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<int> roomEnemyCount,
        Func<long> animationTick,
        Action clearRoomEnemyCount,
        Func<IRoomEntity,bool> isOutgoing)
        : base(record, $"PushBlockTrigger_{record.Order}")
    {
        if (record is not { Id: InteractionId.PushBlockTrigger, SubId: 0x01 })
            throw new ArgumentOutOfRangeException(nameof(record));
        _room = room;
        _data = data;
        _roomEnemyCount = roomEnemyCount;
        _animationTick = animationTick;
        _clearRoomEnemyCount = clearRoomEnemyCount;
        _isOutgoing = isOutgoing;
        PackedPosition = record.PackedPosition;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (_state)
        {
            case 0:
                _state = 1;
                // pushblockTrigger.s saves the live CF byte. Both sentinel
                // writes leave CE, graphics and the underlying buffer intact.
                _originalTile = _room.GetMetatile(Position);
                _room.SetPositionTileAndCollision(
                    Position, (byte)_data.PushableBlock, _room.GetTerrainInfo(Position).Collision,
                    _animationTick(), preserveRenderedTile: true);
                return;

            case 1:
                // subid $01 waits until wNumEnemies is no greater than one;
                // this interaction itself is that one synthetic enemy.
                if (_roomEnemyCount() > 1)
                    return;
                _state = 2;
                _room.SetPositionTileAndCollision(
                    Position, _originalTile, _room.GetTerrainInfo(Position).Collision,
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
                {
                    Finished = true;
                    _clearRoomEnemyCount();
                }
                return;

            default:
                throw new InvalidOperationException(
                    $"Push-block trigger at ${PackedPosition:x2} entered state {_state}.");
        }
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // updateInteractions admits state0 during scroll mode$08. Enabled$02
        // deletes before returnIfScrollMode01Unset; incoming state0 returns.
        if (_state == 0 && _isOutgoing(this)) Finished = true;
    }

}
