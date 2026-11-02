
namespace Oracles;

internal sealed class CompanionTerrainDatabase
{
    public string AssetRoot { get; }

    private readonly Dictionary<string, List<Vector2>> _probes = new();
    internal int[] CollisionMasks { get; } = new int[16];
    internal int VineTop { get; }
    internal int VineMiddle { get; }
    internal int VineBottom { get; }
    private int VineTopCollision { get; }
    internal IReadOnlyList<Vector2> Probes(string kind) => _probes[kind];

    internal bool IsSolid(OracleRoomData room, Vector2 sample, bool swimming = false)
    {
        if (sample.X < 0 || sample.Y < 0 || sample.X >= room.Width || sample.Y >= room.Height) return false;
        int tile = room.GetMetatile(sample);
        if (swimming && tile >= 0xfe) return false;
        if (tile == VineMiddle || tile == VineBottom) return true;
        int collision = tile == VineTop ? VineTopCollision : room.GetTerrainInfo(sample).Collision;
        int x = Mathf.FloorToInt(sample.X) & 15, y = Mathf.FloorToInt(sample.Y) & 15;
        if (collision < 0x10) return (collision & (1 << ((y < 8 ? 2 : 0) + (x < 8 ? 1 : 0)))) != 0;
        int kind = collision & 15;
        return (CollisionMasks[kind] & (1 << ((kind < 8 ? x : y) >> 1))) != 0;
    }

    internal CompanionTerrainDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        var tiles = GeneratedTable.Load(AssetRoot + "metadata/companion_tiles.tsv",
            new("Companion native vine tiles", GeneratedTableKeySemantics.Ordered,
                ["vine-top", "vine-middle", "vine-bottom", "vine-top-collision", "source"], headerRequired: true)).SingleRow();
        VineTop = tiles.HexByte(0); VineMiddle = tiles.HexByte(1); VineBottom = tiles.HexByte(2);
        VineTopCollision = tiles.HexByte(3); _ = tiles.RequiredString(4);

        foreach (var row in GeneratedTable.Load(AssetRoot + "metadata/companion_terrain_probes.tsv",
            new GeneratedTableSchema("Companion native probes", GeneratedTableKeySemantics.Ordered,
                ["kind", "index", "x", "y", "source"], headerRequired: true)).Rows)
        {
            string kind = row.RequiredString(0);
            if (!_probes.TryGetValue(kind, out var probes)) _probes.Add(kind, probes = new());
            if (row.UnsignedDecimal(1) != probes.Count) throw row.Invalid(1, "source probe order");
            probes.Add(new(row.Decimal(2), row.Decimal(3)));
        }
        if (_probes.Count != 3 || Probes("carry").Count != 4 || Probes("cliff").Count != 4 || Probes("collision").Count != 8)
            throw new InvalidOperationException("Dimitri/commonCode.s native probe shape changed.");
        var masks = GeneratedTable.Load(AssetRoot + "metadata/companion_collision_masks.tsv",
            new GeneratedTableSchema("Companion collision masks", GeneratedTableKeySemantics.Ordered,
                ["index", "mask", "source"], headerRequired: true));
        if (masks.Rows.Count != 16) throw new InvalidOperationException("bank0.s companion collision masks require 16 rows.");
        for (int index = 0; index < 16; index++)
        {
            if (masks.Rows[index].UnsignedDecimal(0) != index) throw masks.Rows[index].Invalid(0, "source mask order");
            CollisionMasks[index] = masks.Rows[index].HexByte(1);
        }
    }
}
