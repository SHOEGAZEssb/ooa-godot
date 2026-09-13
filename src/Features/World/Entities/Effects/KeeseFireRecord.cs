using System;

namespace oracleofages;

internal readonly record struct KeeseFireRecord(string Sprite, int TileBase, int Palette, int Frames,
    int Radius, int Damage, int LinkInvincibility, int LinkKnockback, int ShieldInvincibility,
    int ShieldKnockback, int ShieldSound, string Animation)
{
    private static readonly Lazy<KeeseFireRecord> Record = new(Load);
    internal static KeeseFireRecord Shared => Record.Value;
    private static KeeseFireRecord Load()
    {
        var table = GeneratedTable.Load("res://assets/oracle/effects/keese_fire.tsv",
            new GeneratedTableSchema("PART_FIRE $20", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "frames", "radius", "damage-quarters", "link-invincibility", "link-knockback",
                 "shield-invincibility", "shield-knockback", "shield-sound", "animation", "source"], ["source"], headerRequired: true));
        if (table.Rows.Count != 1) throw new InvalidOperationException("Expected one PART_FIRE $20 definition.");
        var r = table.Rows[0];
        return new(r.RequiredString(0), r.UnsignedDecimal(1), r.UnsignedDecimal(2), r.UnsignedDecimal(3),
            r.UnsignedDecimal(4), r.UnsignedDecimal(5), r.UnsignedDecimal(6), r.UnsignedDecimal(7),
            r.UnsignedDecimal(8), r.UnsignedDecimal(9), r.UnsignedDecimal(10), r.RequiredString(11));
    }
}
