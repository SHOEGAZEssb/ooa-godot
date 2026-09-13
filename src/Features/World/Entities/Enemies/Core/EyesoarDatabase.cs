using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class EyesoarDatabase
{
    private readonly Dictionary<string, byte[]> _tables = new(StringComparer.Ordinal);

    internal EyesoarDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/eyesoar_tables.tsv",
            new GeneratedTableSchema("Eyesoar native tables", GeneratedTableKeySemantics.Unique,
                ["profile", "values", "source"], ["profile"], headerRequired: true));
        foreach (var row in table.Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), value => Convert.ToByte(value, 16)));
        foreach (var (name, count) in new[] {
            ("formation-distances", 8), ("center-angles", 4), ("child-angles", 4), ("child-ready-flags", 4),
            ("collision-15", 32), ("collision-4c", 32), ("collision-6d", 32), ("active-11", 32), ("active-7b", 32) })
            if (!_tables.TryGetValue(name, out var bytes) || bytes.Length != count)
                throw new InvalidOperationException($"ENEMY_EYESOAR $7b/$11: missing or incomplete {name} table.");
        if (_tables.Count != 9) throw new InvalidOperationException("Unexpected Eyesoar native profile.");
    }

    internal int FormationDistance(int index) => _tables["formation-distances"][index];
    internal int CenterAngle(int quadrant) => _tables["center-angles"][quadrant];
    internal int ChildAngle(int subid) => _tables["child-angles"][subid];
    internal int ChildReadyFlags(int subid) => _tables["child-ready-flags"][subid];
    internal int CollisionEffect(int mode, int item) => _tables[$"collision-{mode:x2}"][item];
    internal bool CollisionEnabled(int id, int item) => _tables[$"active-{id:x2}"][item] != 0;
}
