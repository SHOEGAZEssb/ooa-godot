using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KeyholeControllerDatabase
{
    private readonly Dictionary<(int Group, int Room), KeyholeControllerRecord> _records = new();

    internal KeyholeControllerDatabase()
    {
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/keyhole_controllers.tsv",
            new GeneratedTableSchema("placed keyhole controllers", GeneratedTableKeySemantics.Unique,
                ["group", "room", "order", "id", "subid", "x", "y", "source"],
                ["group", "room"], headerRequired: true)).Rows)
        {
            int id = row.HexByte(3), subid = row.HexByte(4);
            if (!(id == 0x90 && subid is 0x11 or 0x12 or 0x13) && !(id == 0xdc && subid == 1))
                throw row.Invalid(3, "implemented keyhole controller $90:$11/$12/$13 or $dc:$01");
            var record = new KeyholeControllerRecord(row.Decimal(0, 0, 7), row.HexByte(1),
                row.UnsignedDecimal(2), id, subid, new Vector2(row.HexByte(5), row.HexByte(6)), row.RequiredString(7));
            _records.Add((record.Group, record.Room), record);
        }
        if (_records.Count != 5)
            throw new InvalidOperationException("miscPuzzles.s;miscellaneous2.s: expected five placed keyhole controllers.");
    }

    internal bool TryGet(int group, int room, out KeyholeControllerRecord record) =>
        _records.TryGetValue((group, room), out record);
}

internal readonly record struct KeyholeControllerRecord(int Group, int Room, int Order, int Id, int SubId, Vector2 Position, string Source);
