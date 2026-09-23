using System;

namespace oracleofages;

internal sealed class SomariaLifecycleDatabase
{
    internal int PhaseSound { get; }
    internal int MoveSound { get; }
    internal int NormalSpeed { get; }
    internal int NormalFrames { get; }
    internal int GloveSpeed { get; }
    internal int GloveFrames { get; }

    internal SomariaLifecycleDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/somaria_lifecycle.tsv",
            new GeneratedTableSchema("Somaria lifecycle", GeneratedTableKeySemantics.Unique,
                ["phase-sound", "move-sound", "normal-speed", "normal-frames", "glove-speed", "glove-frames", "source"],
                ["phase-sound"], headerRequired: true));
        if (table.Rows.Count != 1) throw new InvalidOperationException("Somaria requires one itemCode18 lifecycle profile.");
        var row = table.Rows[0];
        PhaseSound = row.HexByte(0); MoveSound = row.HexByte(1);
        NormalSpeed = row.HexByte(2); NormalFrames = row.Decimal(3, 1, 255);
        GloveSpeed = row.HexByte(4); GloveFrames = row.Decimal(5, 1, 255);
        _ = row.RequiredString(6);
    }
}
