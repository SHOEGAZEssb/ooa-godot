using System;

namespace oracleofages;

internal sealed class OraclePegasusWork
{
    internal static OraclePegasusWork Shared { get; } = new();
    private readonly int[,] _clocks = new int[2, 512];

    private OraclePegasusWork()
    {
        var table = GeneratedTable.Load("res://assets/oracle/timing/pegasus_cpu.tsv",
            new GeneratedTableSchema("Pegasus counter work", GeneratedTableKeySemantics.Unique,
                ["ring", "counter", "cpu-cycles", "source"], ["ring", "counter"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            _clocks[row.Decimal(0, 0, 1), row.Decimal(1, 0, 511)] = row.Decimal(2, 1, 100_000);
            _ = row.RequiredString(3);
        }
        foreach (int clocks in _clocks)
            if (clocks == 0) throw new InvalidOperationException("Incomplete decPegasusSeedCounter work table.");
    }

    internal int Get(int rawCounter, bool ring)
    {
        int counter = rawCounter & 0x7fff;
        return _clocks[ring ? 1 : 0, (counter & 255) + (counter > 255 ? 256 : 0)];
    }
}
