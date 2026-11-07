namespace Oracles;

// treasureAndDrops.s:ringTierTable is shared by Maple, Gasha and minigames.
internal sealed class RandomRingDatabase
{
    private readonly List<int>[] _tiers = [[], [], [], [], []];

    internal RandomRingDatabase(string assetRoot)
    {
        var table = GeneratedTable.Load(assetRoot + "metadata/gasha_ring_tiers.tsv",
            new("Random ring tiers", GeneratedTableKeySemantics.Unique,
                ["tier", "index", "ring"], ["tier", "index"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int tier = row.Decimal(0, 0, 4);
            if (row.UnsignedDecimal(1) != _tiers[tier].Count) throw row.Invalid(1, "contiguous source order");
            _tiers[tier].Add(row.HexByte(2));
        }
        for (int tier = 0; tier < 5; tier++)
            if (_tiers[tier].Count != (tier == 4 ? 2 : 8))
                throw new InvalidOperationException($"{assetRoot}: ringTierTable tier ${tier:x2} is incomplete.");
    }

    internal int SelectRing(int tier, byte random) => tier is >= 0 and < 5
        ? _tiers[tier][random & (tier == 4 ? 1 : 7)]
        : throw new ArgumentOutOfRangeException(nameof(tier));
}
