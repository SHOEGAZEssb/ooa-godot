using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Placed INTERAC_MISCELLANEOUS_2 $dc:$08 objects. Their nominal Y/X bytes
/// encode a packed room-layout position and a room-flag mask respectively.
/// </summary>
internal sealed class RoomTileChangeWatcherDatabase
{
    internal readonly record struct Record(
        int Group,
        int Room,
        int Order,
        int Position,
        byte RoomFlag,
        string Source);

    private readonly Dictionary<(int Group, int Room), List<Record>> _byRoom = new();

    internal int RecordCount { get; }

    internal RoomTileChangeWatcherDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tile_change_watchers.tsv",
            new GeneratedTableSchema(
                "room tile-change watchers",
                GeneratedTableKeySemantics.Grouped,
                ["group", "room", "order", "position", "room-flag", "source"],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new Record(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                (byte)row.HexByte(4),
                row.RequiredString(5));
            if (record.Group is < 0 or > 7 || record.Room is < 0 or > 0xff ||
                record.Order < 0 || record.RoomFlag == 0)
            {
                throw new InvalidOperationException(
                    $"Invalid tile-change watcher at {record.Source}: " +
                    $"{row.Path}:{row.LineNumber}.");
            }

            var key = (record.Group, record.Room);
            if (!_byRoom.TryGetValue(key, out List<Record>? records))
            {
                records = new List<Record>();
                _byRoom.Add(key, records);
            }
            foreach (Record existing in records)
            {
                if (existing.Order == record.Order)
                {
                    throw new InvalidOperationException(
                        $"Duplicate room-object order {record.Order} for tile-change " +
                        $"watchers in room {record.Group:x1}:{record.Room:x2}.");
                }
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
}
