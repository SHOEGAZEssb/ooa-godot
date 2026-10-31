using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// Source data for the common enemy $32, independent of other species and story.
internal sealed class KeeseDatabase
{
    internal int InitialSpeedRaw { get; }
    private static readonly ConcurrentDictionary<string,Lazy<KeeseDatabase>> Packs = new(StringComparer.Ordinal);
    internal static KeeseDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<KeeseDatabase>(() => new(root))).Value;
    internal string AssetRoot { get; }
    private readonly Dictionary<int,KeeseDefinition> _definitions = new();
    internal bool SupportsSubId(int subId) => _definitions.ContainsKey(subId);
    internal IReadOnlyList<EnemyBehaviorValue> KeeseDecelerationSpeeds { get; }
    internal IReadOnlyList<EnemyBehaviorValue> KeeseDecelerationAnimationMasks { get; }
    internal KeeseStateBehaviorProfile KeeseState { get; }

    private KeeseDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        var table = GeneratedTable.Load(
            AssetRoot + "objects/keese.tsv",
            new GeneratedTableSchema(
                "Keese definitions",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "sprite", "tile-base", "palette", "radius-y",
                    "radius-x", "damage-quarters", "health", "idle-animation",
                    "fly-animation", "raw-damage"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            KeeseDefinition definition = new KeeseDefinition(AssetRoot,
                row.HexByte(0),
                row.HexByte(1),
                row.RequiredString(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.RequiredString(9),
                row.RequiredString(10), row.HexByte(11));
            if (definition.Id != EnemyId.Keese ||
                !_definitions.TryAdd(definition.SubId, definition))
            {
                throw row.Invalid(0, "unique ENEMY_KEESE $32 definition");
            }
        }
        if (_definitions.Count != 2)
            throw new InvalidOperationException(
                $"Expected two Keese definitions, got {_definitions.Count}.");


        var behavior = GeneratedTable.Load(OracleAssetPack.CommonRoot + "metadata/keese_behavior.tsv",
            new("native Keese behavior",GeneratedTableKeySemantics.Unique,
                ["table","index","value","source"],["table","index"],headerRequired:true));
        var groups = behavior.Rows.GroupBy(row => row.RequiredString(0)).ToDictionary(group => group.Key,group => group.ToArray());
        EnemyBehaviorValue[] Values(string key, int count)
        {
            if (!groups.Remove(key,out var rows) || rows.Length != count)
                throw new InvalidOperationException($"{behavior.Path}: {key} requires {count} ordered native rows.");
            return rows.Select((row,index) => row.UnsignedDecimal(1) == index
                ? new EnemyBehaviorValue(row.Decimal(2),row.RequiredString(3))
                : throw row.Invalid(1,$"ordered {key} index {index}")).ToArray();
        }
        var initialization = GeneratedTable.Load(assetRoot + "metadata/keese_initialization.tsv",
            new("Keese state-zero accumulator",GeneratedTableKeySemantics.Ordered,
                ["initial-speed","source"],headerRequired:true));
        var initial = initialization.SingleRow();
        InitialSpeedRaw = initial.HexByte(0);
        _ = initial.RequiredString(1);
        KeeseDecelerationSpeeds = Values("deceleration-speeds",8);
        KeeseDecelerationAnimationMasks = Values("deceleration-animation-masks",8);
        var values = Values("state-profile",14);
        KeeseState = new(values[0].Value,values[1].Value,values[2].Value,values[3].Value,
            values[4].Value,values[5].Value,values[6].Value,values[7].Value,
            values[8].Value,values[9].Value,values[10].Value,values[11].Value,
            values[12].Value,values[13].Value,values);
        if (behavior.Rows.Count != 30 || groups.Count != 0)
            throw new InvalidOperationException($"{behavior.Path}: expected $1e fully consumed native behavior rows.");
    }

    internal bool TryGetDefinition(RoomObjectRecord source, out EnemyDatabaseEnemyRecord record)
    {
        if (source.Id != 0x32 ||
            !_definitions.TryGetValue(source.SubId, out KeeseDefinition definition))
        {
            record = default;
            return false;
        }
        record = new EnemyDatabaseEnemyRecord(AssetRoot,
            source.Group,
            source.Room,
            definition.Id,
            definition.SubId,
            source.Flags,
            source.Count,
            definition.SpriteName,
            definition.TileBase,
            definition.Palette,
            definition.CollisionRadiusY,
            definition.CollisionRadiusX,
            definition.DamageQuarters,
            definition.Health,
            definition.IdleAnimation,
            definition.FlyAnimation, definition.RawDamage);
        return true;
    }

}

internal readonly record struct KeeseDefinition(string AssetRoot, int Id, int SubId, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, string IdleAnimation, string FlyAnimation, int RawDamage);

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
    int InitialTurnAmount,
    int ReverseTurningMask,
    IReadOnlyList<EnemyBehaviorValue> Sources);
