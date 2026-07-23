using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// applyStandardTileSubstitutions: room-flag bits 0-3 and 7 select an imported
/// replacement list by wActiveCollisions whenever a room is loaded.
/// </summary>
public sealed class StandardTileSubstitutionDatabase
{
    private readonly Dictionary<(int Flag, int Collisions), Dictionary<byte, byte>>
        _substitutions = new();

    internal int RecordCount { get; }

    public StandardTileSubstitutionDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/standard_tile_substitutions.tsv",
            new GeneratedTableSchema(
                "standard tile substitutions",
                GeneratedTableKeySemantics.Grouped,
                ["room-flag", "active-collisions", "replacement", "original", "source"],
                ["room-flag", "active-collisions"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int flag = row.HexByte(0);
            int collisions = row.Decimal(1, 0, 0xff);
            byte replacement = (byte)row.HexByte(2);
            byte original = (byte)row.HexByte(3);
            row.RequiredString(4);
            var key = (flag, collisions);
            if (!_substitutions.TryGetValue(key, out Dictionary<byte, byte>? records))
            {
                records = new Dictionary<byte, byte>();
                _substitutions.Add(key, records);
            }
            if (!records.TryAdd(original, replacement))
            {
                throw new InvalidOperationException(
                    $"Duplicate standard tile substitution ${flag:x2}/" +
                    $"${collisions:x2}:${original:x2}.");
            }
            count++;
        }
        RecordCount = count;
    }

    internal void Apply(OracleRoomData room, byte roomFlags, long animationTick)
    {
        foreach (int flag in new[] { 0x01, 0x02, 0x04, 0x08, 0x80 })
        {
            if ((roomFlags & flag) == 0 ||
                !_substitutions.TryGetValue(
                    (flag, room.ActiveCollisions),
                    out Dictionary<byte, byte>? records))
            {
                continue;
            }
            room.ApplyMetatileSubstitutions(records, animationTick);
        }
    }
}
