using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

public sealed class ChestDatabase
{
    public readonly record struct ChestRecord(
        int Group,
        int Room,
        int Position,
        string TreasureObject,
        int TreasureId,
        int SubId,
        int Parameter,
        int TextId,
        int Graphic,
        int Amount,
        string Message);

    private readonly Dictionary<int, ChestRecord> _records = new();
    private readonly Dictionary<int, List<ChestRecord>> _roomRecords = new();

    public ChestDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/chests.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 11)
                throw new InvalidOperationException($"Malformed chest data row: {line}");

            var record = new ChestRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                columns[3],
                Convert.ToInt32(columns[4], 16),
                Convert.ToInt32(columns[5], 16),
                Convert.ToInt32(columns[6], 16),
                Convert.ToInt32(columns[7], 16),
                Convert.ToInt32(columns[8], 16),
                int.Parse(columns[9]),
                Encoding.UTF8.GetString(Convert.FromBase64String(columns[10])));
            _records.Add(MakeKey(record.Group, record.Room, record.Position), record);

            int roomKey = MakeRoomKey(record.Group, record.Room);
            if (!_roomRecords.TryGetValue(roomKey, out List<ChestRecord>? records))
            {
                records = new List<ChestRecord>();
                _roomRecords.Add(roomKey, records);
            }
            records.Add(record);
        }

        if (_records.Count != 133)
            throw new InvalidOperationException($"Expected 133 chest records, loaded {_records.Count}.");
    }

    public bool TryGet(int group, int room, int position, out ChestRecord record) =>
        _records.TryGetValue(MakeKey(group, room, position), out record);

    public IReadOnlyList<ChestRecord> GetRoomRecords(int group, int room) =>
        _roomRecords.TryGetValue(MakeRoomKey(group, room), out List<ChestRecord>? records)
            ? records
            : Array.Empty<ChestRecord>();

    private static int MakeKey(int group, int room, int position) =>
        (group << 16) | (room << 8) | position;

    private static int MakeRoomKey(int group, int room) => (group << 8) | room;
}
