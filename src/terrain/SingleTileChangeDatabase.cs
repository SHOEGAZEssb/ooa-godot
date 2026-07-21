using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// applySingleTileChanges: ordered, position-specific room-load writes selected
/// by a room-flag mask or one of Ages' $f0-$f2 game-state predicates.
/// </summary>
public sealed class SingleTileChangeDatabase
{
    internal readonly record struct Record(
        int Group,
        int Room,
        byte Mask,
        int Position,
        byte Tile,
        string Source);

    private readonly Dictionary<(int Group, int Room), List<Record>> _byRoom = new();

    internal int RecordCount { get; }

    public SingleTileChangeDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/single_tile_changes.tsv",
            new GeneratedTableSchema(
                "single-tile changes",
                GeneratedTableKeySemantics.Grouped,
                ["group", "room", "mask", "position", "tile", "source"],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new Record(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                (byte)row.HexByte(2),
                row.HexByte(3),
                (byte)row.HexByte(4),
                row.RequiredString(5));
            if (record.Group is < 0 or > 7 || record.Room is < 0 or > 0xff ||
                record.Mask == 0 ||
                (record.Mask > 0x80 && record.Mask is not (0xf0 or 0xf1 or 0xf2)))
            {
                throw new InvalidOperationException(
                    $"Invalid single-tile change at {record.Source}: " +
                    $"{row.Path}:{row.LineNumber}.");
            }

            var key = (record.Group, record.Room);
            if (!_byRoom.TryGetValue(key, out List<Record>? records))
            {
                records = new List<Record>();
                _byRoom.Add(key, records);
            }
            records.Add(record);
            count++;
        }
        RecordCount = count;
    }

    internal IReadOnlyList<Record> GetRoomRecords(int group, int room) =>
        _byRoom.TryGetValue((group, room), out List<Record>? records)
            ? records
            : Array.Empty<Record>();

    internal void Apply(
        int group,
        OracleRoomData room,
        OracleSaveData save,
        long animationTick)
    {
        var writes = new Dictionary<int, byte>();
        foreach (Record record in GetRoomRecords(group, room.Id))
        {
            if (!Matches(record.Mask, group, room.Id, save))
                continue;
            int x = record.Position & 0x0f;
            int y = record.Position >> 4;
            if (x >= room.WidthInTiles || y >= room.HeightInTiles)
            {
                throw new InvalidOperationException(
                    $"{record.Source} writes invalid position ${record.Position:x2} " +
                    $"in room {group:x1}:{room.Id:x2}.");
            }
            writes[record.Position] = record.Tile;
        }

        // loadTilesetAndRoomLayout reloads the source layout on every entry;
        // OracleWorldData caches decoded rooms, so reset before the first
        // substitution pass even when this room has no matching rows.
        room.ResetAndApplyRoomInitializationChanges(writes, animationTick);
    }

    private static bool Matches(
        byte mask,
        int group,
        int room,
        OracleSaveData save) => mask switch
    {
        0xf0 => !save.IsLinkedGame,
        0xf1 => save.IsLinkedGame,
        // The source comment describes this as an unlinked completion row;
        // executed code tests GLOBALFLAG_FINISHEDGAME directly.
        0xf2 => save.HasGlobalFlag(OracleSaveData.GlobalFlagFinishedGame),
        _ => (save.GetRoomFlags(group, room) & mask) != 0
    };
}
