using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Shared dispatcher work, excluding individual object handlers.</summary>
internal sealed class OracleGameplayDispatchWork
{
    internal static OracleGameplayDispatchWork Shared { get; } = new();
    private readonly Dictionary<(string Operation, int Variant), int> _clocks = new();

    private OracleGameplayDispatchWork()
    {
        GeneratedTable table = GeneratedTable.Load("res://assets/oracle/timing/gameplay_dispatch_cpu.tsv",
            new GeneratedTableSchema("original gameplay dispatcher work", GeneratedTableKeySemantics.Unique,
                ["operation", "variant", "cpu-cycles", "source"],
                ["operation", "variant"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string operation = row.RequiredString(0);
            if (operation is not ("objects" or "sprites" or "enemies" or "parts" or "interactions" or "items" or "items-post"))
                throw new InvalidOperationException($"Unsupported gameplay dispatcher {operation}.");
            _clocks.Add((operation, row.Decimal(1, 0, 5)), row.Decimal(2, 1, 1_000_000));
            _ = row.RequiredString(3);
        }
    }

    internal int Objects => _clocks[("objects", 0)];
    internal int NormalItems => _clocks[("items", 0)];
    internal int PostItems => _clocks[("items-post", 0)];
    internal int NormalObjectPass(int phase) => _clocks[(phase switch {
        0 => "enemies", 1 => "parts", 2 => "interactions",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)) }, 0)];
    internal int Sprites(bool scrolling, bool largeRoom, bool linkOnly) =>
        _clocks[("sprites", (scrolling ? largeRoom ? 4 : 2 : 0) + (linkOnly ? 1 : 0))];
}
