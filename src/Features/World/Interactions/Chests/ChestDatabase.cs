using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class ChestDatabase
{

    private readonly Dictionary<int, ChestRecord> _records = new();
    private readonly Lookup<int, ChestRecord> _roomRecords = new();

    public ChestDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/chests.tsv",
            new GeneratedTableSchema(
                "chests",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "position", "treasure-object", "treasure-id",
                    "subid", "parameter", "text-id", "graphic", "amount", "utf8-base64"
                ],
                ["group", "room", "position"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            ChestRecord record = new ChestRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.RequiredString(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                row.HexByte(8),
                row.UnsignedDecimal(9),
                row.Base64Utf8(10));
            _records.Add(MakeKey(record.Group, record.Room, record.Position), record);

            _roomRecords.Add(
                MakeRoomKey(record.Group, record.Room), record);
        }

        if (_records.Count != 133)
            throw new InvalidOperationException($"Expected 133 chest records, loaded {_records.Count}.");
    }

    public bool TryGet(int group, int room, int position, out ChestRecord record) =>
        _records.TryGetValue(MakeKey(group, room, position), out record);

    public IReadOnlyList<ChestRecord> GetRoomRecords(int group, int room) =>
        _roomRecords.ValuesOrEmpty(MakeRoomKey(group, room));

    private static int MakeKey(int group, int room, int position) =>
        (group << 16) | (room << 8) | position;

    private static int MakeRoomKey(int group, int room) => (group << 8) | room;
}

public readonly record struct ChestRecord(int Group, int Room, int Position, string TreasureObject, int TreasureId, int SubId, int Parameter, int TextId, int Graphic, int Amount, string Message);
