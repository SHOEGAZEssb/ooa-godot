using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// One generated implementation classification for each enemy ID/subid used
/// by the ordered room-object stream. Placement opcodes remain per-source-row
/// data; construction capability is resolved only through this registry.
/// </summary>
internal sealed class EnemyHandlerRegistry
{
    private readonly Dictionary<(int Id, int SubId), EnemyHandlerDescriptor>
        _handlers = new();

    internal EnemyHandlerRegistry(
        IEnumerable<List<RoomObjectRecord>> roomObjectGroups)
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/enemy_handler_registry.tsv",
            new GeneratedTableSchema(
                "enemy handler registry",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "classification", "handler",
                    "enemy-name", "source"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var descriptor = new EnemyHandlerDescriptor(
                row.HexByte(0),
                row.HexByte(1),
                ParseClassification(row, 2),
                ParseHandler(row, 3),
                row.RequiredString(4),
                row.RequiredString(5));
            ValidateDescriptor(row, descriptor);
            if (!_handlers.TryAdd(
                (descriptor.Id, descriptor.SubId), descriptor))
            {
                throw new InvalidOperationException(
                    $"Duplicate enemy handler ${descriptor.Id:x2}:" +
                    $"${descriptor.SubId:x2}.");
            }
        }

        var usedKeys = new HashSet<(int Id, int SubId)>();
        foreach (List<RoomObjectRecord> roomObjects in roomObjectGroups)
        {
            foreach (RoomObjectRecord source in roomObjects)
            {
                if (!IsEnemyPlacement(source.Kind))
                    continue;
                EnemyHandlerDescriptor descriptor = ResolveHandler(source);
                usedKeys.Add((descriptor.Id, descriptor.SubId));
                if (source.Kind == RoomObjectKind.ParameterEnemy &&
                    descriptor.Classification ==
                        EnemyHandlerClassification.OrderedImplemented)
                {
                    throw new InvalidOperationException(
                        $"{source.Source} uses parameter-enemy placement for " +
                        $"ordered handler '{descriptor.Handler}'.");
                }
            }
        }
        if (usedKeys.Count != _handlers.Count)
        {
            throw new InvalidOperationException(
                $"Enemy handler registry contains {_handlers.Count} keys, but " +
                $"the ordered source stream references {usedKeys.Count}.");
        }
    }

    internal EnemyObjectHandlerResolution Resolve(RoomObjectRecord source)
    {
        return source.Kind switch
        {
            RoomObjectKind.RandomEnemy => new EnemyObjectHandlerResolution(
                EnemyObjectSlotPolicy.RandomEnemy, ResolveHandler(source)),
            RoomObjectKind.FixedEnemy => new EnemyObjectHandlerResolution(
                EnemyObjectSlotPolicy.FixedEnemy, ResolveHandler(source)),
            RoomObjectKind.ParameterEnemy => new EnemyObjectHandlerResolution(
                EnemyObjectSlotPolicy.ParameterEnemy, ResolveHandler(source)),
            RoomObjectKind.ItemDrop => new EnemyObjectHandlerResolution(
                EnemyObjectSlotPolicy.ItemDrop, null),
            RoomObjectKind.ReservingPart => new EnemyObjectHandlerResolution(
                EnemyObjectSlotPolicy.ReservingPart, null),
            RoomObjectKind.ParameterPart => new EnemyObjectHandlerResolution(
                EnemyObjectSlotPolicy.ParameterPart, null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(source), source.Kind,
                $"{source.Source} has an unknown placement kind.")
        };
    }

    internal EnemyHandlerDescriptor ResolveHandler(RoomObjectRecord source)
    {
        if (!IsEnemyPlacement(source.Kind))
        {
            throw new InvalidOperationException(
                $"{source.Source} is {source.Kind}, not an enemy placement.");
        }
        if (!_handlers.TryGetValue(
            (source.Id, source.SubId), out EnemyHandlerDescriptor? descriptor))
        {
            throw new InvalidOperationException(
                $"{source.Source} has no handler classification for " +
                $"${source.Id:x2}:${source.SubId:x2}.");
        }
        return descriptor;
    }

    private static bool IsEnemyPlacement(RoomObjectKind kind) =>
        kind is RoomObjectKind.RandomEnemy or
            RoomObjectKind.FixedEnemy or
            RoomObjectKind.ParameterEnemy;

    private static EnemyHandlerClassification ParseClassification(
        GeneratedTableRow row,
        int column) => row.RequiredString(column) switch
    {
        "ordered-implemented" =>
            EnemyHandlerClassification.OrderedImplemented,
        "dynamic-special" =>
            EnemyHandlerClassification.DynamicSpecial,
        "deliberately-unsupported" =>
            EnemyHandlerClassification.DeliberatelyUnsupported,
        _ => throw row.Invalid(
            column,
            "ordered-implemented, dynamic-special, or deliberately-unsupported")
    };

    private static EnemyHandlerKind ParseHandler(
        GeneratedTableRow row,
        int column) => row.RequiredString(column) switch
    {
        "-" => EnemyHandlerKind.None,
        "octorok" => EnemyHandlerKind.Octorok,
        "boomerang-moblin" => EnemyHandlerKind.BoomerangMoblin,
        "arrow-moblin" => EnemyHandlerKind.ArrowMoblin,
        "rope" => EnemyHandlerKind.Rope,
        "ghini" => EnemyHandlerKind.Ghini,
        "wallmaster" => EnemyHandlerKind.Wallmaster,
        "stalfos" => EnemyHandlerKind.Stalfos,
        "keese" => EnemyHandlerKind.Keese,
        "zol" => EnemyHandlerKind.Zol,
        "crow" => EnemyHandlerKind.Crow,
        "gel" => EnemyHandlerKind.Gel,
        "maku-sprout-masked-moblin" =>
            EnemyHandlerKind.MakuSproutMaskedMoblin,
        _ => throw row.Invalid(column, "a registered enemy handler")
    };

    private static void ValidateDescriptor(
        GeneratedTableRow row,
        EnemyHandlerDescriptor descriptor)
    {
        bool valid = descriptor.Classification switch
        {
            EnemyHandlerClassification.OrderedImplemented =>
                descriptor.Handler is not (
                    EnemyHandlerKind.None or
                    EnemyHandlerKind.MakuSproutMaskedMoblin),
            EnemyHandlerClassification.DynamicSpecial =>
                descriptor.Handler == EnemyHandlerKind.MakuSproutMaskedMoblin,
            EnemyHandlerClassification.DeliberatelyUnsupported =>
                descriptor.Handler == EnemyHandlerKind.None,
            _ => false
        };
        if (!valid)
        {
            throw row.Invalid(
                3,
                $"a handler compatible with {descriptor.Classification}");
        }
    }
}

internal sealed record EnemyHandlerDescriptor(
    int Id,
    int SubId,
    EnemyHandlerClassification Classification,
    EnemyHandlerKind Handler,
    string EnemyName,
    string Source)
{
    internal bool SupportsOrderedConstruction =>
        Classification == EnemyHandlerClassification.OrderedImplemented;

    internal bool CompletesDungeonEnemyCount => SupportsOrderedConstruction;
}

internal readonly record struct EnemyObjectHandlerResolution(
    EnemyObjectSlotPolicy SlotPolicy,
    EnemyHandlerDescriptor? Handler)
{
    internal EnemyHandlerDescriptor RequireEnemyHandler(
        RoomObjectRecord source) =>
        Handler ?? throw new InvalidOperationException(
            $"{source.Source} requires an enemy handler for {SlotPolicy}.");
}

internal enum EnemyHandlerClassification
{
    OrderedImplemented,
    DynamicSpecial,
    DeliberatelyUnsupported
}

internal enum EnemyHandlerKind
{
    None,
    Octorok,
    BoomerangMoblin,
    ArrowMoblin,
    Rope,
    Ghini,
    Wallmaster,
    Stalfos,
    Keese,
    Zol,
    Crow,
    Gel,
    MakuSproutMaskedMoblin
}

internal enum EnemyObjectSlotPolicy
{
    RandomEnemy,
    FixedEnemy,
    ParameterEnemy,
    ItemDrop,
    ReservingPart,
    ParameterPart
}
