using System;

namespace oracleofages;

internal sealed class BlackTowerFlameDatabase
{
    private readonly byte[][] _routes = new byte[4][];
    private readonly int[] _delays = new int[4];
    internal DungeonInteractionVisual Visual { get; }
    internal int Radius { get; }
    internal int Damage { get; }
    internal BlackTowerFlameDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/black_tower_flames.tsv",
            new("PART $59 routes", GeneratedTableKeySemantics.Unique, ["subid", "delay", "route", "source"], ["subid"], headerRequired: true)).Rows)
        {
            int subid = row.HexByte(0);
            if (subid >= 4) throw row.Invalid(0, "PART $59 subid $00-$03");
            _delays[subid] = row.HexByte(1);
            _routes[subid] = Array.ConvertAll(row.SplitRequired(2, ','), value => Convert.ToByte(value, 16));
            if (_routes[subid].Length < 2 || _routes[subid][^1] != 0) throw row.Invalid(2, "terminated PART $59 route");
        }
        for (int i = 0; i < 4; i++)
            if (_routes[i] is null) throw new InvalidOperationException($"kingMoblinMinionMain.s: missing PART $59:${i:x2} route.");
        var visual = GeneratedTable.Load("res://assets/oracle/effects/black_tower_flame.tsv",
            new("PART $59 visual", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "radius", "damage-quarters", "source-grayscale-inverted", "animation", "source"], ["sprite"], headerRequired: true)).SingleRow();
        Visual = new("black-tower-flame", [visual.RequiredString(0)], visual.Decimal(1), visual.Decimal(2), visual.Boolean01(5), [visual.RequiredString(6)]);
        Radius = visual.Decimal(3); Damage = visual.Decimal(4);
    }
    internal int Delay(int subid) => _delays[subid];
    internal int Target(int subid, int index) => index < _routes[subid].Length ? _routes[subid][index] :
        throw new InvalidOperationException($"kingMoblinMinionMain.s: PART $59:${subid:x2} route index ${index:x2} exceeds its terminator.");
}
