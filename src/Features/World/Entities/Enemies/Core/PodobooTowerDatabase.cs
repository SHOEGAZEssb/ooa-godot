using System.Collections.Concurrent;

namespace Oracles;

internal sealed class PodobooTowerDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<PodobooTowerDatabase>> Packs = new(StringComparer.Ordinal);
    internal static PodobooTowerDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<PodobooTowerDatabase>(() => new(key))).Value;
    internal IReadOnlyList<EnemyBehaviorValue> State { get; }
    internal IReadOnlyList<EnemyBehaviorValue> Radii { get; }
    internal IReadOnlyList<EnemyBehaviorValue> CollisionEffects { get; }

    private PodobooTowerDatabase(string root)
    {
        var table = GeneratedTable.Load(root + "metadata/podoboo_tower_behavior.tsv",
            new("Podoboo Tower behavior", GeneratedTableKeySemantics.Unique,
                ["table", "index", "value", "source"], ["table", "index"], headerRequired: true));
        var groups = table.Rows.GroupBy(row => row.RequiredString(0)).ToDictionary(group => group.Key, group => group.ToArray());
        EnemyBehaviorValue[] Values(string key, int count)
        {
            if (!groups.Remove(key, out var rows) || rows.Length != count)
                throw new InvalidOperationException($"{table.Path}: {key} requires {count} native operands.");
            return rows.Select((row, index) => row.UnsignedDecimal(1) == index
                ? new EnemyBehaviorValue(row.Decimal(2), row.RequiredString(3))
                : throw row.Invalid(1, "native Podoboo Tower operand order")).ToArray();
        }
        State = Values("state-profile", 5); Radii = Values("radii", 15); CollisionEffects = Values("collision-effects", 32);
        if (groups.Count != 0) throw new InvalidOperationException($"{table.Path}: unconsumed native Podoboo Tower tables.");
    }
}
