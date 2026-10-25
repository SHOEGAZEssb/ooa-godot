using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Oracles;

internal sealed class BlueStalfosProjectileDatabase
{
    private static readonly Lazy<BlueStalfosProjectileDatabase> LazyShared = new(() => new());
    internal static BlueStalfosProjectileDatabase Shared => LazyShared.Value;
    internal EnemyProjectileVisualRecord Visual { get; }
    internal IReadOnlyList<int> Properties { get; }
    private readonly bool[] _active = new bool[32];
    internal bool Active(int item) => _active[item];
    private BlueStalfosProjectileDatabase()
    {
        var table = GeneratedTable.Load(AgesGameContent.AssetRoot + "effects/blue_stalfos_parts.tsv",
            new GeneratedTableSchema("Blue Stalfos projectile", GeneratedTableKeySemantics.Unique,
                ["id", "animation-index", "sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "part-data", "source"],
                ["id", "animation-index"], headerRequired: true));
        var rows = table.Rows.ToArray();
        if (rows.Length != 5 || rows.Where((row, index) => row.HexByte(0) != 0x3d || row.UnsignedDecimal(1) != index).Any())
            throw new InvalidOperationException("blueStalfosProjectile.s: PART$3d requires five ordered animations.");
        var first = rows[0];
        Visual = new([first.RequiredString(2)], first.UnsignedDecimal(3), first.UnsignedDecimal(4), first.Boolean01(5), rows.Select(row => row.RequiredString(6)).ToArray());
        Properties = first.SplitRequired(7, ',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        if (Properties.Count != 8) throw first.Invalid(7, "eight native PART$3d property bytes");
        foreach (var row in rows) _ = row.RequiredString(8);
        var collisions = GeneratedTable.Load(AgesGameContent.AssetRoot + "effects/blue_stalfos_part_collisions.tsv",
            new GeneratedTableSchema("Blue Stalfos projectile collisions", GeneratedTableKeySemantics.Unique,
                ["id", "collision", "enabled", "effect", "source"], ["id", "collision"], headerRequired: true));
        if (collisions.Rows.Count != 32) throw new InvalidOperationException("PART$3d requires 32 collision columns.");
        foreach (var row in collisions.Rows)
        {
            if (row.HexByte(0) != 0x3d) throw row.Invalid(0, "PART$3d");
            _active[row.HexByte(1)] = row.Boolean01(2); _ = row.HexByte(3); _ = row.RequiredString(4);
        }
    }
}
