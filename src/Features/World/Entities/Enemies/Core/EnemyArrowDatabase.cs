using System.Collections.Concurrent;

namespace Oracles;

// PART$1a owns both native subids. Wall-launcher placement remains Ages content.
internal sealed class EnemyArrowDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<EnemyArrowDatabase>> Packs = new(StringComparer.Ordinal);
    internal static EnemyArrowDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<EnemyArrowDatabase>(() => new(key))).Value;
    internal EnemyArrowRecord Record { get; }
    internal IReadOnlyList<EnemyBehaviorPair> SpawnOffsets { get; }
    internal IReadOnlyList<EnemyBehaviorPair> CollisionRadii { get; }
    internal int LaunchDelay { get; }
    internal Vector2 LaunchRadii { get; }

    private EnemyArrowDatabase(string root)
    {
        var table = GeneratedTable.Load(root + "effects/enemy_arrow.tsv",
            new("enemy arrow", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "damage-quarters", "speed-raw", "up-animation",
                 "right-animation", "down-animation", "left-animation", "bounce-animation"], headerRequired: true));
        var arrow = table.SingleRow();
        Record = new(root, arrow.RequiredString(0), arrow.UnsignedDecimal(1), arrow.UnsignedDecimal(2),
            arrow.UnsignedDecimal(3), arrow.UnsignedDecimal(4), arrow.RequiredString(5), arrow.RequiredString(6),
            arrow.RequiredString(7), arrow.RequiredString(8), arrow.RequiredString(9));
        table = GeneratedTable.Load(root + "metadata/enemy_arrow_behavior.tsv",
            new("enemy arrow operands", GeneratedTableKeySemantics.Grouped,
                ["table", "index", "first", "second", "source"], ["table"], headerRequired: true));
        EnemyBehaviorPair[] Read(string name, int count)
        {
            var rows = table.Rows.Where(row => row.RequiredString(0) == name).ToArray();
            if (rows.Length != count) throw new InvalidOperationException($"{table.Path}: {name} requires {count} native rows.");
            return rows.Select((row, index) => row.UnsignedDecimal(1) == index
                ? new EnemyBehaviorPair(row.Decimal(2), row.Decimal(3), row.RequiredString(4))
                : throw row.Invalid(1, "native operand order")).ToArray();
        }
        SpawnOffsets = Read("spawn-offsets", 4);
        CollisionRadii = Read("collision-radii", 4);
        LaunchDelay = Read("launch-delay", 1)[0].First;
        LaunchRadii = Read("launch-radii", 1)[0].Vector;
        if (table.Rows.Count != 10) throw new InvalidOperationException($"{table.Path}: unexpected native arrow operands.");
    }
}

public readonly record struct EnemyArrowRecord(string AssetRoot, string SpriteName, int TileBase, int Palette, int DamageQuarters, int SpeedRaw, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation, string BounceAnimation);
