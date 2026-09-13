using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Ordered Ages staticDungeonObjects lists, loaded on dungeon entry.</summary>
internal sealed class StaticDungeonObjectDatabase
{
    private readonly List<MinecartStaticRecord>[] _minecarts = new List<MinecartStaticRecord>[16];

    internal StaticDungeonObjectDatabase()
    {
        for (int dungeon = 0; dungeon < _minecarts.Length; dungeon++) _minecarts[dungeon] = new();
        var table = GeneratedTable.Load("res://assets/oracle/objects/dungeon_static_minecarts.tsv",
            new GeneratedTableSchema("dungeon static minecarts", GeneratedTableKeySemantics.Unique,
                ["dungeon", "slot", "room", "y", "x", "source"], ["dungeon", "slot"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int dungeon = row.HexByte(0);
            if (dungeon >= _minecarts.Length) throw row.Invalid(0, "dungeon $00-$0f");
            var records = _minecarts[dungeon];
            if (row.UnsignedDecimal(1) != records.Count || records.Count == 8)
                throw row.Invalid(1, "ordered static slots $00-$07");
            records.Add(new MinecartStaticRecord(records.Count, row.HexByte(2), row.HexByte(3),
                row.HexByte(4), row.RequiredString(5)));
        }
        int[] counts = [0, 0, 3, 0, 4, 0, 0, 0, 2, 0, 0, 1, 0, 0, 0, 0];
        for (int dungeon = 0; dungeon < counts.Length; dungeon++)
            if (_minecarts[dungeon].Count != counts[dungeon])
                throw new InvalidOperationException($"Incomplete staticDungeonObjects for dungeon ${dungeon:x2}.");
    }

    internal IReadOnlyList<MinecartStaticRecord> Minecarts(int dungeon) =>
        dungeon is >= 0 and < 16 ? _minecarts[dungeon]
            : throw new InvalidOperationException($"No staticDungeonObjects table for dungeon ${dungeon:x2}.");
}
