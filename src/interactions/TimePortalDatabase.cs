using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class TimePortalDatabase
{
    public readonly record struct PortalRecord(
        int Group,
        int Room,
        int SubId,
        int Y,
        int X,
        string SpriteName,
        int TileBase,
        int Palette,
        int LoopStart,
        string Animation);

    private readonly Dictionary<int, List<PortalRecord>> _byRoom = new();

    public TimePortalDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/timePortals.tsv");
        int count = 0;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 10)
                throw new InvalidOperationException($"Malformed time-portal data row: {line}");
            var record = new PortalRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                columns[5],
                int.Parse(columns[6]),
                int.Parse(columns[7]),
                int.Parse(columns[8]),
                columns[9]);
            int key = MakeKey(record.Group, record.Room);
            if (!_byRoom.TryGetValue(key, out List<PortalRecord>? records))
            {
                records = new List<PortalRecord>();
                _byRoom.Add(key, records);
            }
            records.Add(record);
            count++;
        }

        if (count != 21)
            throw new InvalidOperationException($"Expected 21 time-portal spawners, loaded {count}.");
    }

    public IReadOnlyList<PortalRecord> GetRoomPortals(int group, int room) =>
        _byRoom.TryGetValue(MakeKey(group, room), out List<PortalRecord>? records)
            ? records
            : Array.Empty<PortalRecord>();

    private static int MakeKey(int group, int room) => (group << 8) | room;
}
