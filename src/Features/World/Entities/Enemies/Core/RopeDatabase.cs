using System.Collections.Concurrent;

namespace Oracles;

internal sealed class RopeDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<RopeDatabase>> Packs = new(StringComparer.Ordinal);
    internal static RopeDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<RopeDatabase>(() => new(key))).Value;
    internal RopeBehaviorProfile Behavior { get; }

    private RopeDatabase(string root)
    {
        var table = GeneratedTable.Load(root + "metadata/rope_behavior.tsv",
            new("Rope behavior", GeneratedTableKeySemantics.Unique,
                ["index", "value", "source"], ["index"], headerRequired: true));
        if (table.Rows.Count != 11) throw new InvalidOperationException($"{table.Path}: expected eleven native Rope operands.");
        var values = table.Rows.Select((row, index) => row.UnsignedDecimal(0) == index
            ? new EnemyBehaviorValue(row.Decimal(1), row.RequiredString(2))
            : throw row.Invalid(0, "native Rope operand order")).ToArray();
        Behavior = new(values[0].Value, values[1].Value, values[2].Value, values[3].Value,
            values[4].Value, values[5].Value, values[6].Value, values[7].Value,
            values[8].Value, values[9].Value, values[10].Value, values);
    }
}

internal readonly record struct RopeBehaviorProfile(
    int WanderSpeedRaw,
    int ChargeSpeedRaw,
    int CooldownSpeedRaw,
    int CooldownFrames,
    int ApproachAxisRadius,
    int WanderCounterBase,
    int WanderCounterMask,
    int FallDelayMask,
    int InitialFallSpeedZ,
    int FallScreenOffset,
    int FallGravity,
    IReadOnlyList<EnemyBehaviorValue> Sources);
