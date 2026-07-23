using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class SignDatabase
{
    private readonly Dictionary<int, string> _messages = new();

    public SignDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/signs.tsv",
            new GeneratedTableSchema(
                "signs",
                GeneratedTableKeySemantics.Unique,
                ["group", "room", "position", "text-id", "utf8-base64"],
                ["group", "room", "position"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int group = row.Decimal(0, 0, 7);
            int room = row.HexByte(1);
            int position = row.HexByte(2);
            row.HexByte(3);
            int key = MakeKey(group, room, position);
            _messages.Add(key, row.Base64Utf8(4));
        }

        if (_messages.Count != 42)
            throw new InvalidOperationException($"Expected 42 sign records, loaded {_messages.Count}.");
    }

    public bool TryGetMessage(int group, int room, int position, out string message)
    {
        return _messages.TryGetValue(MakeKey(group, room, position), out message!);
    }

    private static int MakeKey(int group, int room, int position)
    {
        return (group << 16) | (room << 8) | position;
    }
}
