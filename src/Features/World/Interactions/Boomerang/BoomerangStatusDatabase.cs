using System;

namespace Oracles;

internal sealed class BoomerangStatusDatabase
{
    internal static BoomerangStatusDatabase Shared { get; } = new();
    internal int StunCounter { get; }
    internal int StunInvincibility { get; }
    internal int DeflectionInvincibility { get; }

    private BoomerangStatusDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/common/metadata/boomerang_status.tsv",
            new GeneratedTableSchema("Boomerang damage status", GeneratedTableKeySemantics.Unique,
                ["damage-type", "flags", "invincibility", "knockback", "stun", "source"],
                ["damage-type"], headerRequired: true));
        if (table.Rows.Count != 2 || table.Rows[0].HexByte(0) != 0x24 || table.Rows[1].HexByte(0) != 0x28 ||
            table.Rows[0].HexByte(1) != 0x29 || table.Rows[1].HexByte(1) != 0x60 ||
            table.Rows[0].HexByte(3) != 0 || table.Rows[1].HexByte(3) != 0 || table.Rows[1].HexByte(4) != 0)
            throw new InvalidOperationException("ENEMYDMG_$24/$28 boomerang status contracts changed.");
        StunCounter = table.Rows[0].HexByte(4);
        StunInvincibility = unchecked((sbyte)table.Rows[0].HexByte(2));
        DeflectionInvincibility = unchecked((sbyte)table.Rows[1].HexByte(2));
        foreach (var row in table.Rows) _ = row.RequiredString(5);
    }
}
