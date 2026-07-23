using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RoomTileChangeWatcherRoomEntity
    : RoomEntityAdapter<Node2D>, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly RoomTileChangeWatcherDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Vector2 _tilePoint;
    private byte _initialTile;

    internal bool Initialized { get; private set; }
    public bool Finished { get; private set; }

    internal RoomTileChangeWatcherRoomEntity(
        RoomTileChangeWatcherDatabaseRecord record,
        OracleRoomData room,
        OracleSaveData save)
        : base(CreateNode(record), static _ => { })
    {
        _record = record;
        _room = room;
        _save = save;
        int x = record.Position & 0x0f;
        int y = record.Position >> 4;
        if (x >= room.WidthInTiles || y >= room.HeightInTiles)
        {
            throw new InvalidOperationException(
                $"{record.Source} watches invalid position ${record.Position:x2} " +
                $"in room {record.Group:x1}:{record.Room:x2}.");
        }
        _tilePoint = new Vector2(
            x * OracleRoomData.MetatileSize + 8,
            y * OracleRoomData.MetatileSize + 8);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;

        if (!Initialized)
        {
            // State 0 checks the flag before reading wRoomLayout. A watcher
            // whose persistent change was already applied deletes itself.
            if (_save.HasRoomFlag(
                _record.Group, _record.Room, _record.RoomFlag))
            {
                Finished = true;
                return;
            }
            _initialTile = _room.GetMetatile(_tilePoint);
            Initialized = true;
            return;
        }

        if (_room.GetMetatile(_tilePoint) == _initialTile)
            return;

        _save.SetRoomFlag(
            _record.Group, _record.Room, _record.RoomFlag);
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private static Node2D CreateNode(
        RoomTileChangeWatcherDatabaseRecord record) => new()
    {
        Name = $"TileChangeWatcher_{record.Order}"
    };
}
