using System;

namespace oracleofages;

internal sealed class PartSwitchCollisionDatabase
{
    internal static PartSwitchCollisionDatabase Shared { get; } = new();
    private readonly int[] _lockouts = new int[32];

    private PartSwitchCollisionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/part_switch_collisions.tsv",
            new GeneratedTableSchema("PART_SWITCH collision responses", GeneratedTableKeySemantics.Unique,
                ["item", "enabled", "effect", "lockout", "source"], ["item"], headerRequired: true));
        if (table.Rows.Count != 32) throw new InvalidOperationException("Incomplete PART_SWITCH collision row $03.");
        for (int index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0, "ordered collision indices $00-$1f");
            int enabled = row.Decimal(1, 0, 1), effect = row.HexByte(2), lockout = row.HexByte(3);
            if (enabled != 0 && (effect is not (0x20 or 0x26) || lockout != (effect == 0x26 ? 28 : 0)))
                throw row.Invalid(2, "PART_SWITCH effects $20/$26 and their source lockouts");
            _lockouts[index] = enabled != 0 ? lockout : -1;
        }
    }

    internal int HitLockout(int itemCollision) => itemCollision is >= 0 and < 32 ? _lockouts[itemCollision] : -1;
}
