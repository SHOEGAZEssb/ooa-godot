
namespace Oracles;

internal sealed class BoomerangDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Root, int SubId), Lazy<BoomerangDatabase>> Packs = new();
    internal static BoomerangDatabase For(string assetRoot, int subId = 0) => Packs.GetOrAdd((assetRoot, subId),
        static key => new Lazy<BoomerangDatabase>(() => new BoomerangDatabase(key.Root, key.SubId))).Value;
    internal int SubId { get; }
    internal bool SupportsLevel2 { get; }
    internal int BreakSource { get; }

    internal int Speed { get; }
    internal int OutwardUpdates { get; }
    internal int CatchUpdates { get; }
    internal int NearRadius { get; }
    internal int CatchRadius { get; }
    internal int Sound { get; }
    internal int Collision { get; }
    internal Vector2I Radius { get; }
    internal int Damage { get; }
    internal int OamFlags { get; }
    internal bool SourceInverted => false;
    internal string Animation { get; }

    private BoomerangDatabase(string assetRoot, int subId)
    {
        AssetRoot = assetRoot;
        SubId = subId;

        var table = GeneratedTable.Load(AssetRoot + "metadata/boomerang.tsv",
            new GeneratedTableSchema("ITEM_BOOMERANG $06", GeneratedTableKeySemantics.Unique,
                ["subid", "speed-raw", "outward-updates", "catch-updates", "near-radius", "catch-radius", "sound",
                 "collision", "radius-y", "radius-x", "damage", "oam-flags", "break-source", "animation", "source"], ["subid"],
                headerRequired: true));
        GeneratedTableRow? selected = null;
        foreach (var candidate in table.Rows)
        {
            int id = candidate.Decimal(0, 0, 1);
            SupportsLevel2 |= id == 1;
            if (id == subId) selected = candidate;
        }
        var row = selected ?? throw new NotSupportedException($"{assetRoot}: ITEM$06 subid${subId:x2} is not a native Boomerang variant.");
        Speed = row.Decimal(1, 1, 255);
        OutwardUpdates = row.Decimal(2, 1, 255);
        CatchUpdates = row.Decimal(3, 1, 255);
        NearRadius = row.Decimal(4, 1, 127);
        CatchRadius = row.Decimal(5, 1, 127);
        Sound = row.HexByte(6);
        Collision = row.HexByte(7);
        Radius = new(row.Decimal(9, 0, 15), row.Decimal(8, 0, 15));
        Damage = -unchecked((sbyte)row.HexByte(10));
        OamFlags = row.HexByte(11);
        BreakSource = row.Decimal(12, -1, 7);
        Animation = row.RequiredString(13);
        _ = row.RequiredString(14);
    }
}
