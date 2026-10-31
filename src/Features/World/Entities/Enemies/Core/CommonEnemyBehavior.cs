using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// Published only after both clean builds verify the same native operands.
internal sealed class CommonEnemyBehavior
{
    internal static CommonEnemyBehavior Shared { get; } = new();
    internal IReadOnlyList<EnemyBehaviorPair> EnemySwordDamageProfiles { get; }
    internal EnemyKnockbackBehaviorProfile EnemyKnockback { get; }
    internal EnemyHazardBehaviorProfile EnemyHazards { get; }
    internal ScentSeedAttractionBehaviorProfile ScentSeedAttraction { get; }
    internal ProjectileBounceBehaviorProfile ProjectileBounce { get; }
    internal IReadOnlyList<EnemyBehaviorPair> ProjectileBoundaryOffsets { get; }

    private CommonEnemyBehavior()
    {
        var table = GeneratedTable.Load(OracleAssetPack.CommonRoot + "metadata/enemy_common_behavior.tsv",
            new("common enemy behavior",GeneratedTableKeySemantics.Unique,
                ["profile","index","value-a","value-b","source"],["profile","index"],headerRequired:true));
        var groups = table.Rows.GroupBy(row => row.RequiredString(0)).ToDictionary(group => group.Key,group => group.ToArray());
        EnemyBehaviorPair[] Pairs(string name, int count)
        {
            if (!groups.Remove(name,out var rows) || rows.Length != count)
                throw new InvalidOperationException($"{table.Path}: native {name} requires {count} ordered rows.");
            var pairs = new EnemyBehaviorPair[count];
            for (int index = 0; index < count; index++)
            {
                if (rows[index].UnsignedDecimal(1) != index) throw rows[index].Invalid(1,$"ordered {name} index {index}");
                pairs[index] = new(rows[index].Decimal(2),rows[index].Decimal(3),rows[index].RequiredString(4));
            }
            return pairs;
        }
        EnemyBehaviorValue[] Values(string name, int count) => Pairs(name,count).Select(pair =>
            pair.Second == 0 ? new EnemyBehaviorValue(pair.First,pair.Source) :
                throw new InvalidOperationException($"{table.Path}: native {name} has an unexpected secondary value.")).ToArray();
        EnemySwordDamageProfiles = Pairs("sword-damage-profiles",4);
        var values = Values("knockback-speeds",2);
        EnemyKnockback = new(values[0].Value,values[1].Value,values);
        values = Values("hazard-profile",7);
        EnemyHazards = new(values[0].Value,values[1].Value,values[2].Value,values[3].Value,
            values[4].Value,values[5].Value,values[6].Value,values);
        values = Values("scent-attraction-profile",4);
        ScentSeedAttraction = new(values[0].Value,values[1].Value,values[2].Value,values[3].Value,values);
        values = Values("projectile-bounce-profile",4);
        ProjectileBounce = new(values[0].Value,values[1].Value,values[2].Value,values[3].Value,values);
        ProjectileBoundaryOffsets = Pairs("projectile-boundary-offsets",8);
        if (groups.Count != 0 || table.Rows.Count != 29)
            throw new InvalidOperationException($"{table.Path}: expected $1d common enemy rows without unclaimed profiles.");
    }
}

internal readonly record struct EnemyKnockbackBehaviorProfile(
    int NormalSpeedRaw,
    int HighSpeedRaw,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct EnemyHazardBehaviorProfile(
    int ProbeY,
    int FirstProbeX,
    int SecondProbeX,
    int FallFrames,
    int PullIntervalMask,
    int PullSpeedRaw,
    int AnimationDecrement,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct ScentSeedAttractionBehaviorProfile(
    int State,
    int AngleRefreshMask,
    int CardinalRounding,
    int CardinalMask,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct ProjectileBounceBehaviorProfile(
    int Frames,
    int Gravity,
    int SpeedRaw,
    int InitialSpeedZ,
    IReadOnlyList<EnemyBehaviorValue> Sources);
