using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OracleTextboxLoadingWork
{
    private readonly Dictionary<(int Id, int Side), (int Cpu, int Vblank, string Status)> _plans = new();
    internal static OracleTextboxLoadingWork Shared { get; } = new();

    private OracleTextboxLoadingWork()
    {
        GeneratedTable table = GeneratedTable.Load("res://assets/oracle/timing/textbox_cpu.tsv",
            new GeneratedTableSchema("textbox initialization CPU work", GeneratedTableKeySemantics.Unique,
                ["text-id", "side", "cpu", "vblank", "status", "source"], ["text-id", "side"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _plans.Add((row.HexWord(0), row.Decimal(1, 0, 2)),
                (row.Decimal(2, 1, 10_000_000), row.Decimal(3, 0, 10_000_000), row.RequiredString(4)));
    }

    internal IReadOnlyList<LoadingStep> Plan(int textId, int side)
    {
        if (!_plans.TryGetValue((textId, side), out var plan))
            throw new InvalidOperationException($"Missing textbox loading work for TX_{textId:x4}, side {side}.");
        if (plan.Status != "standard")
            throw new InvalidOperationException($"TX_{textId:x4} needs dynamic textbox initialization ({plan.Status}).");
        return [new("cpu", plan.Cpu), new("vblank-work", plan.Vblank)];
    }
}
