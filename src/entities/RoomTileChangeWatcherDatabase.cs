using Godot;
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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/tile_change_watchers.tsv");
        int count = 0;
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 6 || string.IsNullOrWhiteSpace(columns[5]))
                throw new InvalidOperationException(
                    $"Malformed tile-change watcher row: {line}");

            var record = new Record(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                int.Parse(columns[2]),
                Convert.ToInt32(columns[3], 16),
                Convert.ToByte(columns[4], 16),
                columns[5]);
            if (record.Group is < 0 or > 7 || record.Room is < 0 or > 0xff ||
                record.Order < 0 || record.RoomFlag == 0)
            {
                throw new InvalidOperationException(
                    $"Invalid tile-change watcher at {record.Source}: {line}");
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
