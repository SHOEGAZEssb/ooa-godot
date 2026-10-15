using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace oracleofages;

internal sealed class RamrockPartDatabase
{
    private static readonly Lazy<RamrockPartDatabase> LazyShared = new(() => new());
    internal static RamrockPartDatabase Shared => LazyShared.Value;
    private readonly Dictionary<int, EnemyProjectileVisualRecord> _visuals = new();
    private readonly Dictionary<int, int[]> _properties = new();
    private readonly Dictionary<(int Id, int Item), (bool Active, int Effect)> _collisions = new();
    private RamrockPartDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/effects/ramrock_parts.tsv",
            new GeneratedTableSchema("Ramrock parts", GeneratedTableKeySemantics.Grouped,
                ["id", "animation-index", "sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "part-data", "source"],
                ["id"], headerRequired: true));
        foreach (var group in table.Rows.GroupBy(row => row.HexByte(0)))
        {
            var rows = group.ToArray(); var first = rows[0];
            int count = group.Key switch { 0x34 => 1, 0x35 => 2, 0x4f => 5, _ => throw first.Invalid(0, "PART$34/$35/$4f") };
            if (rows.Length != count || rows.Where((row, i) => row.UnsignedDecimal(1) != i).Any())
                throw new InvalidOperationException($"Ramrock PART${group.Key:x2} requires {count} ordered animations.");
            _visuals.Add(group.Key, new(first.SplitRequired(2, ','), first.UnsignedDecimal(3), first.UnsignedDecimal(4),
                first.Boolean01(5), rows.Select(row => row.RequiredString(6)).ToArray()));
            _properties.Add(group.Key, first.SplitRequired(7, ',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray());
            foreach (var row in rows) _ = row.RequiredString(8);
        }
        var collisions = GeneratedTable.Load("res://assets/oracle/effects/ramrock_part_collisions.tsv",
            new GeneratedTableSchema("Ramrock part collisions", GeneratedTableKeySemantics.Unique,
                ["id", "collision", "enabled", "effect", "source"], ["id", "collision"], headerRequired: true));
        foreach (var row in collisions.Rows)
        { _collisions.Add((row.HexByte(0), row.HexByte(1)), (row.Boolean01(2), row.HexByte(3))); _ = row.RequiredString(4); }
        if (_visuals.Count != 3 || _collisions.Count != 96 || _properties.Values.Any(p => p.Length != 8))
            throw new InvalidOperationException("Ramrock requires PART$34/$35/$4f, eight property bytes and 32 collision columns each.");
    }
    internal EnemyProjectileVisualRecord Visual(int id) => _visuals[id];
    internal IReadOnlyList<int> Properties(int id) => _properties[id];
    internal (bool Active, int Effect) Collision(int id, int item) => _collisions[(id, item)];
}
