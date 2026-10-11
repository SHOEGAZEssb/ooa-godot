using System;

namespace oracleofages;

// bank1.s:initWaveScrollValues_body multiplies bytes, retains the high byte,
// mirrors the first quarter, then reverses/negates the complete first half.
internal sealed class ScreenWaveData
{
    internal static ScreenWaveData Shared { get; } = new();
    private readonly int[] _quarter = new int[32];

    private ScreenWaveData()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/screen_wave_sine.tsv",
            new GeneratedTableSchema("screen wave coefficients", GeneratedTableKeySemantics.Ordered,
                ["order", "coefficient", "source"]));
        if (table.Rows.Count != 32)
            throw new InvalidOperationException("bank1.s:initWaveScrollValues_body@sineWave requires 32 coefficients.");
        for (int index = 0; index < _quarter.Length; index++)
        {
            var row = table.Rows[index];
            if (row.Decimal(0, 0, 31) != index)
                throw row.Invalid(0, "contiguous source wave coefficient order");
            _quarter[index] = row.Decimal(1, 0, 255);
        }
    }

    internal int Offset(int amplitude, int index)
    {
        int phase = index & 0x7f;
        int quarterIndex = phase & 0x1f;
        if ((phase & 0x20) != 0) quarterIndex = 0x1f - quarterIndex;
        int value = (_quarter[quarterIndex] * Math.Clamp(amplitude, 0, 0xff)) >> 8;
        return (phase & 0x40) != 0 ? -value : value;
    }
}
