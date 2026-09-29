using System;

namespace oracleofages;

/// <summary>Normal grounded Link dispatch, excluding all called handlers.</summary>
internal sealed class OracleLinkStateWork
{
    internal static OracleLinkStateWork Shared { get; } = new();
    private readonly int[] _normal = new int[2];

    private OracleLinkStateWork()
    {
        var table = GeneratedTable.Load("res://assets/oracle/timing/link_state_cpu.tsv",
            new GeneratedTableSchema("normal Link state dispatch work", GeneratedTableKeySemantics.Unique,
                ["moving", "cpu-cycles", "source"], ["moving"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            _normal[row.Decimal(0, 0, 1)] = row.Decimal(1, 1, 100_000);
            _ = row.RequiredString(2);
        }
        if (table.Rows.Count != 2 || _normal[0] == 0 || _normal[1] == 0)
            throw new InvalidOperationException("Incomplete normal Link dispatch work.");
    }

    internal int Normal(bool moving) => _normal[moving ? 1 : 0];
}
