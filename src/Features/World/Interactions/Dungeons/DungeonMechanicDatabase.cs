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

    private readonly Dictionary<int, List<DungeonMechanicDatabaseRecord>> _recordsByRoom = new();
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
        GeneratedTable mechanics = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_mechanics.tsv",
            new GeneratedTableSchema(
                "dungeon mechanics",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "id", "subid", "position", "parameter",
                    "trigger-predicate", "count-source-complete"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in mechanics.Rows)
        {
            DungeonMechanicDatabaseRecord record = new DungeonMechanicDatabaseRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.RequiredString(7) switch
                {
                    "none" => TriggerPredicate.None,
                    "bit" => TriggerPredicate.BitSet,
                    "exact" => TriggerPredicate.Exact,
                    _ => throw row.Invalid(7, "one of none, bit, exact")
                },
                row.Boolean01(8));
            if (record.Id is not (0x09 or 0x13 or 0x1e or 0x20 or 0x21) ||
                record.Id == 0x20 && record.SubId != 0x00 ||
                record.Id == 0x21 && record.SubId != 0x17)
                throw row.Invalid(3, "a supported dungeon mechanic interaction id");
            int key = MakeKey(record.Group, record.Room);
            if (!_recordsByRoom.TryGetValue(key, out List<DungeonMechanicDatabaseRecord>? records))
            {
                records = new List<DungeonMechanicDatabaseRecord>();
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

        GeneratedTable constants = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_mechanic_constants.tsv",
            new GeneratedTableSchema(
                "dungeon mechanic constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constants.Rows)
        {
            _constants.Add(row.RequiredString(0), row.Decimal(1));
        }

        IReadOnlyList<DungeonMechanicDatabaseRecord> room0c = GetRoomRecords(4, 0x0c);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room0b = GetRoomRecords(4, 0x0b);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room08 = GetRoomRecords(4, 0x08);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room09 = GetRoomRecords(4, 0x09);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room22 = GetRoomRecords(4, 0x22);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room7a = GetRoomRecords(4, 0x7a);
        if (RecordCount != 155 || _constants.Count != 20 ||
            room08.Count != 2 ||
            room08[0] != new DungeonMechanicDatabaseRecord(
                4, 0x08, 0, 0x20, 0x00, 0x57, 0x01,
                TriggerPredicate.Exact, true) ||
            room08[1] != new DungeonMechanicDatabaseRecord(
                4, 0x08, 1, 0x09, 0x00, 0x17, 0x00,
                TriggerPredicate.None, true) ||
            room09.Count != 4 ||
            room09[0] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 0, 0x1e, 0x04, 0x07, 0x00,
                TriggerPredicate.BitSet, true) ||
            room09[1] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 1, 0x1e, 0x05, 0x5e, 0x00,
                TriggerPredicate.BitSet, true) ||
            room09[2] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 3, 0x13, 0x01, 0x2a, 0x00,
                TriggerPredicate.None, true) ||
            room09[3] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 5, 0x09, 0x00, 0x14, 0x00,
                TriggerPredicate.None, true) ||
            room22.Count != 2 || room22[1] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x22, 1, 0x09, 0x80, 0x5b, 0x00,
                    TriggerPredicate.None, true) ||
            room7a.Count != 2 || room7a[0] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x7a, 0, 0x21, 0x17, 0x39, 0x01,
                    TriggerPredicate.Exact, true) ||
            room0c.Count != 2 ||
            room0c[0] != new DungeonMechanicDatabaseRecord(
                4, 0x0c, 0, 0x13, 0x01, 0x47, 0x00,
                TriggerPredicate.None, true) ||
            room0c[1] != new DungeonMechanicDatabaseRecord(
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

    internal IReadOnlyList<DungeonMechanicDatabaseRecord> GetRoomRecords(int group, int room) =>
        _recordsByRoom.TryGetValue(MakeKey(group, room), out List<DungeonMechanicDatabaseRecord>? records)
            ? records
            : Array.Empty<DungeonMechanicDatabaseRecord>();

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

}
