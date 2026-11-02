
namespace Oracles;

internal sealed class AnimatedPartDatabase
{
    public string AssetRoot { get; }
    internal string Sprite { get; }
    internal int TileBase { get; }
    internal int Palette { get; }
    internal int RadiusY { get; }
    internal int RadiusX { get; }
    internal int Damage { get; }
    internal string[] Animations { get; }

    internal static AnimatedPartDatabase Beam(string root) => new(root, "beamos_beam", "PART_BEAM $29", 8);
    internal static AnimatedPartDatabase SpikedBall(string root) => new(root, "spiked_ball", "PART_SPIKED_BALL $2a", 2);

    private AnimatedPartDatabase(string root, string file, string source, int count)
    {
        AssetRoot = root;
        var table = GeneratedTable.Load(root + $"effects/{file}.tsv",
            new GeneratedTableSchema(source, GeneratedTableKeySemantics.Unique,
                ["animation-index", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                 "damage-quarters", "animation", "source"], ["animation-index"], headerRequired: true));
        if (table.Rows.Count != count) throw new InvalidOperationException($"{source} requires {count} animations.");
        var first = table.Rows[0];
        Sprite = first.RequiredString(1);
        TileBase = first.UnsignedDecimal(2);
        Palette = first.UnsignedDecimal(3);
        RadiusY = first.UnsignedDecimal(4);
        RadiusX = first.UnsignedDecimal(5);
        Damage = first.UnsignedDecimal(6);
        Animations = new string[count];
        for (int i = 0; i < count; i++)
        {
            var row = table.Rows[i];
            if (row.UnsignedDecimal(0) != i || row.RequiredString(1) != Sprite ||
                row.UnsignedDecimal(2) != TileBase || row.UnsignedDecimal(3) != Palette ||
                row.UnsignedDecimal(4) != RadiusY || row.UnsignedDecimal(5) != RadiusX ||
                row.UnsignedDecimal(6) != Damage)
                throw row.Invalid(0, $"ordered {source} animation with identical attributes");
            Animations[i] = row.RequiredString(7);
            _ = row.RequiredString(8);
        }
    }
}
