using System.Collections.Concurrent;

namespace Oracles;

internal sealed class OctorokDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<OctorokDatabase>> Packs = new(StringComparer.Ordinal);
    internal static OctorokDatabase For(string root) => Packs.GetOrAdd(root,
        static key => new Lazy<OctorokDatabase>(() => new(key))).Value;
    private readonly Dictionary<int, OctorokRecord> _definitions = new();
    internal OctorokProjectileRecord Projectile { get; }
    internal IReadOnlyList<EnemyBehaviorValue> CounterValues { get; }
    internal IReadOnlyList<EnemyBehaviorValue> WalkCounterValues { get; }
    internal OctorokBehaviorProfile Behavior { get; }

    private OctorokDatabase(string root)
    {
        var table = GeneratedTable.Load(root + "objects/octoroks.tsv",
            new("Octorok definitions", GeneratedTableKeySemantics.Unique,
                ["id", "subid", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                 "damage-quarters", "health", "speed-raw", "counter-mask", "up-animation",
                 "right-animation", "down-animation", "left-animation", "raw-damage"], ["id", "subid"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var definition = new OctorokRecord(root, row.HexByte(0), row.HexByte(1), row.RequiredString(2),
                row.UnsignedDecimal(3), row.UnsignedDecimal(4), row.UnsignedDecimal(5), row.UnsignedDecimal(6),
                row.UnsignedDecimal(7), row.UnsignedDecimal(8), row.UnsignedDecimal(9), row.UnsignedDecimal(10),
                row.RequiredString(11), row.RequiredString(12), row.RequiredString(13), row.RequiredString(14), row.HexByte(15));
            int last = root == OracleAssetPack.ForGame(OracleGame.Seasons) ? 3 : 2;
            if (definition.Id != EnemyId.Octorok || definition.SubId > last || !_definitions.TryAdd(definition.SubId, definition))
                throw row.Invalid(0, $"unique ENEMY_OCTOROK $09:$00-${last:x2} definition");
        }
        int expected = root == OracleAssetPack.ForGame(OracleGame.Seasons) ? 4 : 3;
        if (_definitions.Count != expected) throw new InvalidOperationException($"{table.Path}: requires all {expected} supported Octorok definitions.");
        table = GeneratedTable.Load(root + "effects/octorok_projectile.tsv",
            new("Octorok projectile", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "radius-y", "radius-x", "damage-quarters",
                 "speed-raw", "normal-animation", "bounce-animation"], headerRequired: true));
        var part = table.SingleRow();
        Projectile = new(root, part.RequiredString(0), part.UnsignedDecimal(1), part.UnsignedDecimal(2),
            part.UnsignedDecimal(3), part.UnsignedDecimal(4), part.UnsignedDecimal(5), part.UnsignedDecimal(6),
            part.RequiredString(7), part.RequiredString(8));
        table = GeneratedTable.Load(root + "metadata/octorok_behavior.tsv",
            new("Octorok behavior", GeneratedTableKeySemantics.Grouped,
                ["table", "index", "value", "source"], ["table"], headerRequired: true));
        EnemyBehaviorValue[] Read(string name, int count)
        {
            var rows = table.Rows.Where(row => row.RequiredString(0) == name).ToArray();
            if (rows.Length != count) throw new InvalidOperationException($"{table.Path}: {name} requires {count} native operands.");
            return rows.Select((row, index) => row.UnsignedDecimal(1) == index
                ? new EnemyBehaviorValue(row.Decimal(2), row.RequiredString(3))
                : throw row.Invalid(1, "native operand order")).ToArray();
        }
        CounterValues = Read("counter-values", 8);
        WalkCounterValues = Read("walk-counter-values", 4);
        var values = Read("state-profile", 2);
        Behavior = new(values[0].Value, values[1].Value, values);
        if (table.Rows.Count != 14) throw new InvalidOperationException($"{table.Path}: unexpected Octorok behavior rows.");
    }

    internal bool HasDefinition(int subId) => _definitions.ContainsKey(subId);
    internal bool TryGetDefinition(RoomObjectRecord source, out OctorokRecord record)
    {
        record = default;
        return source.Id == EnemyId.Octorok && _definitions.TryGetValue(source.SubId, out record);
    }

}


internal readonly record struct OctorokBehaviorProfile(
    int ShootDelayFrames,
    int PostShotWaitFrames,
    IReadOnlyList<EnemyBehaviorValue> Sources);

public readonly record struct OctorokProjectileRecord(string AssetRoot, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int SpeedRaw, string NormalAnimation, string BounceAnimation);

public readonly record struct OctorokRecord(string AssetRoot, int Id, int SubId, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, int CounterMask, string UpAnimation, string RightAnimation, string DownAnimation, string LeftAnimation, int RawDamage);
