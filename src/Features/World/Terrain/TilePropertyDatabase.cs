using System;
using System.Collections.Concurrent;

namespace Oracles;

internal sealed class TilePropertyDatabase
{
    private const int CollisionModeCount = 6;
    private static readonly ConcurrentDictionary<string, Lazy<TilePropertyDatabase>> Packs = new(StringComparer.Ordinal);
    internal static TilePropertyDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        root => new Lazy<TilePropertyDatabase>(() => new(root))).Value;

    private readonly byte[,] _types = new byte[CollisionModeCount, 256];
    private readonly byte[,] _hazards = new byte[CollisionModeCount, 256];
    private readonly bool[] _sideScrolling = new bool[CollisionModeCount];
    private readonly int _sideScrollMode;
    internal string AssetRoot { get; }

    private TilePropertyDatabase(string assetRoot)
    {
        _ = OracleAssetPack.GameFor(assetRoot);
        AssetRoot = assetRoot;
        GeneratedTable modes = GeneratedTable.Load(assetRoot + "metadata/collision_modes.tsv",
            new("native collision modes", GeneratedTableKeySemantics.Ordered,
                ["mode", "name", "side-scrolling", "source"], headerRequired: true));
        if (modes.Rows.Count != CollisionModeCount)
            throw new InvalidOperationException($"{modes.Path}: expected six source collision-mode pointers.");
        int sideScrollMode = -1;
        for (int index = 0; index < modes.Rows.Count; index++)
        {
            GeneratedTableRow row = modes.Rows[index];
            if (row.UnsignedDecimal(0) != index) throw row.Invalid(0, $"native collision-mode index ${index:x2}");
            _ = row.RequiredString(1);
            _ = row.RequiredString(3);
            _sideScrolling[index] = row.Decimal(2, 0, 1) != 0;
            if (!_sideScrolling[index]) continue;
            if (sideScrollMode >= 0) throw row.Invalid(2, "one native side-scrolling pointer");
            sideScrollMode = index;
        }
        if (sideScrollMode < 0) throw new InvalidOperationException($"{modes.Path}: missing side-scrolling pointer.");
        _sideScrollMode = sideScrollMode;
        LoadMappings("tile_types", _types, hazards: false);
        LoadMappings("tile_hazards", _hazards, hazards: true);
    }

    internal byte NativeTileType(int mode, byte tile) => _types[RequireMode(mode), tile];
    internal byte NativeHazardFlags(int mode, byte tile) => _hazards[RequireMode(mode), tile];
    internal TerrainType Type(int mode, byte tile) =>
        _sideScrolling[RequireMode(mode)] ? TerrainType.Normal : (TerrainType)_types[mode, tile];
    internal SideScrollTileType SideScrollType(byte tile) => (SideScrollTileType)_types[_sideScrollMode, tile];

    internal HazardType Hazard(int mode, byte tile) => (NativeHazardFlags(mode, tile) & 7) switch
    {
        1 => HazardType.Water,
        2 => HazardType.Hole,
        4 => HazardType.Lava,
        _ => HazardType.None
    };

    private int RequireMode(int mode) => mode is >= 0 and < CollisionModeCount ? mode :
        throw new ArgumentOutOfRangeException(nameof(mode), $"{AssetRoot}: native collision mode ${mode:x2} is outside $00-$05.");

    private void LoadMappings(string name, byte[,] destination, bool hazards)
    {
        GeneratedTable table = GeneratedTable.Load(AssetRoot + $"metadata/{name}.tsv",
            new(name, GeneratedTableKeySemantics.Ordered,
                ["mode", "index", "tile", "value", "source"], headerRequired: true));
        var seen = new bool[CollisionModeCount, 256];
        int previousMode = -1, nextIndex = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int mode = row.Decimal(0, 0, CollisionModeCount - 1);
            if (mode < previousMode) throw row.Invalid(0, "source collision-mode pointer order");
            if (mode != previousMode) { previousMode = mode; nextIndex = 0; }
            if (row.UnsignedDecimal(1) != nextIndex++) throw row.Invalid(1, "source pair order before the $00 terminator");
            byte tile = (byte)row.HexByte(2), value = (byte)row.HexByte(3);
            _ = row.RequiredString(4);
            if (tile == 0) throw row.Invalid(2, "a nonterminating source tile");
            if (hazards ? value is not (1 or 2 or 4 or 0x42) :
                _sideScrolling[mode] ? (value & ~0xf5) != 0 : value > 0x18)
                throw row.Invalid(3, hazards ? "native hazard flags $01/$02/$04/$42" : "native tile type or side-view flags");
            // bank0.s:lookupKey returns on the first matching pair. In Seasons
            // side view, $0c's later LAVA entry must not overwrite its HOLE entry.
            if (seen[mode, tile]) continue;
            seen[mode, tile] = true;
            destination[mode, tile] = value;
        }
    }
}
