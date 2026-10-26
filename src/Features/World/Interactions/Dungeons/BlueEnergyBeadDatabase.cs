using System;

namespace Oracles;

internal sealed class BlueEnergyBeadDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<BlueEnergyBeadDatabase>> Packs = new(StringComparer.Ordinal);
    internal static BlueEnergyBeadDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<BlueEnergyBeadDatabase>(() => new BlueEnergyBeadDatabase(root))).Value;

    internal int Count { get; }
    internal int Speed { get; }
    internal int Radius { get; }
    internal int DelayMask { get; }
    internal int DeleteAddress { get; }

    private BlueEnergyBeadDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        var row = GeneratedTable.Load(AssetRoot + "objects/blue_energy_bead.tsv",
            new GeneratedTableSchema("PART_BLUE_ENERGY_BEAD inward swirl", GeneratedTableKeySemantics.Unique,
                ["id", "count", "speed", "radius", "delay-mask", "delete-address", "source"],
                ["id"], headerRequired: true)).SingleRow();
        if (row.HexByte(0) != 0x53) throw row.Invalid(0, "PART_BLUE_ENERGY_BEAD $53");
        Count = row.HexByte(1); Speed = row.HexByte(2); Radius = row.HexByte(3); DelayMask = row.HexByte(4);
        DeleteAddress = row.HexWord(5);
    }
}
