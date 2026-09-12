using System;
using System.Linq;
using System.Text;

namespace oracleofages;

internal sealed class FallingBoulderDatabase
{
    internal string Sprite { get; }
    internal int TileBase { get; }
    internal int Palette { get; }
    internal int Radius { get; }
    internal int Damage { get; }
    internal int[] Delays { get; }
    internal string Animation { get; }
    internal bool SourceGrayscaleInverted { get; }

    internal FallingBoulderDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/effects/falling_boulder.tsv",
            new GeneratedTableSchema("PART_FALLING_BOULDER_SPAWNER $45", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "radius", "damage-quarters", "delays", "animation-base64", "source-grayscale-inverted", "source"],
                ["sprite"], headerRequired: true));
        if (table.Rows.Count != 1)
            throw new InvalidOperationException("fallingBoulderSpawner.s: expected one PART $45 definition.");
        var row = table.Rows[0];
        Sprite = row.RequiredString(0);
        TileBase = row.Decimal(1, 0, 255);
        Palette = row.Decimal(2, 0, 7);
        Radius = row.Decimal(3, 1, 30);
        Damage = row.Decimal(4, 1, 255);
        Delays = row.RequiredString(5).Split(',').Select(value =>
            int.TryParse(value, out int delay) && delay is > 0 and <= 255 ? delay :
            throw row.Invalid(5, "four byte appearance delays")).ToArray();
        if (Delays.Length != 4) throw row.Invalid(5, "four PART $45 subid delays");
        Animation = Encoding.UTF8.GetString(Convert.FromBase64String(row.RequiredString(6)));
        SourceGrayscaleInverted = row.Decimal(7, 0, 1) != 0;
        _ = row.RequiredString(8);
    }
}
