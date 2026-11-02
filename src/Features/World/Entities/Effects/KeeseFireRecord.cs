
namespace Oracles;

internal readonly record struct KeeseFireRecord(string AssetRoot, string Sprite, int TileBase, int Palette, int Frames,
    int Radius, int Damage, int LinkInvincibility, int LinkKnockback, int ShieldInvincibility,
    int ShieldKnockback, int ShieldSound, string Animation)
{
    internal static KeeseFireRecord Load(string assetRoot)
    {
        var table = GeneratedTable.Load(assetRoot + "effects/keese_fire.tsv",
            new GeneratedTableSchema("PART_FIRE $20", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "frames", "radius", "damage-quarters", "link-invincibility", "link-knockback",
                 "shield-invincibility", "shield-knockback", "shield-sound", "animation", "source"], ["source"], headerRequired: true));
        GeneratedTableRow r = table.SingleRow();
        return new(assetRoot, r.RequiredString(0), r.UnsignedDecimal(1), r.UnsignedDecimal(2), r.UnsignedDecimal(3),
            r.UnsignedDecimal(4), r.UnsignedDecimal(5), r.UnsignedDecimal(6), r.UnsignedDecimal(7),
            r.UnsignedDecimal(8), r.UnsignedDecimal(9), r.UnsignedDecimal(10), r.RequiredString(11));
    }
}
