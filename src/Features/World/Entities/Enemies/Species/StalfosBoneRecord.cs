using System;

namespace oracleofages;

internal readonly record struct StalfosBoneRecord(string Sprite, int TileBase, int Palette,
    int RadiusY, int RadiusX, int DamageQuarters, int SpeedRaw, string Animation, string Source)
{
    internal static StalfosBoneRecord Load()
    {
        var table = GeneratedTable.Load("res://assets/oracle/effects/stalfos_bone.tsv",
            new GeneratedTableSchema("Stalfos bone", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "radius-y", "radius-x", "damage-quarters", "speed-raw", "animation", "source"],
                ["source"], headerRequired: true));
        if (table.Rows.Count != 1) throw new InvalidOperationException("Expected one PART_STALFOS_BONE $1c definition.");
        var row = table.Rows[0];
        return new(row.RequiredString(0), row.UnsignedDecimal(1), row.UnsignedDecimal(2),
            row.UnsignedDecimal(3), row.UnsignedDecimal(4), row.UnsignedDecimal(5),
            row.UnsignedDecimal(6), row.RequiredString(7), row.RequiredString(8));
    }
}
