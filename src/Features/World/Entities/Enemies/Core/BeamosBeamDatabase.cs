using System;

namespace oracleofages;

internal sealed class BeamosBeamDatabase
{
    internal string Sprite { get; }
    internal int TileBase { get; }
    internal int Palette { get; }
    internal int RadiusY { get; }
    internal int RadiusX { get; }
    internal int Damage { get; }
    internal string[] Animations { get; } = new string[8];

    internal BeamosBeamDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/effects/beamos_beam.tsv",
            new GeneratedTableSchema("PART_BEAM $29", GeneratedTableKeySemantics.Unique,
                ["animation-index", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                 "damage-quarters", "animation", "source"], ["animation-index"], headerRequired: true));
        if (table.Rows.Count != 8) throw new InvalidOperationException("PART_BEAM $29 requires eight animations.");
        var first = table.Rows[0];
        Sprite = first.RequiredString(1);
        TileBase = first.UnsignedDecimal(2);
        Palette = first.UnsignedDecimal(3);
        RadiusY = first.UnsignedDecimal(4);
        RadiusX = first.UnsignedDecimal(5);
        Damage = first.UnsignedDecimal(6);
        for (int i = 0; i < 8; i++)
        {
            var row = table.Rows[i];
            if (row.UnsignedDecimal(0) != i || row.RequiredString(1) != Sprite ||
                row.UnsignedDecimal(2) != TileBase || row.UnsignedDecimal(3) != Palette ||
                row.UnsignedDecimal(4) != RadiusY || row.UnsignedDecimal(5) != RadiusX ||
                row.UnsignedDecimal(6) != Damage)
                throw row.Invalid(0, "ordered PART_BEAM $29 animation with identical attributes");
            Animations[i] = row.RequiredString(7);
            _ = row.RequiredString(8);
        }
    }
}
