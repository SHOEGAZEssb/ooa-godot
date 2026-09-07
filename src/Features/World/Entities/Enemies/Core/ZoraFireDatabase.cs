namespace oracleofages;

internal sealed class ZoraFireDatabase
{
    internal string Sprite { get; }
    internal int TileBase { get; }
    internal int Palette { get; }
    internal int RadiusY { get; }
    internal int RadiusX { get; }
    internal int Damage { get; }
    internal int Speed { get; }
    internal string Animation { get; }

    internal ZoraFireDatabase()
    {
        GeneratedTable table = GeneratedTable.Load("res://assets/oracle/effects/zora_fire.tsv",
            new GeneratedTableSchema("PART_ZORA_FIRE $19", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "radius-y", "radius-x", "damage-quarters",
                 "speed-raw", "animation", "source"], ["sprite"], headerRequired: true));
        if (table.Rows.Count != 1)
            throw new System.InvalidOperationException("PART_ZORA_FIRE $19 requires one visual/attribute row.");
        GeneratedTableRow row = table.Rows[0];
        Sprite = row.RequiredString(0);
        TileBase = row.UnsignedDecimal(1);
        Palette = row.UnsignedDecimal(2);
        RadiusY = row.UnsignedDecimal(3);
        RadiusX = row.UnsignedDecimal(4);
        Damage = row.UnsignedDecimal(5);
        Speed = row.UnsignedDecimal(6);
        Animation = row.RequiredString(7);
        _ = row.RequiredString(8);
    }
}
