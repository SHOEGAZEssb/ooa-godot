using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class OctogonBehaviorProfile
{
    private static readonly Lazy<OctogonBehaviorProfile> LazyShared = new(() => new());
    internal static OctogonBehaviorProfile Shared => LazyShared.Value;
    internal IReadOnlyList<int> State { get; }
    internal IReadOnlyList<int> Targets { get; }
    internal IReadOnlyList<int> BodyOffsets { get; }
    internal IReadOnlyList<int> Shell { get; }
    internal IReadOnlyList<int> SurfacePositions { get; }
    internal IReadOnlyList<int> Compensation { get; }
    internal IReadOnlyList<int> ProjectileOffsets { get; }
    internal IReadOnlyList<int> BodyEffects { get; }
    internal IReadOnlyList<int> ShellEffects { get; }
    internal IReadOnlyList<int> Active { get; }
    internal IReadOnlyList<int> DepthPositions { get; }
    internal IReadOnlyList<int> BubbleZ { get; }
    internal IReadOnlyList<int> PartState { get; }
    internal IReadOnlyDictionary<int,Color[]> Palettes { get; }
    private OctogonBehaviorProfile()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/octogon_behavior.tsv",
            new GeneratedTableSchema("Octogon native behavior",GeneratedTableKeySemantics.Grouped,
                ["table","index","value","source"],["table"],headerRequired:true));
        var groups = new Dictionary<string,List<int>>();
        foreach (var row in table.Rows)
        {
            string name = row.RequiredString(0);
            if (!groups.TryGetValue(name,out var values)) groups.Add(name,values = []);
            if (row.UnsignedDecimal(1) != values.Count) throw row.Invalid(1,$"contiguous {name} indices");
            values.Add(row.Decimal(2)); _ = row.RequiredString(3);
        }
        IReadOnlyList<int> Take(string name,int count)
        {
            if (!groups.Remove(name,out var values) || values.Count != count)
                throw new InvalidOperationException($"octogon.s: {name} requires {count} imported values.");
            return values;
        }
        State = Take("state",23); Targets = Take("targets",96); BodyOffsets = Take("body-offsets",8);
        Shell = Take("shell",16); SurfacePositions = Take("surface-positions",36); Compensation = Take("compensation",9);
        ProjectileOffsets = Take("projectile-offsets",8); BodyEffects = Take("effects-4e",32);
        ShellEffects = Take("effects-67",32); Active = Take("active",32);
        DepthPositions = Take("depth-positions",8); BubbleZ = Take("bubble-z",4); PartState = Take("part-state",11);
        if (groups.Count != 0) throw new InvalidOperationException("octogon.s: unconsumed imported behavior rows.");
        Color[,] palette = OracleGraphicsData.LoadPalette("res://assets/oracle/metadata/octogon_palette.bin",1,6);
        Palettes = new Dictionary<int,Color[]> { [6] = Enumerable.Range(0,4).Select(index => palette[6,index]).ToArray() };
    }
    internal int Speed => State[0];
    internal int SwimAnimationWait => State[1];
    internal int InitialWait => State[2];
    internal int WaterWait => State[3];
    internal int UnderwaterPhaseWait => State[4];
    internal int AttackWait => State[5];
    internal int TargetWait => State[6];
    internal int FireWindup => State[7];
    internal int TurnWait => State[8];
    internal int TurnPause => State[9];
    internal int FireRecovery => State[10];
    internal int SubmergeWait => State[11];
    internal int RiseZStep => State[12];
    internal int DeathWait => State[13];
    internal int PhaseDamage => State[14];
    internal int SurfaceFireChance => State[15];
    internal int AlignmentRadius => State[16];
}
