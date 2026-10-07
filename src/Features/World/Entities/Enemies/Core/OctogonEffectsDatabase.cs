using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace oracleofages;

internal sealed class OctogonEffectsDatabase
{
    private static readonly Lazy<OctogonEffectsDatabase> LazyShared = new(() => new());
    internal static OctogonEffectsDatabase Shared => LazyShared.Value;
    private readonly Dictionary<int,EnemyProjectileVisualRecord> _visuals = [];
    private readonly Dictionary<int,int[]> _properties = [];
    private readonly Dictionary<(int,int),(bool Enabled,int Effect)> _collisions = [];
    internal EnemyProjectileVisualRecord Visual(int id) => _visuals[id];
    internal IReadOnlyList<int> Properties(int id) => _properties[id];
    internal (bool Enabled,int Effect) Collision(int id,int collision) => _collisions[(id,collision)];
    private OctogonEffectsDatabase()
    {
        void Load(string path,bool parts)
        {
            string[] columns = parts
                ? ["id","animation-index","sprite","tile-base","palette","source-grayscale-inverted","animation","part-data","source"]
                : ["id","animation-index","sprite","tile-base","palette","source-grayscale-inverted","animation","source"];
            var table = GeneratedTable.Load(path,new GeneratedTableSchema("Octogon native effects",GeneratedTableKeySemantics.Unique,
                columns,["id","animation-index"],headerRequired:true));
            foreach (var group in table.Rows.GroupBy(row => row.HexByte(0)))
            {
                var rows = group.ToArray(); int expected = group.Key == 0x8e ? 4 : group.Key == 0x91 ? 1 : 2;
                if (rows.Length != expected || rows.Where((row,index) => row.UnsignedDecimal(1) != index).Any())
                    throw new InvalidOperationException($"Octogon effect${group.Key:x2} lost its {expected} ordered animations.");
                var first = rows[0];
                _visuals.Add(group.Key,new([first.RequiredString(2)],first.UnsignedDecimal(3),first.UnsignedDecimal(4),first.Boolean01(5),
                    rows.Select(row => row.RequiredString(6)).ToArray()));
                if (parts)
                {
                    int[] bytes = first.SplitRequired(7,',').Select(value => int.Parse(value,CultureInfo.InvariantCulture)).ToArray();
                    if (bytes.Length != 8 || bytes.Any(value => value is < 0 or > 255)) throw first.Invalid(7,"eight native property bytes");
                    _properties.Add(group.Key,bytes);
                }
                foreach (var row in rows) _ = row.RequiredString(parts ? 8 : 7);
            }
        }
        Load("res://assets/oracle/effects/octogon_parts.tsv",true);
        Load("res://assets/oracle/effects/octogon_interactions.tsv",false);
        var collisions = GeneratedTable.Load("res://assets/oracle/effects/octogon_part_collisions.tsv",
            new GeneratedTableSchema("Octogon PART collisions",GeneratedTableKeySemantics.Unique,
                ["id","collision","enabled","effect","source"],["id","collision"],headerRequired:true));
        foreach (var row in collisions.Rows)
        { _collisions.Add((row.HexByte(0),row.HexByte(1)),(row.Boolean01(2),row.HexByte(3))); _ = row.RequiredString(4); }
        if (_visuals.Count != 4 || _properties.Count != 2 || _collisions.Count != 64)
            throw new InvalidOperationException("Octogon effects require PART$48/$55 and INTERAC$8e/$91.");
    }
}
