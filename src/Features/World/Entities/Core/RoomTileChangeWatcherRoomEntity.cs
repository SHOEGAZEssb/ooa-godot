using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class RoomTileChangeWatcherRoomEntity
    : RoomEntityAdapter<Node2D>, INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
      IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomTileChangeWatcherDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Vector2 _tilePoint;
    private byte _initialTile;

    internal bool Initialized { get; private set; }
    public bool Finished { get; private set; }
    internal RoomTileChangeWatcherDatabaseRecord Record => _record;
    public bool UpdatesDuringDialogue => !Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized;

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
            Initialize();
            return;
        }

        if (_room.GetMetatile(_tilePoint) == _initialTile)
            return;

        _save.SetRoomFlag(
            _record.Group, _record.Room, _record.RoomFlag);
        Finished = true;
    }

    private void Initialize()
    {
        // State0 checks the flag before capturing the layout byte.
        if (_save.HasRoomFlag(_record.Group, _record.Room, _record.RoomFlag))
        { Finished = true; return; }
        _initialTile = _room.GetMetatile(_tilePoint);
        Initialized = true;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!Initialized && !Finished) Initialize();
        return ScreenTransitionPresentation.Visible;
    }

    private static Node2D CreateNode(
        RoomTileChangeWatcherDatabaseRecord record) => new()
    {
        Name = $"TileChangeWatcher_{record.Order}"
    };
}
