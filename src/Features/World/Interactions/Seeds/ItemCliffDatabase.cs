using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>itemPassableCliffTilesTable, indexed by collision set and direction.</summary>
internal sealed class ItemCliffDatabase
{
    private readonly Dictionary<(int CollisionSet, int Direction, int Tile), byte> _deltas = new();

    internal ItemCliffDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/item_passable_cliffs.tsv",
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
        if (_deltas.Count != 68)
            throw new InvalidOperationException("itemPassableCliffTilesTable requires 68 directional rows.");
    }

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
