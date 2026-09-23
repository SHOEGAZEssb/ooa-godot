using System;

namespace oracleofages;

internal sealed class SmogFireTimerDatabase
{
    private readonly byte[][] _values = new byte[14][];

    internal SmogFireTimerDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/enemies/smog_fire_timers.tsv",
            new GeneratedTableSchema("smog_setCounterToFireProjectile", GeneratedTableKeySemantics.Unique,
                ["subid", "phase", "random0", "random1", "random2", "random3", "source"],
                ["subid", "phase"], headerRequired: true));
        if (table.Rows.Count != 14) throw new InvalidOperationException("Smog fire timers require two intro and twelve phase rows.");
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            int subid = i < 2 ? i : 2 + (i - 2) / 4;
            int phase = i < 2 ? 0 : (i - 2) % 4;
            if (row.HexByte(0) != subid || row.UnsignedDecimal(1) != phase)
                throw row.Invalid(0, $"ordered Smog subid${subid:x2}, phase{phase}");
            _values[i] = [(byte)row.HexByte(2), (byte)row.HexByte(3), (byte)row.HexByte(4), (byte)row.HexByte(5)];
        }
    }

    internal int Select(int subid, int phase, int random)
    {
        int kind = subid & 15;
        if (subid is < 0 or > 255 || kind > 4 || phase is < 0 or > 3 || (kind < 2 && phase != 0))
            throw new NotSupportedException($"smog_setCounterToFireProjectile subid${subid:x2}, phase${phase:x2}: source table path is not represented.");
        return _values[kind < 2 ? kind : 2 + (kind - 2) * 4 + phase][random & 3];
    }
}
