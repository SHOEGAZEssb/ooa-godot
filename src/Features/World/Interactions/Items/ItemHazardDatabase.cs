using System;
using System.Collections.Generic;

namespace oracleofages;

// objectCheckIsOverHazard, shared by physical ITEM handlers.
internal sealed class ItemHazardDatabase
{
    internal static ItemHazardDatabase Shared { get; } = new();
    private readonly Dictionary<int, int> _hazards = new();

    private ItemHazardDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/item_hazards.tsv",
            new GeneratedTableSchema("hazardCollisionTable", GeneratedTableKeySemantics.Unique,
                ["mode", "tile", "kind", "source"], ["mode", "tile"], headerRequired: true));
        if (table.Rows.Count != 84) throw new InvalidOperationException("hazardCollisionTable requires $54 source pairs.");
        foreach (var row in table.Rows)
        {
            int kind = row.Decimal(2, 1, 4);
            if (kind is not (1 or 2 or 4)) throw row.Invalid(2, "water$01/hole$02/lava$04");
            _hazards.Add((row.Decimal(0, 0, 5) << 8) | row.HexByte(1), kind);
            _ = row.RequiredString(3);
        }
    }

    internal int Hazard(int mode, int tile) => mode is >= 0 and < 6 ? _hazards.GetValueOrDefault((mode << 8) | tile) :
        throw new NotSupportedException($"hazardCollisionTable: collision mode ${mode:x2} is not imported.");
}
