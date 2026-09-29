using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Executed source work of the global pirate course, excluding ship allocation.</summary>
internal sealed class OraclePirateCourseWork
{
    internal static OraclePirateCourseWork Shared { get; } = new();
    private readonly Dictionary<(string Operation, int Index, int Variant), int> _clocks = new();
    private readonly HashSet<int>[] _tiles = [[], []];

    private OraclePirateCourseWork()
    {
        var table = GeneratedTable.Load("res://assets/oracle/timing/pirate_course_cpu.tsv",
            new GeneratedTableSchema("pirate course work", GeneratedTableKeySemantics.Unique,
                ["operation", "index", "variant", "cpu-cycles", "source"],
                ["operation", "index", "variant"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            string operation = row.RequiredString(0);
            if (operation is not ("dispatch" or "tile" or "angle" or "position" or "room"))
                throw row.Invalid(0, "a source pirate course operation");
            int index = row.HexByte(1), variant = row.Decimal(2, 0, 511);
            _clocks.Add((operation, index, variant), row.Decimal(3, 1, 100_000));
            if (operation == "angle") _tiles[variant >> 8].Add(variant & 255);
            _ = row.RequiredString(4);
        }
    }

    internal int Get(string operation, int index = 0, int variant = 0) =>
        _clocks.TryGetValue((operation, index, variant), out int clocks) ? clocks :
        throw new InvalidOperationException($"Missing pirate course work {operation}/${index:x2}/{variant}.");

    internal int Angle(bool linked, byte room, byte tile) =>
        Get("angle", room, (linked ? 256 : 0) + (_tiles[linked ? 1 : 0].Contains(tile) ? tile : 255));
}
