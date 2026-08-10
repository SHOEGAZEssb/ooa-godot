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
    internal IReadOnlyList<EnemyBehaviorValue>
        SpikedBeetleShakeXOffsets { get; }
    internal ArmoredSwordAttackerKnockbackProfile
        ArmoredSwordAttackerKnockback { get; }
    internal IReadOnlyList<EnemyBehaviorPair> EnemySwordDamageProfiles { get; }
    internal EnemyKnockbackBehaviorProfile EnemyKnockback { get; }
    internal EnemyHazardBehaviorProfile EnemyHazards { get; }
    internal ScentSeedAttractionBehaviorProfile ScentSeedAttraction { get; }
    internal ProjectileBounceBehaviorProfile ProjectileBounce { get; }
    internal KeeseStateBehaviorProfile KeeseState { get; }
    internal ArrowMoblinBehaviorProfile ArrowMoblin { get; }
    internal BabyCuccoBehaviorProfile BabyCucco { get; }
    internal CuccoBehaviorProfile Cucco { get; }
    internal GiantCuccoBehaviorProfile GiantCucco { get; }
    internal CuccoAttackerBehaviorProfile CuccoAttacker { get; }
    internal CrowBehaviorProfile Crow { get; }
    internal GelBehaviorProfile Gel { get; }
    internal ZolBehaviorProfile Zol { get; }
    internal OctorokBehaviorProfile Octorok { get; }
    internal LeeverBehaviorProfile Leever { get; }
    internal SandCrabBehaviorProfile SandCrab { get; }
    internal BoomerangMoblinBehaviorProfile BoomerangMoblin { get; }
    internal RopeBehaviorProfile Rope { get; }
    internal GhiniBehaviorProfile Ghini { get; }
    internal StalfosBehaviorProfile Stalfos { get; }
    internal HardhatBeetleBehaviorProfile HardhatBeetle { get; }
    internal SpikedBeetleBehaviorProfile SpikedBeetle { get; }
    internal SpinyBeetleBehaviorProfile SpinyBeetle { get; }
    internal WallmasterBehaviorProfile Wallmaster { get; }
    internal MoblinBoomerangBehaviorProfile MoblinBoomerang { get; }
    internal PumpkinProjectileBehaviorProfile PumpkinProjectile { get; }
    internal SparkBehaviorProfile Spark { get; }
    internal WhispBehaviorProfile Whisp { get; }
    internal ThwompBehaviorProfile Thwomp { get; }
    internal PeahatBehaviorProfile Peahat { get; }
    internal SwordEnemyBehaviorProfile SwordEnemy { get; }
    internal ColorChangingGelBehaviorProfile ColorChangingGel { get; }

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
                (owner, tableName), out List<EnemyBehaviorPair>? groupValues))
            {
                groupValues = new List<EnemyBehaviorPair>();
                groups.Add((owner, tableName), groupValues);
            }
            int index = row.UnsignedDecimal(2);
            if (index != groupValues.Count)
            {
                throw row.Invalid(
                    2,
                    $"contiguous {owner}/{tableName} index {groupValues.Count}");
            }
            groupValues.Add(new EnemyBehaviorPair(
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
        SpikedBeetleShakeXOffsets = TakeValues(
            groups, "spiked-beetle", "shake-x-offsets", 4);
        EnemyBehaviorValue[] armoredAttackerKnockbackValues = TakeValues(
            groups,
            "common-enemy",
            "armored-sword-attacker-knockback-frames",
            3);
        ArmoredSwordAttackerKnockback = new(
            armoredAttackerKnockbackValues[0].Value,
            armoredAttackerKnockbackValues[1].Value,
            armoredAttackerKnockbackValues[2].Value,
            armoredAttackerKnockbackValues);

        EnemySwordDamageProfiles = TakePairs(
            groups, "common-enemy", "sword-damage-profiles", 4);

        EnemyBehaviorValue[] values = TakeValues(
            groups, "common-enemy", "knockback-speeds", 2);
        EnemyKnockback = new(
            values[0].Value,
            values[1].Value,
            values);

        values = TakeValues(groups, "common-enemy", "hazard-profile", 7);
        EnemyHazards = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values);

        values = TakeValues(
            groups, "common-enemy", "scent-attraction-profile", 4);
        ScentSeedAttraction = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values);

        values = TakeValues(
            groups, "common-projectile", "bounce-profile", 4);
        ProjectileBounce = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values);

        values = TakeValues(groups, "keese", "state-profile", 12);
        KeeseState = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            values[9].Value,
            values[10].Value,
            values[11].Value,
            values);

        values = TakeValues(groups, "arrow-moblin", "state-profile", 4);
        ArrowMoblin = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values);

        values = TakeValues(groups, "baby-cucco", "state-profile", 6);
        BabyCucco = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values);

        values = TakeValues(groups, "cucco", "state-profile", 9);
        EnemyBehaviorValue[] cuccoHopZValues = TakeValues(
            groups, "cucco", "hop-z-values", 16);
        EnemyBehaviorValue[] cuccoRevengeDelays = TakeValues(
            groups, "cucco", "revenge-delays", 9);
        Cucco = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            cuccoHopZValues,
            cuccoRevengeDelays,
            values);

        values = TakeValues(groups, "giant-cucco", "state-profile", 3);
        GiantCucco = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values);

        values = TakeValues(
            groups, "cucco-attacker", "state-profile", 7);
        EnemyBehaviorValue[] cuccoAttackerEdges = TakeValues(
            groups, "cucco-attacker", "screen-edge-positions", 4);
        EnemyBehaviorValue[] cuccoAttackerAxes = TakeValues(
            groups, "cucco-attacker", "edge-axis-values", 32);
        EnemyBehaviorValue[] cuccoAttackerSpeeds = TakeValues(
            groups, "cucco-attacker", "speeds", 9);
        CuccoAttacker = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            cuccoAttackerEdges,
            cuccoAttackerAxes,
            cuccoAttackerSpeeds,
            values);

        values = TakeValues(groups, "crow", "state-profile", 6);
        Crow = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values);

        values = TakeValues(groups, "gel", "state-profile", 8);
        Gel = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values);

        values = TakeValues(groups, "zol", "state-profile", 14);
        Zol = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            values[9].Value,
            values[10].Value,
            values[11].Value,
            values[12].Value,
            values[13].Value,
            values);

        values = TakeValues(groups, "octorok", "state-profile", 2);
        Octorok = new(values[0].Value, values[1].Value, values);

        values = TakeValues(groups, "leever", "state-profile", 6);
        EnemyBehaviorValue[] leeverUndergroundCounters = TakeValues(
            groups, "leever", "underground-counters", 4);
        EnemyBehaviorValue[] leeverLinkRelativeOffsets = TakeValues(
            groups, "leever", "link-relative-offsets", 16);
        Leever = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            leeverUndergroundCounters,
            leeverLinkRelativeOffsets,
            values);

        values = TakeValues(groups, "sand-crab", "state-profile", 5);
        SandCrab = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values);

        values = TakeValues(groups, "boomerang-moblin", "state-profile", 1);
        BoomerangMoblin = new(values[0].Value, values);

        values = TakeValues(groups, "rope", "state-profile", 7);
        Rope = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values);

        values = TakeValues(groups, "ghini", "state-profile", 3);
        Ghini = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values);

        values = TakeValues(groups, "stalfos", "state-profile", 2);
        Stalfos = new(values[0].Value, values[1].Value, values);

        values = TakeValues(
            groups, "hardhat-beetle", "state-profile", 1);
        HardhatBeetle = new(values[0].Value, values);

        values = TakeValues(
            groups, "spiked-beetle", "state-profile", 16);
        SpikedBeetle = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            values[9].Value,
            values[10].Value,
            values[11].Value,
            values[12].Value,
            values[13].Value,
            values[14].Value,
            values[15].Value,
            values);

        values = TakeValues(
            groups, "spiny-beetle", "state-profile", 10);
        SpinyBeetle = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            values[9].Value,
            values);

        values = TakeValues(groups, "wallmaster", "state-profile", 10);
        Wallmaster = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            values[9].Value,
            values);

        values = TakeValues(
            groups, "moblin-boomerang-projectile", "state-profile", 9);
        MoblinBoomerang = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            values);

        values = TakeValues(
            groups, "pumpkin-head-projectile", "state-profile", 5);
        PumpkinProjectile = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values);

        values = TakeValues(groups, "spark", "state-profile", 1);
        Spark = new(values[0].Value, values);

        values = TakeValues(groups, "whisp", "state-profile", 1);
        Whisp = new(values[0].Value, values);

        values = TakeValues(groups, "thwomp", "state-profile", 7);
        Thwomp = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values);

        values = TakeValues(groups, "peahat", "state-profile", 12);
        Peahat = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            Array.ConvertAll(
                values[4..],
                static value => value.Value),
            values);

        values = TakeValues(groups, "sword-enemy", "state-profile", 12);
        SwordEnemy = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values[6].Value,
            values[7].Value,
            values[8].Value,
            Array.ConvertAll(
                values[9..],
                static value => value.Value),
            values);

        values = TakeValues(
            groups, "color-changing-gel", "state-profile", 6);
        ColorChangingGel = new(
            values[0].Value,
            values[1].Value,
            values[2].Value,
            values[3].Value,
            values[4].Value,
            values[5].Value,
            values);

        if (table.Rows.Count != 380 || groups.Count != 0)
        {
            throw new InvalidOperationException(
                $"Enemy behavior table contract expected 380 rows and no " +
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

internal readonly record struct KeeseStateBehaviorProfile(
    int NormalSpeedRaw,
    int ApproachSpeedRaw,
    int InitialRestFrames,
    int ApproachDistance,
    int TurningInterval,
    int TurningIntervals,
    int MovementCounterBase,
    int MovementCounterMask,
    int DecelerationMoveLimit,
    int DecelerationEnd,
    int RestCounterBase,
    int RestCounterMask,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct ArrowMoblinBehaviorProfile(
    int SpeedRaw,
    int MoveCounterBase,
    int MoveCounterMask,
    int TurnWait,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct BabyCuccoBehaviorProfile(
    int SpeedRaw,
    int ProximityDistance,
    int RandomHopMask,
    int HopSpeedZ,
    int HopGravity,
    int AnimationAngleThreshold,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct CuccoBehaviorProfile(
    int WanderSpeedRaw,
    int RunawaySpeedRaw,
    int IdleRollMask,
    int HopCountBase,
    int HopCountMask,
    int AngleMask,
    int RevengeHitThreshold,
    int BabyReplacementId,
    int GiantReplacementId,
    IReadOnlyList<EnemyBehaviorValue> HopZValues,
    IReadOnlyList<EnemyBehaviorValue> RevengeDelays,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct GiantCuccoBehaviorProfile(
    int WanderSpeedRaw,
    int InitialScreenShakeUpdates,
    int PostHitHealth,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct CuccoAttackerBehaviorProfile(
    int EntryDelay,
    int Z,
    int EdgeMask,
    int AxisTableMask,
    int AxisIndexMask,
    int LeftAnimationAngleThreshold,
    int AnimationFrameDuration,
    IReadOnlyList<EnemyBehaviorValue> ScreenEdgePositions,
    IReadOnlyList<EnemyBehaviorValue> EdgeAxisValues,
    IReadOnlyList<EnemyBehaviorValue> Speeds,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct CrowBehaviorProfile(
    int ApproachRadiusY,
    int ApproachRadiusX,
    int RisingFrames,
    int ChargeFrames,
    int ScreenBottom,
    int ScreenRight,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct GelBehaviorProfile(
    int InitialWaitFrames,
    int PrepareHopFrames,
    int InchFrames,
    int InchSpeedRaw,
    int HopSpeedRaw,
    int InitialSpeedZ,
    int Gravity,
    int AttachedFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct ZolBehaviorProfile(
    int WakeDistance,
    int InitialSpeedZ,
    int Gravity,
    int RedInitialWaitFrames,
    int GreenHopCount,
    int GreenWaitFrames,
    int GreenHopSpeedRaw,
    int HiddenWaitFrames,
    int RedSlideFrames,
    int RedSlideSpeedRaw,
    int RedShakeFrames,
    int RedHopSpeedRaw,
    int RedWaitFrames,
    int SplitDelayFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct OctorokBehaviorProfile(
    int ShootDelayFrames,
    int PostShotWaitFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct LeeverBehaviorProfile(
    int ChaseSpeedRaw,
    int SinkSpeedRaw,
    int ChaseCounterMask,
    int ChaseCounterBase,
    int CardinalRounding,
    int CardinalMask,
    IReadOnlyList<EnemyBehaviorValue> UndergroundCounters,
    IReadOnlyList<EnemyBehaviorValue> LinkRelativeOffsets,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct SandCrabBehaviorProfile(
    int AngleMask,
    int DurationMask,
    int DurationBase,
    int VerticalSpeedRaw,
    int HorizontalSpeedRaw,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct BoomerangMoblinBehaviorProfile(
    int SpeedRaw,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct RopeBehaviorProfile(
    int WanderSpeedRaw,
    int ChargeSpeedRaw,
    int CooldownSpeedRaw,
    int CooldownFrames,
    int ApproachAxisRadius,
    int WanderCounterBase,
    int WanderCounterMask,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct GhiniBehaviorProfile(
    int SpeedRaw,
    int MoveCounterBase,
    int MoveCounterMask,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct StalfosBehaviorProfile(
    int MoveCounterBase,
    int MoveCounterMask,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct HardhatBeetleBehaviorProfile(
    int SpeedRaw,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct SpikedBeetleBehaviorProfile(
    int WanderSpeedRaw,
    int ApproachAxisRadius,
    int ChargeInitialSpeedRaw,
    int ChargeAccelerationMask,
    int ChargeSpeedStepRaw,
    int ChargeMaximumSpeedRaw,
    int ChargeCounter,
    int WallRestFrames,
    int FlippedWaitFrames,
    int ShakeThreshold,
    int Gravity,
    int InitialSpeedZ,
    int FlippedRecoilSpeedRaw,
    int FlipBackSpeedRaw,
    int LandingApproachAxisRadius,
    int ArmoredInvincibilityFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct ArmoredSwordAttackerKnockbackProfile(
    int LowFrames,
    int NormalFrames,
    int HighFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct SpinyBeetleBehaviorProfile(
    int SpeedRaw,
    int CoveredCollisionRadius,
    int ApproachAxisRadius,
    int ChargeFrames,
    int RestFrames,
    int RevealFrames,
    int ExposedCollisionRadius,
    int WanderCounter,
    int DungeonBushTile,
    int ChargeCoverZ,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct WallmasterBehaviorProfile(
    int InitialDelayFrames,
    int RetryDelayFrames,
    int SpawnZ,
    int Gravity,
    int GroundFrames,
    int CloseHandCounter,
    int RisePixelsPerFrame,
    int ResetDelayFrames,
    int FlickerBelowZ,
    int VisibleBelowZ,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct MoblinBoomerangBehaviorProfile(
    int OutboundFrames,
    int DecelerationInterval,
    int InitialSpeedRaw,
    int SpeedStepRaw,
    int ReturnMaximumSpeedRaw,
    int ReturnAccelerationMask,
    int CollisionRadius,
    int CatchRadius,
    int DamageQuarters,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct PumpkinProjectileBehaviorProfile(
    int DelayFrames,
    int SpeedRaw,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int DamageQuarters,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct SparkBehaviorProfile(
    int SpeedRaw,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct WhispBehaviorProfile(
    int SpeedRaw,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct ThwompBehaviorProfile(
    int ApproachRadius,
    int Gravity,
    int RestFrames,
    int RiseSpeedFixed,
    int CooldownFrames,
    int RidingRadiusX,
    int RidingSlopY,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct PeahatBehaviorProfile(
    int AccelerationFrames,
    int SlowdownFrames,
    int InitialSpeedRaw,
    int TopSpeedRaw,
    IReadOnlyList<int> FlightCounters,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct SwordEnemyBehaviorProfile(
    int WanderSpeedRaw,
    int ChaseSpeedRaw,
    int ChasePrepareFrames,
    int ChaseFrames,
    int ChaseRadius,
    int RouteCounterBase,
    int RouteCounterMask,
    int TowardLinkMask,
    int TurnIntervalMask,
    IReadOnlyList<int> CooldownFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);

internal readonly record struct ColorChangingGelBehaviorProfile(
    int WaitFrames,
    int HopDelayFrames,
    int SpeedRaw,
    int InitialSpeedZ,
    int Gravity,
    int ColorDelayFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);
