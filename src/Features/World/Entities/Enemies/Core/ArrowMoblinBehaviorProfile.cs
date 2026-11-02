using System.Collections.Concurrent;

namespace Oracles;

internal readonly record struct ArrowMoblinBehaviorProfile(
    int SpeedRaw, int MoveCounterBase, int MoveCounterMask, int TurnWait,
    IReadOnlyList<EnemyBehaviorValue> Sources)
{
    private static readonly ConcurrentDictionary<string, Lazy<ArrowMoblinBehaviorProfile>> Packs = new(StringComparer.Ordinal);
    internal static ArrowMoblinBehaviorProfile For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<ArrowMoblinBehaviorProfile>(() => Load(key))).Value;

    private static ArrowMoblinBehaviorProfile Load(string root)
    {
        var table = GeneratedTable.Load(root + "metadata/moblin_behavior.tsv",
            new("Moblin route operands", GeneratedTableKeySemantics.Unique,
                ["index", "value", "source"], ["index"], headerRequired: true));
        if (table.Rows.Count != 4) throw new InvalidOperationException($"{table.Path}: requires four native route operands.");
        var values = table.Rows.Select((row, index) => row.UnsignedDecimal(0) == index
            ? new EnemyBehaviorValue(row.UnsignedDecimal(1), row.RequiredString(2))
            : throw row.Invalid(0, "native operand order")).ToArray();
        return new(values[0].Value, values[1].Value, values[2].Value, values[3].Value, values);
    }
}
