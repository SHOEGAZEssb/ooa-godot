using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Resolves ITEM_SHOVEL ($15) against the imported breakable-tile table.
/// Link owns the parent-item timing; this controller performs the child
/// item's single state-0 tile attempt.
/// </summary>
public sealed class ShovelController
{
    private readonly RoomSession _rooms;
    private readonly BreakableTileDatabase _breakables;
    private readonly RoomView _roomView;
    private readonly RoomEntityManager _entities;
    private readonly OracleSaveData _saveData;
    private readonly Action<int> _playSound;
    private readonly Func<long> _animationTick;

    public ShovelController(
        RoomSession rooms,
        BreakableTileDatabase breakables,
        RoomView roomView,
        RoomEntityManager entities,
        OracleSaveData saveData,
        Action<int> playSound,
        Func<long> animationTick)
    {
        _rooms = rooms;
        _breakables = breakables;
        _roomView = roomView;
        _entities = entities;
        _saveData = saveData;
        _playSound = playSound;
        _animationTick = animationTick;
    }

    public bool TryDig(Vector2 point, Vector2I direction)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        byte tile = room.GetMetatile(point);
        if (!_breakables.TryGet(
                room.ActiveCollisions, tile,
                out BreakableTileDatabase.BreakableTileRecord record) ||
            !record.AllowsSource(BreakableTileDatabase.SourceShovel))
        {
            _playSound(OracleSoundEngine.SndClink);
            return false;
        }
        if ((record.Effect & 0x1f) != 0x0a)
        {
            throw new InvalidOperationException(
                $"Unsupported shovel break effect ${record.Effect:x2} for " +
                $"collision set ${room.ActiveCollisions:x2}, tile ${tile:x2}.");
        }

        int packedPosition = room.GetPackedPosition(point);
        Vector2 tileCenter = new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        bool changed = record.Replacement == 0 || room.ReplaceMetatile(
            tileCenter, tile, (byte)record.Replacement, _animationTick());
        if (!changed)
        {
            _playSound(OracleSoundEngine.SndClink);
            return false;
        }

        // updateRoomFlagsForBrokenTile is selected by effect bit 7. For the
        // shovel source in Ages this is overworld/underwater tile $cb: the
        // source tables add 50 maturity and set current-room flag bit 7.
        if ((record.Effect & 0x80) != 0)
        {
            if (room.ActiveCollisions is 0 or 4 && tile == 0xcb)
            {
                _saveData.AddGashaMaturity(50);
                _saveData.SetRoomFlag(
                    _rooms.ActiveGroup, room.Id, OracleSaveData.RoomFlag80);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported shovel room-flag update for collision set " +
                    $"${room.ActiveCollisions:x2}, tile ${tile:x2}.");
            }
        }

        if ((record.Effect & 0x40) != 0)
            _playSound(OracleSoundEngine.SndSolvePuzzle);
        if (record.Drop != 0)
            _entities.SpawnBreakableDrop(record.Drop, tileCenter, direction);

        _entities.Spawn<ShovelDebrisEffect>(
            new ShovelDebrisSpawn(tileCenter, direction));
        _roomView.QueueRedraw();
        _saveData.AddGashaMaturity(1);
        _playSound(OracleSoundEngine.SndDig);
        return true;
    }
}
