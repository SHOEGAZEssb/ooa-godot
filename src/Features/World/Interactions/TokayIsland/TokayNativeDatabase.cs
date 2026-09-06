using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TokayNativeDatabase
{
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NpcRecord> _visuals = new(StringComparer.Ordinal);
    internal List<TokayCookJump> CookPaths { get; } = new();
    internal int Constant(string key) => _constants[key];
    internal NpcRecord Visual(string key, int group, int room) =>
        _visuals[key] with { Group = group, Room = room };

    internal TokayNativeDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/tokay_native_constants.tsv",
            new GeneratedTableSchema("Tokay native operands", GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"], ["key"], headerRequired: true)).Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/tokay_cook_paths.tsv",
            new GeneratedTableSchema("Tokay cook jumps", GeneratedTableKeySemantics.Ordered,
                ["index", "angle", "speed-z", "gravity", "source"], ["index"], headerRequired: true)).Rows)
            CookPaths.Add(new(row.HexByte(1), row.Decimal(2), row.Decimal(3)));
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/tokay_native_visuals.tsv",
            new GeneratedTableSchema("Tokay native children", GeneratedTableKeySemantics.Unique,
                ["key", "id", "subid", "sprite", "tile-base", "palette", "animation", "source"],
                ["key"], headerRequired: true)).Rows)
        {
            string animation = row.RequiredString(6);
            _visuals.Add(row.RequiredString(0), new NpcRecord(0, 0, row.HexByte(1), row.HexByte(2),
                0, 0, 0, 0, row.RequiredString(3), row.HexByte(4), row.HexByte(5), 0, false,
                animation, animation, animation, animation, string.Empty, NpcImplementationClassification.EventOwned));
        }
        if (CookPaths.Count != 6 || _visuals.Count != 5 || _constants.Count != 9)
            throw new InvalidOperationException("Tokay native source tables are incomplete.");
    }
}

internal readonly record struct TokayCookJump(int Angle, int SpeedZ, int Gravity);
