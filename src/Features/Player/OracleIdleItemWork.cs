using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Ordinary item dispatch work with neither A nor B held.</summary>
internal sealed class OracleIdleItemWork
{
    internal static OracleIdleItemWork Shared { get; } = new();
    private readonly Dictionary<(string Operation, int Index, int Variant), int> _clocks = new();

    private OracleIdleItemWork()
    {
        var table = GeneratedTable.Load("res://assets/oracle/timing/idle_item_cpu.tsv",
            new GeneratedTableSchema("idle item input work", GeneratedTableKeySemantics.Unique,
                ["operation", "index", "variant", "cpu-cycles", "source"],
                ["operation", "index", "variant"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            string operation = row.RequiredString(0);
            if (operation is not ("body" or "item" or "punch"))
                throw row.Invalid(0, "a source item input operation");
            _clocks.Add((operation, row.HexByte(1), row.Decimal(2, 0, 1)), row.Decimal(3, 1, 100_000));
            _ = row.RequiredString(4);
        }
        if (_clocks.Count != 261) throw new InvalidOperationException("Incomplete idle item work table.");
    }

    internal int Get(int equippedA, int equippedB, int ring)
    {
        int Item(int item, int other) => item == 0 && ring is (int)RingId.Experts or (int)RingId.Fist
            ? _clocks[("punch", ring, other == 0 ? 0 : 1)] : _clocks[("item", item, 0)];
        return _clocks[("body", 0, 0)] + Item(equippedA, equippedB) + Item(equippedB, equippedA);
    }
}
