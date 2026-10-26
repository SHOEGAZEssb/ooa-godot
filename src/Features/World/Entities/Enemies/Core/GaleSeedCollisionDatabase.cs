using System;

namespace Oracles;

internal sealed class GaleSeedCollisionDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<GaleSeedCollisionDatabase>> Packs = new(StringComparer.Ordinal);
    internal static GaleSeedCollisionDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<GaleSeedCollisionDatabase>(() => new GaleSeedCollisionDatabase(root))).Value;

    private readonly int[] _effects = new int[0x7d];
    private readonly int[] _modes = new int[128];
    private GaleSeedCollisionDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        GeneratedTable table = GeneratedTable.Load(AssetRoot + "metadata/gale_collision_effects.tsv",
            new GeneratedTableSchema("gale collisions", GeneratedTableKeySemantics.Unique,
                ["mode", "effect", "source"], ["mode"], headerRequired: true));
        if (table.Rows.Count != 0x7d) throw new InvalidOperationException("Expected 125 vanilla gale collision modes.");
        foreach (GeneratedTableRow row in table.Rows) _effects[row.HexByte(0)] = row.HexByte(1);
        table = GeneratedTable.Load(AssetRoot + "metadata/gale_enemy_modes.tsv",
            new GeneratedTableSchema("gale enemy modes", GeneratedTableKeySemantics.Unique,
                ["id", "mode", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("Expected 128 source enemy modes.");
        foreach (GeneratedTableRow row in table.Rows) _modes[row.HexByte(0)] = row.HexByte(1);
    }
    internal int Effect(int mode) => (mode & 0x7f) < _effects.Length
        ? _effects[mode & 0x7f]
        : throw new NotSupportedException($"Vanilla objectCollisionTable has no mode ${(mode & 0x7f):x2}.");
    internal int EnemyMode(int id) => _modes[id];
}
