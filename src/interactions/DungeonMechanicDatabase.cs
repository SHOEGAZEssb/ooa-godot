using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported placements and common constants for PART_BUTTON $09, its
/// $20:$00/$21:$17 trigger-chest consumers, INTERAC_PUSHBLOCK_TRIGGER
/// $13:$01, and shutter-door controller variants $1e:$04-$0b.
/// </summary>
internal sealed class DungeonMechanicDatabase
{
    internal enum TriggerPredicate
    {
        None,
        BitSet,
        Exact
    }

    internal readonly record struct Record(
        int Group,
        int Room,
        int Order,
        int Id,
        int SubId,
        int PackedPosition,
        int Parameter,
        TriggerPredicate Predicate,
        bool CountSourceComplete);

    private readonly Dictionary<int, List<Record>> _recordsByRoom = new();
    private readonly Dictionary<string, int> _constants = new();

    internal int RecordCount { get; }
    internal int PushableBlock => Constant("pushable-block");
    internal int PushDelay => Constant("push-delay");
    internal int SolveWait => Constant("solve-wait");
    internal int DoorFrameWait => Constant("door-frame-wait");
    internal int OpenTile => Constant("open-tile");
    internal int SolveSound => Constant("solve-sound");
    internal int DoorSound => Constant("door-sound");
    internal int ButtonTile => Constant("button-tile");
    internal int PressedButtonTile => Constant("pressed-button-tile");
    internal int ButtonRadiusY => Constant("button-radius-y");
    internal int ButtonRadiusX => Constant("button-radius-x");
    internal int ButtonObjectReleaseDelay => Constant("button-object-release-delay");
    internal int ButtonSound => Constant("button-sound");
    internal int ChestTile => Constant("chest-tile");
    internal int ChestWait => Constant("chest-wait");
    internal int PuffSound => Constant("puff-sound");

    public DungeonMechanicDatabase()
    {
        int count = 0;
        foreach (string line in DataLines("dungeon_mechanics.tsv"))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 9)
                throw Malformed("placement", line);
            var record = new Record(
                int.Parse(fields[0]),
                Convert.ToInt32(fields[1], 16),
                int.Parse(fields[2]),
                Convert.ToInt32(fields[3], 16),
                Convert.ToInt32(fields[4], 16),
                Convert.ToInt32(fields[5], 16),
                Convert.ToInt32(fields[6], 16),
                fields[7] switch
                {
                    "none" => TriggerPredicate.None,
                    "bit" => TriggerPredicate.BitSet,
                    "exact" => TriggerPredicate.Exact,
                    _ => throw Malformed("trigger-predicate", line)
                },
                fields[8] switch
                {
                    "0" => false,
                    "1" => true,
                    _ => throw Malformed("count-source", line)
                });
            if (record.Id is not (0x09 or 0x13 or 0x1e or 0x20 or 0x21) ||
                record.Id == 0x20 && record.SubId != 0x00 ||
                record.Id == 0x21 && record.SubId != 0x17)
                throw Malformed("interaction", line);
            int key = MakeKey(record.Group, record.Room);
            if (!_recordsByRoom.TryGetValue(key, out List<Record>? records))
            {
                records = new List<Record>();
                _recordsByRoom.Add(key, records);
            }
            if (records.Count > 0 && records[^1].Order >= record.Order)
            {
                throw new InvalidOperationException(
                    $"Room {record.Group:x1}:{record.Room:x2} dungeon interaction order " +
                    $"did not increase at source object {record.Order}.");
            }
            records.Add(record);
            count++;
        }
        RecordCount = count;

        foreach (string line in DataLines("dungeon_mechanic_constants.tsv"))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 2)
                throw Malformed("constant", line);
            _constants.Add(fields[0], int.Parse(fields[1]));
        }

        IReadOnlyList<Record> room0c = GetRoomRecords(4, 0x0c);
        IReadOnlyList<Record> room0b = GetRoomRecords(4, 0x0b);
        IReadOnlyList<Record> room08 = GetRoomRecords(4, 0x08);
        IReadOnlyList<Record> room09 = GetRoomRecords(4, 0x09);
        IReadOnlyList<Record> room22 = GetRoomRecords(4, 0x22);
        IReadOnlyList<Record> room7a = GetRoomRecords(4, 0x7a);
        if (RecordCount != 155 || _constants.Count != 20 ||
            room08.Count != 2 ||
            room08[0] != new Record(
                4, 0x08, 0, 0x20, 0x00, 0x57, 0x01,
                TriggerPredicate.Exact, true) ||
            room08[1] != new Record(
                4, 0x08, 1, 0x09, 0x00, 0x17, 0x00,
                TriggerPredicate.None, true) ||
            room09.Count != 4 ||
            room09[0] != new Record(
                4, 0x09, 0, 0x1e, 0x04, 0x07, 0x00,
                TriggerPredicate.BitSet, true) ||
            room09[1] != new Record(
                4, 0x09, 1, 0x1e, 0x05, 0x5e, 0x00,
                TriggerPredicate.BitSet, true) ||
            room09[2] != new Record(
                4, 0x09, 3, 0x13, 0x01, 0x2a, 0x00,
                TriggerPredicate.None, true) ||
            room09[3] != new Record(
                4, 0x09, 5, 0x09, 0x00, 0x14, 0x00,
                TriggerPredicate.None, true) ||
            room22.Count != 2 || room22[1] !=
                new Record(
                    4, 0x22, 1, 0x09, 0x80, 0x5b, 0x00,
                    TriggerPredicate.None, true) ||
            room7a.Count != 2 || room7a[0] !=
                new Record(
                    4, 0x7a, 0, 0x21, 0x17, 0x39, 0x01,
                    TriggerPredicate.Exact, true) ||
            room0c.Count != 2 ||
            room0c[0] != new Record(
                4, 0x0c, 0, 0x13, 0x01, 0x47, 0x00,
                TriggerPredicate.None, true) ||
            room0c[1] != new Record(
                4, 0x0c, 1, 0x1e, 0x08, 0x07, 0x00,
                TriggerPredicate.None, true) ||
            room0b.Count != 2 || room0b[0].SubId != 0x08 || room0b[1].SubId != 0x0b ||
            PushableBlock != 0x1d || PushDelay != 30 || SolveWait != 8 ||
            DoorFrameWait != 6 || OpenTile != 0xa0 ||
            ClosedTile(0x08) != 0x78 || ClosedTile(0x09) != 0x79 ||
            ClosedTile(0x0a) != 0x7a || ClosedTile(0x0b) != 0x7b ||
            ClosedTile(0x04) != 0x78 || ClosedTile(0x07) != 0x7b ||
            SolveSound != 0x4d || DoorSound != 0x70 ||
            ButtonTile != 0x0c || PressedButtonTile != 0x0d ||
            ButtonRadiusY != 2 || ButtonRadiusX != 2 ||
            ButtonObjectReleaseDelay != 0x1c || ButtonSound != 0x87 ||
            ChestTile != 0xf1 || ChestWait != 15 || PuffSound != 0x98)
        {
            throw new InvalidOperationException(
                "Imported dungeon button / trigger-chest / $13:$01 / " +
                "$1e:$04-$0b contract is incomplete.");
        }
    }

    internal IReadOnlyList<Record> GetRoomRecords(int group, int room) =>
        _recordsByRoom.TryGetValue(MakeKey(group, room), out List<Record>? records)
            ? records
            : Array.Empty<Record>();

    internal int ClosedTile(int subId) => subId switch
    {
        0x04 or 0x08 => Constant("closed-up"),
        0x05 or 0x09 => Constant("closed-right"),
        0x06 or 0x0a => Constant("closed-down"),
        0x07 or 0x0b => Constant("closed-left"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(subId), $"Unsupported shutter subid ${subId:x2}.")
    };

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Dungeon mechanic constant '{key}' was not imported.");

    private static int MakeKey(int group, int room) => (group << 8) | room;

    private static IEnumerable<string> DataLines(string file)
    {
        string source = FileAccess.GetFileAsString(
            $"res://assets/oracle/objects/{file}");
        foreach (string raw in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith('#'))
                yield return line;
        }
    }

    private static InvalidOperationException Malformed(string kind, string line) =>
        new($"Malformed dungeon mechanic {kind} row: {line}");
}
