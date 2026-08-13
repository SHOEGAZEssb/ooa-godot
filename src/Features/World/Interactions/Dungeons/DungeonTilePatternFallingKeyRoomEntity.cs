using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// The shared INTERAC_DUNGEON_EVENTS verifyTilesAndDropSmallKey path. It
/// compares handler-owned metatile/position pairs in source order, then
/// creates TREASURE_SMALL_KEY:$01 at the interaction's exact Y/X.
/// </summary>
internal sealed partial class DungeonTilePatternFallingKeyRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly IReadOnlyList<DungeonTilePatternRecord> _pattern;
    private readonly OracleRoomData _room;
    private readonly GroundTreasureGrantRequest _request;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal IReadOnlyList<DungeonTilePatternRecord> Pattern => _pattern;

    internal DungeonTilePatternFallingKeyRoomEntity(
        DungeonMechanicDatabaseRecord record,
        IReadOnlyList<DungeonTilePatternRecord> pattern,
        OracleRoomData room,
        GroundTreasureGrantRequest request)
    {
        if (record.Id != 0x21 || pattern.Count == 0)
            throw new ArgumentOutOfRangeException(nameof(record));
        foreach (DungeonTilePatternRecord cell in pattern)
        {
            if (cell.Id != record.Id || cell.SubId != record.SubId)
                throw new ArgumentException(
                    "The tile pattern does not belong to the interaction.",
                    nameof(pattern));
        }

        _pattern = pattern;
        _room = room;
        _request = request;
        Name = $"DungeonTilePatternKey_{record.Room:x2}_{record.Order}";
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        foreach (DungeonTilePatternRecord cell in _pattern)
        {
            if (_room.GetMetatile(Point(cell.PackedPosition)) != cell.Tile)
                return;
        }

        spawns.Add(new GroundTreasureGrantSpawn(_request));
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private static Vector2 Point(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
