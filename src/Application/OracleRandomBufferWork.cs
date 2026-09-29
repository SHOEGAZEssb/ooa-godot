using System;

namespace oracleofages;

/// <summary>Blocking work of the original shared placement-buffer shuffle.</summary>
internal sealed class OracleRandomBufferWork
{
    internal static OracleRandomBufferWork Shared { get; } = new();
    internal int BaseClocks { get; }
    private readonly int[] _multiplyExtra = new int[256];

    private OracleRandomBufferWork()
    {
        GeneratedTable table = GeneratedTable.Load("res://assets/oracle/timing/random_buffer_cpu.tsv",
            new GeneratedTableSchema("original random buffer CPU work", GeneratedTableKeySemantics.Unique,
                ["component", "cpu-cycles", "source"], ["component"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int clocks = row.Decimal(1, 0, 1_000_000);
            if (row.RequiredString(0) == "base") BaseClocks = clocks;
            else _multiplyExtra[row.HexByte(0)] = clocks;
            _ = row.RequiredString(2);
        }
        if (BaseClocks == 0 || table.Rows.Count != 257)
            throw new InvalidOperationException("generateRandomBuffer work needs its base and all 256 multiply branches.");
    }

    internal int MultiplyExtra(byte value) => _multiplyExtra[value];
}
