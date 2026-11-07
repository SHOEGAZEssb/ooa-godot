
namespace Oracles;

public sealed class EnemyDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<EnemyDatabase>> Packs = new(StringComparer.Ordinal);
    internal static EnemyDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<EnemyDatabase>(() => new EnemyDatabase(root))).Value;


    private readonly Lookup<int, RoomObjectRecord> _roomObjectsByRoom = new();
    private readonly Dictionary<int, StalfosRecord> _stalfosDefinitions = new();
    private readonly Dictionary<int, CrowRecord> _crowDefinitions = new();
    private readonly IReadOnlyDictionary<(int Id, int SubId), ImportedEnemyDefinition> _importedDefinitions;
    private readonly Dictionary<int, Color[]> _colorChangingGelPalettes = new();

    public int RoomObjectRecordCount { get; }
    public OctorokProjectileRecord OctorokProjectile => OctorokDatabase.For(AssetRoot).Projectile;
    internal EnemyProjectileVisualRecord MoblinBoomerang { get; }
    internal EnemyProjectileVisualRecord WizzrobeProjectile { get; }
    internal EnemyProjectileVisualRecord LynelBeam { get; }
    internal EnemyProjectileVisualRecord CandleFlame { get; }
    internal EnemyProjectileVisualRecord VireProjectile { get; }
    internal IReadOnlyDictionary<int, Color[]> ColorChangingGelPalettes =>
        _colorChangingGelPalettes;
    internal EnemyHandlerRegistry EnemyHandlers { get; }
    internal VineSproutDatabase VineSprouts { get; } = new();
    private readonly Dictionary<int, string> _cukemanTexts = new();
    internal string LikeLikeShieldText { get; }
    internal string SmogIntroText { get; }
    internal string CukemanText(int id) => _cukemanTexts.TryGetValue(id, out string? text)
        ? text : throw new InvalidOperationException($"Missing Cukeman text ${id:x4}.");

    public EnemyDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        _importedDefinitions = EnemyDefinitionDatabase.For(assetRoot).Definitions;

        var smogText = GeneratedTable.Load(AssetRoot + "objects/smog_intro_text.tsv",
            new GeneratedTableSchema("Smog intro", GeneratedTableKeySemantics.Unique,
                ["text-id", "text-base64", "source"], ["text-id"], headerRequired: true));
        if (smogText.Rows.Count != 1 || smogText.Rows[0].HexWord(0) != 0x2f26)
            throw new InvalidOperationException("smog_state_uninitialized requires TX_2f26.");
        SmogIntroText = smogText.Rows[0].Base64Utf8(1);
        var likeLikeText = GeneratedTable.Load(AssetRoot + "objects/like_like_text.tsv",
            new GeneratedTableSchema("Like Like shield loss", GeneratedTableKeySemantics.Unique,
                ["text-id", "text-base64", "source"], ["text-id"], headerRequired: true));
        if (likeLikeText.Rows.Count != 1 || likeLikeText.Rows[0].HexWord(0) != 0x510b)
            throw new InvalidOperationException("likelike_stateB requires TX_510b.");
        LikeLikeShieldText = likeLikeText.Rows[0].Base64Utf8(1);
        _ = likeLikeText.Rows[0].RequiredString(2);
        GeneratedTable texts = GeneratedTable.Load(AssetRoot + "objects/cukeman_text.tsv",
            new GeneratedTableSchema("Cukeman dialogue", GeneratedTableKeySemantics.Unique,
                ["text-id", "text-base64", "source"], ["text-id"], headerRequired: true));
        foreach (GeneratedTableRow row in texts.Rows)
        {
            int id = row.HexWord(0);
            if (id is < 0x2f1e or > 0x2f25)
                throw row.Invalid(0, "TX_2f1e through TX_2f25");
            _cukemanTexts.Add(id, row.Base64Utf8(1));
            _ = row.RequiredString(2);
        }
        if (_cukemanTexts.Count != 8)
            throw new InvalidOperationException("Cukeman requires TX_2f1e through TX_2f25.");
        GeneratedTable table;
        if (_importedDefinitions.Count != 105 ||
            ImportedEnemy(EnemyId.LikeLike) is not
                { Health: 5, DamageQuarters: 2, RadiusY: 6, RadiusX: 6,
                    TileBase: 12, Palette: 3, Animations.Length: 2 } ||
            ImportedEnemy(EnemyId.BoomerangMoblin) is not
                { Health: 3, DamageQuarters: 2, Animations.Length: 4 } ||
            ImportedEnemy(EnemyId.Leever) is not
                {
                    TileBase: 14,
                    Palette: 2,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 3,
                    DamageQuarters: 2,
                    Animations.Length: 3
                } ||
            ImportedEnemy(EnemyId.ArrowMoblin) is not
                { Health: 3, DamageQuarters: 2, Animations.Length: 4 } ||
            ImportedEnemy(EnemyId.Rope) is not { Health: 2, DamageQuarters: 2 } ||
            ImportedEnemy(EnemyId.FlyingTile, 0x00) is not
                {
                    TileBase: 22,
                    Palette: 5,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 2,
                    DamageQuarters: 2
                } ||
            ImportedEnemy(EnemyId.FlyingTile, 0x02) is not { Health: 2 } ||
            ImportedEnemy(EnemyId.SpikedBeetle) is not
                {
                    TileBase: 8,
                    Palette: 1,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 2,
                    DamageQuarters: 2,
                    Animations.Length: 2
                } ||
            ImportedEnemy(EnemyId.Ghini) is not { Health: 10, DamageQuarters: 2 } ||
            ImportedEnemy(EnemyId.Armos) is not
                {
                    TileBase: 18,
                    Palette: 0,
                    RadiusY: 7,
                    RadiusX: 7,
                    Health: 4,
                    DamageQuarters: 4,
                    Animations.Length: 1
                } ||
            ImportedEnemy(EnemyId.SandCrab) is not
                {
                    TileBase: 0,
                    Palette: 3,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 2,
                    DamageQuarters: 2,
                    Animations.Length: 1
                } ||
            ImportedEnemy(EnemyId.SpinyBeetle, 0x01) is not
                { Health: 2, DamageQuarters: 2, Animations.Length: 1 } ||
            ImportedEnemy(EnemyId.PolsVoice) is not
                {
                    TileBase: 0,
                    Palette: 3,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 3,
                    DamageQuarters: 2,
                    Animations.Length: 5
                } ||
            ImportedEnemy(EnemyId.Wallmaster) is not { Health: 5, DamageQuarters: 2 } ||
            ImportedEnemy(EnemyId.BabyCucco) is not
                {
                    TileBase: 14,
                    Palette: 3,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 127,
                    DamageQuarters: 128,
                    Animations.Length: 2
                } ||
            ImportedEnemy(EnemyId.Cucco) is not
                {
                    TileBase: 0,
                    Palette: 2,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 32,
                    DamageQuarters: 128,
                    Animations.Length: 2
                } ||
            ImportedEnemy(EnemyId.GiantCucco) is not
                {
                    TileBase: 0,
                    Palette: 2,
                    RadiusY: 7,
                    RadiusX: 12,
                    Health: 2,
                    DamageQuarters: 2,
                    Animations.Length: 2
                } ||
            ImportedEnemy(EnemyId.HardhatBeetle) is not
                { Health: 4, DamageQuarters: 2, Animations.Length: 1 } ||
            ImportedEnemy(EnemyId.ArmMimic) is not
                {
                    TileBase: 0,
                    Palette: 3,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 5,
                    DamageQuarters: 4,
                    Animations.Length: 4
                } ||
            ImportedEnemy(EnemyId.Moldorm) is not
                {
                    TileBase: 14,
                    Palette: 0,
                    RadiusY: 6,
                    RadiusX: 6,
                    Health: 8,
                    DamageQuarters: 2,
                    Animations.Length: 10
                })
        {
            throw new InvalidOperationException(
                "Imported common-enemy contract is incomplete.");
        }

        var beamPoses = GeneratedTable.Load(AssetRoot + "effects/lynel_beam.tsv",
            new GeneratedTableSchema("PART_LYNEL_BEAM $1b", GeneratedTableKeySemantics.Unique,
                ["animation-index", "sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "source"],
                ["animation-index"], headerRequired: true));
        if (beamPoses.Rows.Count != 4) throw new InvalidOperationException("PART$1b requires four directional poses.");
        var firstBeam = beamPoses.Rows[0];
        string[] beamAnimations = new string[4];
        for (int i = 0; i < 4; i++)
        {
            var row = beamPoses.Rows[i];
            if (row.UnsignedDecimal(0) != i || row.RequiredString(1) != firstBeam.RequiredString(1) ||
                row.UnsignedDecimal(2) != 26 || row.UnsignedDecimal(3) != 5 || row.Boolean01(4) != firstBeam.Boolean01(4))
                throw row.Invalid(0, "ordered PART$1b directional pose with source graphics");
            beamAnimations[i] = row.RequiredString(5);
        }
        LynelBeam = new(AssetRoot, [firstBeam.RequiredString(1)], 26, 5, firstBeam.Boolean01(4), beamAnimations);
        var wizzrobePoses = GeneratedTable.Load(AssetRoot + "effects/wizzrobe_projectile.tsv",
            new GeneratedTableSchema("PART_WIZZROBE_PROJECTILE $1f", GeneratedTableKeySemantics.Unique,
                ["animation-index", "sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "source"],
                ["animation-index"], headerRequired: true));
        if (wizzrobePoses.Rows.Count != 4) throw new InvalidOperationException("PART$1f requires four directional poses.");
        var firstPose = wizzrobePoses.Rows[0];
        string[] poses = new string[4];
        for (int i = 0; i < poses.Length; i++)
        {
            var pose = wizzrobePoses.Rows[i];
            if (pose.UnsignedDecimal(0) != i || pose.RequiredString(1) != firstPose.RequiredString(1) ||
                pose.UnsignedDecimal(2) != firstPose.UnsignedDecimal(2) || pose.UnsignedDecimal(3) != firstPose.UnsignedDecimal(3) ||
                pose.Boolean01(4) != firstPose.Boolean01(4)) throw pose.Invalid(0, "ordered PART$1f directional pose with matching graphics");
            poses[i] = pose.RequiredString(5);
        }
        WizzrobeProjectile = new(AssetRoot, [firstPose.RequiredString(1)], firstPose.UnsignedDecimal(2),
            firstPose.UnsignedDecimal(3), firstPose.Boolean01(4), poses);
        var candleFlame = GeneratedTable.Load(AssetRoot + "effects/candle_flame.tsv",
            new GeneratedTableSchema("PART_CANDLE_FLAME $36", GeneratedTableKeySemantics.Unique,
                ["animation-index", "sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "source"],
                ["animation-index"], headerRequired: true));
        if (candleFlame.Rows.Count != 2) throw new InvalidOperationException("PART$36 requires two animations.");
        string[] flameAnimations = new string[2];
        var firstFlame = candleFlame.Rows[0];
        for (int i = 0; i < 2; i++)
        {
            var row = candleFlame.Rows[i];
            if (row.UnsignedDecimal(0) != i || row.RequiredString(1) != firstFlame.RequiredString(1) ||
                row.UnsignedDecimal(2) != firstFlame.UnsignedDecimal(2) || row.UnsignedDecimal(3) != firstFlame.UnsignedDecimal(3) ||
                row.Boolean01(4) != firstFlame.Boolean01(4)) throw row.Invalid(0, "ordered PART$36 poses with matching graphics");
            flameAnimations[i] = row.RequiredString(5);
        }
        CandleFlame = new(AssetRoot, [firstFlame.RequiredString(1)], firstFlame.UnsignedDecimal(2), firstFlame.UnsignedDecimal(3),
            firstFlame.Boolean01(4), flameAnimations);

        var vireShots = GeneratedTable.Load(AssetRoot + "effects/vire_projectile.tsv",
            new GeneratedTableSchema("PART_VIRE_PROJECTILE $3a", GeneratedTableKeySemantics.Unique,
                ["animation-index", "sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "source"],
                ["animation-index"], headerRequired: true));
        if (vireShots.Rows.Count != 2) throw new InvalidOperationException("PART$3a requires two animations, including part1c fallthrough.");
        var firstShot = vireShots.Rows[0];
        string[] shotAnimations = new string[2];
        for (int i = 0; i < 2; i++)
        {
            var row = vireShots.Rows[i];
            if (row.UnsignedDecimal(0) != i || row.RequiredString(1) != firstShot.RequiredString(1) ||
                row.UnsignedDecimal(2) != firstShot.UnsignedDecimal(2) || row.UnsignedDecimal(3) != firstShot.UnsignedDecimal(3) ||
                row.Boolean01(4) != firstShot.Boolean01(4)) throw row.Invalid(0, "ordered PART$3a animations with matching graphics");
            shotAnimations[i] = row.RequiredString(5);
        }
        VireProjectile = new(AssetRoot, [firstShot.RequiredString(1)], firstShot.UnsignedDecimal(2), firstShot.UnsignedDecimal(3),
            firstShot.Boolean01(4), shotAnimations);

        Color[,] colorGelPalette = OracleGraphicsData.LoadPalette(
            AssetRoot + "objects/color_changing_gel_palette.bin",
            1,
            6);
        var colorGelPalette6 = new Color[4];
        for (int shade = 0; shade < colorGelPalette6.Length; shade++)
            colorGelPalette6[shade] = colorGelPalette[6, shade];
        _colorChangingGelPalettes.Add(6, colorGelPalette6);

        table = GeneratedTable.Load(
            AssetRoot + "effects/moblin_boomerang.tsv",
            new GeneratedTableSchema(
                "Moblin boomerang",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "animations-base64"
                ],
                headerRequired: true));
        GeneratedTableRow boomerang = table.SingleRow();
        MoblinBoomerang = new EnemyProjectileVisualRecord(AssetRoot,
            boomerang.SplitRequired(0, ','),
            boomerang.UnsignedDecimal(1),
            boomerang.UnsignedDecimal(2),
            boomerang.Boolean01(3),
            boomerang.EncodedAnimations(4));
        if (MoblinBoomerang is not
            {
                Sprites: ["spr_projectiles_1"],
                TileBase: 10,
                Palette: 4,
                SourceGrayscaleInverted: true,
                Animations.Length: 1
            })
        {
            throw new InvalidOperationException(
                "Imported PART_MOBLIN_BOOMERANG $21 visual is incomplete.");
        }

        table = GeneratedTable.Load(
            AssetRoot + "objects/crows.tsv",
            new GeneratedTableSchema(
                "Crow definitions",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "sprite", "tile-base", "palette", "radius-y",
                    "radius-x", "damage-quarters", "health", "speed-raw",
                    "perched-right", "perched-left", "flight-right", "flight-left"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            CrowRecord definition = new CrowRecord(AssetRoot,
                row.HexByte(0),
                row.HexByte(1),
                row.RequiredString(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                row.RequiredString(10),
                row.RequiredString(11),
                row.RequiredString(12),
                row.RequiredString(13));
            if (definition.Id != EnemyId.Crow ||
                !_crowDefinitions.TryAdd(definition.SubId, definition))
            {
                throw row.Invalid(0, "unique ENEMY_CROW $41 definition");
            }
        }
        if (_crowDefinitions.Count != 2 ||
            !_crowDefinitions.ContainsKey(0) || !_crowDefinitions.ContainsKey(1))
        {
            throw new InvalidOperationException(
                "Expected the ENEMY_CROW $41:$00/$01 definitions.");
        }

        table = GeneratedTable.Load(
            AssetRoot + "objects/stalfos.tsv",
            new GeneratedTableSchema(
                "Stalfos definitions",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "sprite", "tile-base", "palette", "radius-y",
                    "radius-x", "damage-quarters", "health", "speed-raw",
                    "walk-animation", "jump-animation"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            StalfosRecord definition = new StalfosRecord(AssetRoot,
                row.HexByte(0),
                row.HexByte(1),
                row.RequiredString(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                row.RequiredString(10),
                row.RequiredString(11));
            if (definition.Id != EnemyId.Stalfos ||
                !_stalfosDefinitions.TryAdd(definition.SubId, definition))
            {
                throw row.Invalid(0, "unique ENEMY_STALFOS $31 definition");
            }
        }
        if (_stalfosDefinitions.Count != 2 ||
            !_stalfosDefinitions.ContainsKey(0) || !_stalfosDefinitions.ContainsKey(2))
        {
            throw new InvalidOperationException(
                "Expected ENEMY_STALFOS $31:$00/$02 definitions.");
        }

        int records = 0;
        foreach (RoomObjectRecord record in NativeObjectStreamDatabase.For(AssetRoot).RoomObjects())
        {
            var roomRecords = _roomObjectsByRoom.GetOrAdd(MakeKey(record.Group, record.Room));
            if (roomRecords.Count != record.Order)
                throw new InvalidOperationException($"{record.Source}: native placement order is not contiguous.");
            roomRecords.Add(record);
            records++;
        }
        RoomObjectRecordCount = records;

        EnemyHandlers = new EnemyHandlerRegistry(AssetRoot, _roomObjectsByRoom.Values);
        ValidateEnemyHandlerDefinitions();
    }

    public IReadOnlyList<RoomObjectRecord> GetRoomObjects(int group, int room)
    {
        return _roomObjectsByRoom.ValuesOrEmpty(MakeKey(group, room));
    }

    public bool TryGetKeeseDefinition(RoomObjectRecord source, out KeeseDefinition record) =>
        KeeseDatabase.For(AssetRoot).TryGetDefinition(source,out record);

    public bool TryGetOctorokDefinition(RoomObjectRecord source, out OctorokRecord record) =>
        OctorokDatabase.For(AssetRoot).TryGetDefinition(source, out record);

    public bool TryGetStalfosDefinition(RoomObjectRecord source, out StalfosRecord record)
    {
        record = default;
        return source.Id == EnemyId.Stalfos && _stalfosDefinitions.TryGetValue(source.SubId, out record);
    }

    public bool TryGetCrowDefinition(RoomObjectRecord source, out CrowRecord record)
    {
        record = default;
        return source.Id == EnemyId.Crow && _crowDefinitions.TryGetValue(source.SubId, out record);
    }

    internal ImportedEnemyDefinition ImportedEnemy(int id, int subId = 0) =>
        _importedDefinitions.TryGetValue((id, subId), out ImportedEnemyDefinition record)
            ? record
            : throw new KeyNotFoundException(
                $"Common enemy ${id:x2}:${subId:x2} was not imported.");

    internal bool TryGetImportedEnemyDefinition(
        RoomObjectRecord source,
        out ImportedEnemyDefinition record)
    {
        // ENEMY_AMBI_GUARD uses its sub-ID for patrol and hostility, sharing
        // the single enemyData/graphics definition for $54.
        int graphicsSubId = source.Id == 0x54 && (source.SubId & 0x7f) <= 0x0c
            ? 0
            : source.SubId;
        return _importedDefinitions.TryGetValue((source.Id, graphicsSubId), out record);
    }

    private void ValidateEnemyHandlerDefinitions()
    {
        var validated = new HashSet<(int Id, int SubId)>();
        foreach ((_, IReadOnlyList<RoomObjectRecord> roomObjects) in
            _roomObjectsByRoom)
        {
            foreach (RoomObjectRecord source in roomObjects)
            {
                if (source.Kind is not (
                    RoomObjectKind.RandomEnemy or
                    RoomObjectKind.FixedEnemy or
                    RoomObjectKind.ParameterEnemy))
                {
                    continue;
                }
                EnemyHandlerDescriptor descriptor =
                    EnemyHandlers.ResolveHandler(source);
                if (!validated.Add((descriptor.Id, descriptor.SubId)))
                    continue;

                bool valid = descriptor.Classification switch
                {
                    EnemyHandlerClassification.OrderedImplemented =>
                        HasOrderedHandlerDefinition(descriptor),
                    EnemyHandlerClassification.DynamicSpecial =>
                        descriptor.Handler ==
                            EnemyHandlerKind.MakuSproutMaskedMoblin &&
                        descriptor.Id == EnemyId.MaskedMoblin &&
                        descriptor.SubId == 0 &&
                        HasImportedDefinition(descriptor, EnemyId.MaskedMoblin),
                    EnemyHandlerClassification.DeliberatelyUnsupported =>
                        descriptor.Handler == EnemyHandlerKind.None,
                    _ => false
                };
                if (!valid)
                {
                    throw new InvalidOperationException(
                        $"{descriptor.Source} classifies {descriptor.EnemyName} " +
                        $"${descriptor.Id:x2}:${descriptor.SubId:x2} as " +
                        $"{descriptor.Classification}/{descriptor.Handler}, but " +
                        "its typed runtime definition is unavailable.");
                }
            }
        }
    }

    private bool HasOrderedHandlerDefinition(
        EnemyHandlerDescriptor descriptor) => descriptor.Handler switch
    {
        EnemyHandlerKind.Twinrova => descriptor.Id == 3 && descriptor.SubId == 0 && new TwinrovaDatabase(AssetRoot).Actor(3).Health == 3,
        EnemyHandlerKind.VeranFinal => descriptor.Id==2&&descriptor.SubId==0&&new VeranFinalDatabase(AssetRoot).Actor(2).Health==24,
        EnemyHandlerKind.AmbiGuard => descriptor.Id == 0x54 && _importedDefinitions.ContainsKey((0x54, 0)),
        EnemyHandlerKind.KingMoblin => descriptor.Id == AgesEnemyId.KingMoblin && descriptor.SubId == 0 &&
            new KingMoblinDatabase(AssetRoot).Actor(0x7f).Health == 6,
        EnemyHandlerKind.CheepCheep => HasImportedDefinition(descriptor, 0x2c),
        EnemyHandlerKind.TargetCartCrystal => descriptor.Id==AgesEnemyId.TargetCartCrystal && descriptor.SubId<12 && _importedDefinitions.ContainsKey((0x63,0)),
        EnemyHandlerKind.RiverZora => HasImportedDefinition(descriptor, 0x08),
        EnemyHandlerKind.GopongaFlower => HasImportedDefinition(descriptor, 0x25),
        EnemyHandlerKind.ArrowDarknut => HasImportedDefinition(descriptor, 0x21),
        EnemyHandlerKind.Lynel => HasImportedDefinition(descriptor, EnemyId.Lynel),
        EnemyHandlerKind.PodobooTower => HasImportedDefinition(descriptor, 0x2d),
        EnemyHandlerKind.BuzzBlob => HasImportedDefinition(descriptor, 0x18),
        EnemyHandlerKind.Octorok =>
            descriptor.Id == EnemyId.Octorok &&
            OctorokDatabase.For(AssetRoot).HasDefinition(descriptor.SubId),
        EnemyHandlerKind.BoomerangMoblin =>
            HasImportedDefinition(descriptor, 0x0a),
        EnemyHandlerKind.Leever =>
            HasImportedDefinition(descriptor, 0x0b),
        EnemyHandlerKind.ArrowMoblin =>
            descriptor.Id is EnemyId.ArrowMoblin or EnemyId.MaskedMoblin or EnemyId.ArrowShroudedStalfos &&
            _importedDefinitions.ContainsKey(
                (descriptor.Id, descriptor.SubId)),
        EnemyHandlerKind.Rope =>
            HasImportedDefinition(descriptor, 0x10),
        EnemyHandlerKind.BladeTrap =>
            HasImportedDefinition(descriptor, 0x0e),
        EnemyHandlerKind.IronMask => HasImportedDefinition(descriptor, 0x1c),
        EnemyHandlerKind.Pincer => HasImportedDefinition(descriptor,0x45),
        EnemyHandlerKind.Gibdo =>
            HasImportedDefinition(descriptor, 0x12),
        EnemyHandlerKind.LikeLike => HasImportedDefinition(descriptor, 0x24),
        EnemyHandlerKind.BallChainSoldier => HasImportedDefinition(descriptor, 0x4b),
        EnemyHandlerKind.FireKeese =>
            HasImportedDefinition(descriptor, 0x39),
        EnemyHandlerKind.Spark =>
            HasImportedDefinition(descriptor, 0x13),
        EnemyHandlerKind.Whisp =>
            HasImportedDefinition(descriptor, 0x19),
        EnemyHandlerKind.SandCrab =>
            HasImportedDefinition(descriptor, 0x1a),
        EnemyHandlerKind.PolsVoice =>
            HasImportedDefinition(descriptor, 0x23),
        EnemyHandlerKind.Moldorm =>
            HasImportedDefinition(descriptor, 0x4f),
        EnemyHandlerKind.Thwomp =>
            HasImportedDefinition(descriptor, 0x2f),
        EnemyHandlerKind.Tektite =>
            HasImportedDefinition(descriptor, 0x30),
        EnemyHandlerKind.WaterTektite => HasImportedDefinition(descriptor, 0x3a),
        EnemyHandlerKind.GiantBladeTrap => HasImportedDefinition(descriptor, 0x2a),
        EnemyHandlerKind.Bubble => HasImportedDefinition(descriptor, 0x15),
        EnemyHandlerKind.Bari => HasImportedDefinition(descriptor, 0x3c),
        EnemyHandlerKind.Floormaster => HasImportedDefinition(descriptor, 0x35),
        EnemyHandlerKind.Aquamentus => HasImportedDefinition(descriptor, 0x78),
        EnemyHandlerKind.Wizzrobe => HasImportedDefinition(descriptor, 0x40),
        EnemyHandlerKind.Candle => HasImportedDefinition(descriptor, 0x55),
        EnemyHandlerKind.Peahat =>
            HasImportedDefinition(descriptor, 0x3e),
        EnemyHandlerKind.ColorChangingGel =>
            HasImportedDefinition(descriptor, 0x47),
        EnemyHandlerKind.SwordEnemy =>
            descriptor.Id is EnemyId.SwordMoblin or EnemyId.SwordDarknut or EnemyId.SwordShroudedStalfos or EnemyId.SwordMaskedMoblin &&
            _importedDefinitions.ContainsKey(
                (descriptor.Id, descriptor.SubId)),
        EnemyHandlerKind.Ghini =>
            HasImportedDefinition(descriptor, 0x17),
        EnemyHandlerKind.SpikedBeetle =>
            HasImportedDefinition(descriptor, 0x14),
        EnemyHandlerKind.SpinyBeetle =>
            HasImportedDefinition(descriptor, 0x1b),
        EnemyHandlerKind.Wallmaster =>
            HasImportedDefinition(descriptor, 0x28),
        EnemyHandlerKind.Stalfos =>
            descriptor.Id == EnemyId.Stalfos &&
            _stalfosDefinitions.ContainsKey(descriptor.SubId),
        EnemyHandlerKind.Keese =>
            descriptor.Id == EnemyId.Keese &&
            KeeseDatabase.For(AssetRoot).SupportsSubId(descriptor.SubId),
        EnemyHandlerKind.BabyCucco =>
            HasImportedDefinition(descriptor, 0x33),
        EnemyHandlerKind.Cucco =>
            HasImportedDefinition(descriptor, 0x36),
        EnemyHandlerKind.GreatFairy =>
            HasImportedDefinition(descriptor, 0x38),
        EnemyHandlerKind.Beamos =>
            HasImportedDefinition(descriptor, 0x16),
        EnemyHandlerKind.FireballShooter => descriptor.Id == EnemyId.FireballShooter && descriptor.SubId is 0 or 1,
        EnemyHandlerKind.Zol =>
            descriptor.Id == EnemyId.Zol &&
            _importedDefinitions.ContainsKey((descriptor.Id, descriptor.SubId)),
        EnemyHandlerKind.Crow =>
            descriptor.Id == EnemyId.Crow &&
            _crowDefinitions.ContainsKey(descriptor.SubId),
        EnemyHandlerKind.VineSprout =>
            descriptor.Id == AgesEnemyId.VineSprout && VineSprouts.HasSubId(descriptor.SubId),
        EnemyHandlerKind.Gel =>
            descriptor.Id == EnemyId.Gel && descriptor.SubId == 0,
        EnemyHandlerKind.HardhatBeetle =>
            HasImportedDefinition(descriptor, 0x4d) || HasImportedDefinition(descriptor, 0x5f),
        EnemyHandlerKind.ArmMimic =>
            HasImportedDefinition(descriptor, 0x4e),
        EnemyHandlerKind.FlyingTile =>
            HasImportedDefinition(descriptor, 0x52),
        _ => false
    };

    private bool HasImportedDefinition(
        EnemyHandlerDescriptor descriptor,
        int expectedId) =>
        descriptor.Id == expectedId &&
        _importedDefinitions.ContainsKey((descriptor.Id, descriptor.SubId));

    private static int MakeKey(int group, int room) => (group << 8) | room;

}

public readonly record struct CrowRecord(string AssetRoot, int Id, int SubId, string SpriteName, int TileBase, int Palette, int CollisionRadiusY, int CollisionRadiusX, int DamageQuarters, int Health, int SpeedRaw, string PerchedRightAnimation, string PerchedLeftAnimation, string FlightRightAnimation, string FlightLeftAnimation);






internal readonly record struct EnemyProjectileVisualRecord(string AssetRoot, string[] Sprites, int TileBase, int Palette, bool SourceGrayscaleInverted, string[] Animations);
