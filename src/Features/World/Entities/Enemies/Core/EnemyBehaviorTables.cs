using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed owner of source lookup tables shared by implemented enemy state
/// machines. Generated rows preserve table order and source identity; species
/// classes consume these records instead of maintaining C# copies.
/// </summary>
internal sealed class EnemyBehaviorTables
{
    private static readonly Lazy<EnemyBehaviorTables> LazyShared =
        new(static () => new EnemyBehaviorTables());

    internal static EnemyBehaviorTables Shared => LazyShared.Value;

    internal IReadOnlyList<EnemyBehaviorValue> KeeseDecelerationSpeeds { get; }
    internal IReadOnlyList<EnemyBehaviorValue>
        KeeseDecelerationAnimationMasks { get; }
    internal IReadOnlyList<EnemyBehaviorValue> OctorokCounterValues { get; }
    internal IReadOnlyList<EnemyBehaviorValue> OctorokWalkCounterValues { get; }
    internal IReadOnlyList<EnemyBehaviorValue> BoomerangMoblinRouteCounters { get; }
    internal IReadOnlyList<EnemyBehaviorPair> EnemyArrowSpawnOffsets { get; }
    internal IReadOnlyList<EnemyBehaviorPair> EnemyArrowCollisionRadii { get; }
    internal IReadOnlyList<EnemyBehaviorPair> GiantGhiniChildSpawnOffsets { get; }
    internal IReadOnlyList<EnemyBehaviorValue> PumpkinHeadWalkDurations { get; }
    internal IReadOnlyList<EnemyBehaviorValue> PumpkinHeadStompTimers { get; }
    internal IReadOnlyList<EnemyBehaviorPair> PumpkinHeadFollowOffsets { get; }
    internal IReadOnlyList<EnemyBehaviorValue>
        PumpkinHeadProjectileAngleOffsets { get; }
    internal IReadOnlyList<EnemyBehaviorPair>
        PumpkinHeadProjectileOriginOffsets { get; }

    private EnemyBehaviorTables()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/enemy_behavior_tables.tsv",
            new GeneratedTableSchema(
                "enemy behavior lookup tables",
                GeneratedTableKeySemantics.Grouped,
                ["owner", "table", "index", "value-a", "value-b", "source"],
                ["owner", "table"],
                headerRequired: true));

        var groups = new Dictionary<
            (string Owner, string Table),
            List<EnemyBehaviorPair>>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            string owner = row.RequiredString(0);
            string tableName = row.RequiredString(1);
            if (!groups.TryGetValue(
                (owner, tableName), out List<EnemyBehaviorPair>? values))
            {
                values = new List<EnemyBehaviorPair>();
                groups.Add((owner, tableName), values);
            }
            int index = row.UnsignedDecimal(2);
            if (index != values.Count)
            {
                throw row.Invalid(
                    2,
                    $"contiguous {owner}/{tableName} index {values.Count}");
            }
            values.Add(new EnemyBehaviorPair(
                row.Decimal(3),
                row.Decimal(4),
                row.RequiredString(5)));
        }

        KeeseDecelerationSpeeds = TakeValues(
            groups, "keese", "deceleration-speeds", 8);
        KeeseDecelerationAnimationMasks = TakeValues(
            groups, "keese", "deceleration-animation-masks", 8);
        OctorokCounterValues = TakeValues(
            groups, "octorok", "counter-values", 8);
        OctorokWalkCounterValues = TakeValues(
            groups, "octorok", "walk-counter-values", 4);
        BoomerangMoblinRouteCounters = TakeValues(
            groups, "boomerang-moblin", "route-counters", 4);
        EnemyArrowSpawnOffsets = TakePairs(
            groups, "enemy-arrow", "spawn-offsets", 4);
        EnemyArrowCollisionRadii = TakePairs(
            groups, "enemy-arrow", "collision-radii", 4);
        GiantGhiniChildSpawnOffsets = TakePairs(
            groups, "giant-ghini-child", "spawn-offsets", 3);
        PumpkinHeadWalkDurations = TakeValues(
            groups, "pumpkin-head", "walk-durations", 16);
        PumpkinHeadStompTimers = TakeValues(
            groups, "pumpkin-head", "stomp-timers", 8);
        PumpkinHeadFollowOffsets = TakePairs(
            groups, "pumpkin-head", "head-offsets", 3);
        PumpkinHeadProjectileAngleOffsets = TakeValues(
            groups, "pumpkin-head", "projectile-angle-offsets", 3);
        PumpkinHeadProjectileOriginOffsets = TakePairs(
            groups, "pumpkin-head", "projectile-origin-offsets", 4);

        if (table.Rows.Count != 77 || groups.Count != 0)
        {
            throw new InvalidOperationException(
                $"Enemy behavior table contract expected 77 rows and no " +
                $"unclaimed groups; got {table.Rows.Count} rows and " +
                $"{groups.Count} unclaimed groups.");
        }
    }

    private static EnemyBehaviorValue[] TakeValues(
        Dictionary<(string Owner, string Table), List<EnemyBehaviorPair>> groups,
        string owner,
        string table,
        int count)
    {
        EnemyBehaviorPair[] pairs = TakePairs(groups, owner, table, count);
        var values = new EnemyBehaviorValue[pairs.Length];
        for (int index = 0; index < pairs.Length; index++)
        {
            if (pairs[index].Second != 0)
            {
                throw new InvalidOperationException(
                    $"Enemy behavior value {owner}/{table}[{index}] has " +
                    $"unexpected secondary value {pairs[index].Second}.");
            }
            values[index] = new EnemyBehaviorValue(
                pairs[index].First, pairs[index].Source);
        }
        return values;
    }

    private static EnemyBehaviorPair[] TakePairs(
        Dictionary<(string Owner, string Table), List<EnemyBehaviorPair>> groups,
        string owner,
        string table,
        int count)
    {
        if (!groups.Remove((owner, table), out List<EnemyBehaviorPair>? values))
        {
            throw new InvalidOperationException(
                $"Enemy behavior table {owner}/{table} is missing.");
        }
        if (values.Count != count)
        {
            throw new InvalidOperationException(
                $"Enemy behavior table {owner}/{table} expected {count} rows, " +
                $"got {values.Count}.");
        }
        return values.ToArray();
    }
}

internal readonly record struct EnemyBehaviorValue(int Value, string Source);

/// <summary>
/// An ordered pair from a source table. Geometry rows retain Y/X order and use
/// <see cref="Vector"/> when applied to Godot coordinates; other rows name the
/// two components at their typed consumer.
/// </summary>
internal readonly record struct EnemyBehaviorPair(
    int First,
    int Second,
    string Source)
{
    internal Vector2 Vector => new(Second, First);
}
