
namespace Oracles;

/// <summary>
/// One generated implementation classification for each enemy ID/subid used
/// by the ordered room-object stream. Placement opcodes remain per-source-row
/// data; construction capability is resolved only through this registry.
/// </summary>
internal sealed class EnemyHandlerRegistry
{
    internal string AssetRoot { get; }
    private readonly Dictionary<(int Id, int SubId), EnemyHandlerDescriptor>
        _handlers = new();

    internal IEnumerable<EnemyHandlerDescriptor> Handlers => _handlers.Values;

    internal EnemyHandlerRegistry(string assetRoot)
    {
        AssetRoot = assetRoot;
        GeneratedTable table = GeneratedTable.Load(
            assetRoot + "objects/enemy_handler_registry.tsv",
            new GeneratedTableSchema(
                "enemy handler registry",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "collision-mode", "classification",
                    "handler", "enemy-name", "source"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var descriptor = new EnemyHandlerDescriptor(
                AssetRoot,
                row.HexByte(0),
                row.HexByte(1),
                row.HexByte(2),
                ParseClassification(row, 3),
                ParseHandler(row, 4),
                row.RequiredString(5),
                row.RequiredString(6));
            ValidateDescriptor(row, descriptor);
            if (!_handlers.TryAdd(
                (descriptor.Id, descriptor.SubId), descriptor))
            {
                throw new InvalidOperationException(
                    $"Duplicate enemy handler ${descriptor.Id:x2}:" +
                    $"${descriptor.SubId:x2}.");
            }
        }

    }

    internal EnemyHandlerRegistry(string assetRoot, IEnumerable<IReadOnlyList<RoomObjectRecord>> roomObjectGroups)
        : this(assetRoot)
    {
        var usedKeys = new HashSet<(int Id, int SubId)>();
        foreach (IReadOnlyList<RoomObjectRecord> roomObjects in roomObjectGroups)
        {
            foreach (RoomObjectRecord source in roomObjects)
            {
                if (!IsEnemyPlacement(source.Kind))
                    continue;
                EnemyHandlerDescriptor descriptor = ResolveHandler(source);
                usedKeys.Add((descriptor.Id, descriptor.SubId));
            }
        }
        var nativeSpawns = GeneratedTable.Load(assetRoot + "objects/native_enemy_spawns.tsv",
            new GeneratedTableSchema("native enemy creation", GeneratedTableKeySemantics.Unique,
                ["id", "subid", "source"], ["id", "subid"], headerRequired: true));
        foreach (var row in nativeSpawns.Rows)
        {
            var descriptor = ResolveHandler(row.HexByte(0), row.HexByte(1), row.RequiredString(2));
            if (!descriptor.SupportsOrderedConstruction) throw row.Invalid(0, "an implemented native enemy handler");
            usedKeys.Add((descriptor.Id, descriptor.SubId));
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

    internal EnemyHandlerDescriptor ResolveHandler(
        int id,
        int subId,
        string source)
    {
        if (!_handlers.TryGetValue(
            (id, subId), out EnemyHandlerDescriptor? descriptor))
        {
            throw new InvalidOperationException(
                $"{source} has no handler classification for " +
                $"${id:x2}:${subId:x2}.");
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
        "unimplemented" => EnemyHandlerClassification.Unimplemented,
        _ => throw row.Invalid(
            column,
            "ordered-implemented, dynamic-special, deliberately-unsupported, or unimplemented")
    };

    private static EnemyHandlerKind ParseHandler(
        GeneratedTableRow row,
        int column) => row.RequiredString(column) switch
    {
        "-" => EnemyHandlerKind.None,
        "veran-final" => EnemyHandlerKind.VeranFinal,
        "twinrova" => EnemyHandlerKind.Twinrova,
        "octorok" => EnemyHandlerKind.Octorok,
        "river-zora" => EnemyHandlerKind.RiverZora,
        "goponga-flower" => EnemyHandlerKind.GopongaFlower,
        "buzzblob" => EnemyHandlerKind.BuzzBlob,
        "boomerang-moblin" => EnemyHandlerKind.BoomerangMoblin,
        "leever" => EnemyHandlerKind.Leever,
        "arrow-moblin" or "masked-moblin" => EnemyHandlerKind.ArrowMoblin,
        "arrow-darknut" => EnemyHandlerKind.ArrowDarknut,
        "lynel" => EnemyHandlerKind.Lynel,
        "podoboo-tower" => EnemyHandlerKind.PodobooTower,
        "rope" => EnemyHandlerKind.Rope,
        "beetle" => EnemyHandlerKind.Beetle,
        "blade-trap" => EnemyHandlerKind.BladeTrap,
        "iron-mask" => EnemyHandlerKind.IronMask,
        "pincer" => EnemyHandlerKind.Pincer,
        "giant-blade-trap" => EnemyHandlerKind.GiantBladeTrap,
        "bubble" => EnemyHandlerKind.Bubble,
        "bari" => EnemyHandlerKind.Bari,
        "floormaster" => EnemyHandlerKind.Floormaster,
        "wizzrobe" => EnemyHandlerKind.Wizzrobe,
        "candle" => EnemyHandlerKind.Candle,
        "gibdo" => EnemyHandlerKind.Gibdo,
        "like-like" => EnemyHandlerKind.LikeLike,
        "ball-chain-soldier" => EnemyHandlerKind.BallChainSoldier,
        "fire-keese" => EnemyHandlerKind.FireKeese,
        "pols-voice" => EnemyHandlerKind.PolsVoice,
        "moldorm" => EnemyHandlerKind.Moldorm,
        "spark" => EnemyHandlerKind.Spark,
        "whisp" => EnemyHandlerKind.Whisp,
        "sand-crab" => EnemyHandlerKind.SandCrab,
        "cheep-cheep" => EnemyHandlerKind.CheepCheep,
        "thwomp" => EnemyHandlerKind.Thwomp,
        "tektite" => EnemyHandlerKind.Tektite,
        "water-tektite" => EnemyHandlerKind.WaterTektite,
        "peahat" => EnemyHandlerKind.Peahat,
        "color-changing-gel" => EnemyHandlerKind.ColorChangingGel,
        "sword-enemy" => EnemyHandlerKind.SwordEnemy,
        "ghini" => EnemyHandlerKind.Ghini,
        "spiked-beetle" => EnemyHandlerKind.SpikedBeetle,
        "spiny-beetle" => EnemyHandlerKind.SpinyBeetle,
        "wallmaster" => EnemyHandlerKind.Wallmaster,
        "stalfos" => EnemyHandlerKind.Stalfos,
        "keese" => EnemyHandlerKind.Keese,
        "baby-cucco" => EnemyHandlerKind.BabyCucco,
        "cucco" => EnemyHandlerKind.Cucco,
        "zol" => EnemyHandlerKind.Zol,
        "crow" => EnemyHandlerKind.Crow,
        "gel" => EnemyHandlerKind.Gel,
        "hardhat-beetle" => EnemyHandlerKind.HardhatBeetle,
        "arm-mimic" => EnemyHandlerKind.ArmMimic,
        "flying-tile" => EnemyHandlerKind.FlyingTile,
        "vine-sprout" => EnemyHandlerKind.VineSprout,
        "great-fairy" => EnemyHandlerKind.GreatFairy,
        "beamos" => EnemyHandlerKind.Beamos,
        "fireball-shooter" => EnemyHandlerKind.FireballShooter,
        "target-cart-crystal" => EnemyHandlerKind.TargetCartCrystal,
        "king-moblin" => EnemyHandlerKind.KingMoblin,
        "ambi-guard" => EnemyHandlerKind.AmbiGuard,
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
            EnemyHandlerClassification.DeliberatelyUnsupported or EnemyHandlerClassification.Unimplemented =>
                descriptor.Handler == EnemyHandlerKind.None,
            _ => false
        };
        if (!valid)
        {
            throw row.Invalid(
                4,
                $"a handler compatible with {descriptor.Classification}");
        }
    }
}

internal sealed record EnemyHandlerDescriptor(
    string AssetRoot,
    int Id,
    int SubId,
    int CollisionMode,
    EnemyHandlerClassification Classification,
    EnemyHandlerKind Handler,
    string EnemyName,
    string Source)
{
    internal bool SupportsOrderedConstruction =>
        Classification == EnemyHandlerClassification.OrderedImplemented;

    internal bool SupportsCombatSource =>
        Classification is (EnemyHandlerClassification.OrderedImplemented or EnemyHandlerClassification.DynamicSpecial) &&
        Handler is not (EnemyHandlerKind.FireballShooter or EnemyHandlerKind.Beamos or EnemyHandlerKind.VineSprout or EnemyHandlerKind.GreatFairy or EnemyHandlerKind.TargetCartCrystal or
            EnemyHandlerKind.BabyCucco or EnemyHandlerKind.Cucco);

    internal bool CompletesDungeonEnemyCount => SupportsOrderedConstruction;
    internal bool SupportsStandaloneConstruction => SupportsOrderedConstruction &&
        (SupportsCombatSource || Handler is EnemyHandlerKind.TargetCartCrystal or EnemyHandlerKind.GreatFairy or EnemyHandlerKind.Beamos);

    internal EnemyCombatSourceDescriptor CombatSource(
        RoomObjectRecord source,
        int killableEnemyIndex)
    {
        if (source.Id != Id || source.SubId != SubId)
        {
            throw new InvalidOperationException(
                $"{source.Source} is ${source.Id:x2}:${source.SubId:x2}, " +
                $"not the registered ${Id:x2}:${SubId:x2} combat source.");
        }
        return CombatSource(
            source.Flags, killableEnemyIndex, source.Source);
    }

    internal EnemyCombatSourceDescriptor CombatSource(
        int objectFlags,
        int killableEnemyIndex,
        string source)
    {
        if (!SupportsCombatSource)
        {
            throw new InvalidOperationException(
                $"{source} cannot construct non-combat or unsupported " +
                $"{EnemyName} ${Id:x2}:${SubId:x2} combat.");
        }
        if (objectFlags is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(objectFlags));
        if (killableEnemyIndex is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(killableEnemyIndex));

        return new EnemyCombatSourceDescriptor(
            AssetRoot,
            Id,
            SubId,
            CollisionMode,
            objectFlags,
            killableEnemyIndex,
            source);
    }
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
    DeliberatelyUnsupported,
    Unimplemented
}

internal enum EnemyHandlerKind
{
    VeranFinal,
    Twinrova,
    AmbiGuard,
    KingMoblin,
    Beetle,
    None,
    Octorok,
    RiverZora,
    GopongaFlower,
    BuzzBlob,
    BoomerangMoblin,
    Leever,
    ArrowMoblin,
    ArrowDarknut,
    Lynel,
    PodobooTower,
    Rope,
    BladeTrap,
    IronMask,
    Pincer,
    Gibdo,
    LikeLike,
    BallChainSoldier,
    FireKeese,
    PolsVoice,
    Moldorm,
    Spark,
    Whisp,
    SandCrab,
    CheepCheep,
    Thwomp,
    Tektite,
    WaterTektite,
    GiantBladeTrap,
    Bubble,
    Bari,
    Floormaster,
    Wizzrobe,
    Candle,
    Peahat,
    ColorChangingGel,
    SwordEnemy,
    Ghini,
    SpikedBeetle,
    SpinyBeetle,
    Wallmaster,
    Stalfos,
    Keese,
    BabyCucco,
    Cucco,
    Zol,
    Crow,
    Gel,
    HardhatBeetle,
    ArmMimic,
    FlyingTile,
    VineSprout,
    GreatFairy,
    Beamos,
    FireballShooter,
    TargetCartCrystal,
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
