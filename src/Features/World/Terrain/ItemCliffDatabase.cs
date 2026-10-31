using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>Native unconditional item passability and directional cliff elevations.</summary>
internal sealed class ItemCliffDatabase
{
    public string AssetRoot { get; }

    private readonly Dictionary<(int CollisionSet, int Direction, int Tile), byte> _deltas = new();
    private readonly HashSet<(int CollisionSet, int Tile)> _passable = new();

    internal ItemCliffDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "metadata/item_passable_cliffs.tsv",
            new GeneratedTableSchema("item-passable cliffs", GeneratedTableKeySemantics.Unique,
                ["collision-set", "direction", "order", "tile", "elevation-delta", "source"],
                ["collision-set", "direction", "tile"], headerRequired: true));
        var nextOrder = new Dictionary<(int, int), int>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int set = row.Decimal(0, 0, 5);
            int direction = row.Decimal(1, 0, 3);
            int order = row.UnsignedDecimal(2);
            byte delta = (byte)row.HexByte(4);
            if (order != nextOrder.GetValueOrDefault((set, direction)) || delta is not (1 or 0xff))
                throw row.Invalid(2, "source-ordered tile/delta pairs with byte delta $01 or $ff");
            nextOrder[(set, direction)] = order + 1;
            _deltas.Add((set, direction, row.HexByte(3)), delta);
            row.RequiredString(5);
        }
        int expected = OracleAssetPack.GameFor(assetRoot) == OracleGame.Ages ? 68 : 36;
        if (_deltas.Count != expected)
            throw new InvalidOperationException($"{assetRoot}: itemPassableCliffTilesTable requires {expected} directional rows.");
        table = GeneratedTable.Load(AssetRoot + "metadata/item_passable_tiles.tsv",
            new GeneratedTableSchema("item-passable tiles", GeneratedTableKeySemantics.Unique,
                ["collision-set", "order", "tile", "source"], ["collision-set", "tile"], headerRequired: true));
        var orders = new int[6];
        foreach (GeneratedTableRow row in table.Rows)
        {
            int set = row.Decimal(0, 0, 5);
            if (row.UnsignedDecimal(1) != orders[set]++)
                throw row.Invalid(1, "source-ordered item-passable tiles");
            _passable.Add((set, row.HexByte(2)));
            row.RequiredString(3);
        }
    }

    internal bool CanPassWithoutElevation(int collisionSet, int tile) => _passable.Contains((collisionSet, tile));

    internal bool TryGetDelta(int collisionSet, byte tile, int angle, out byte delta)
    {
        // angleTable sampled at the shooter's eight angles (angle * 4)
        // selects the cardinal direction, then its clockwise neighbor for
        // diagonals. The first matching table wins, including Up-left.
        int direction = angle >> 1;
        if (_deltas.TryGetValue((collisionSet, direction, tile), out delta))
            return true;
        return (angle & 1) != 0 &&
            _deltas.TryGetValue((collisionSet, (direction + 1) & 3, tile), out delta);
    }
}
