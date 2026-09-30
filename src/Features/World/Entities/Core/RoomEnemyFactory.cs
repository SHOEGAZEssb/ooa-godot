using Godot;
using System;
using System.Linq;

namespace oracleofages;

// Constructs the imported enemy handler selected by the ordered room parser or
// a native allocation. Slot admission, placement buffers and RNG order belong
// to the manager and room parser, never to a second placement pass.
internal sealed class RoomEnemyFactory(RoomEntityManager owner, EnemyDatabase enemies,
    RoomEntityResources resources, RoomSession? rooms)
{
    private readonly OracleRandom _random = owner.Random;
    private readonly OracleRuntimeState _runtimeState = owner.RuntimeState;
    private readonly OracleSaveData? _saveData = owner.SaveData;
    private readonly Func<long> _animationTick = owner.AnimationTick;

    internal IRoomEntity? CreateOrderedEnemy(
        EnemyHandlerDescriptor handler,
        RoomObjectRecord source,
        OracleRoomData room,
        Vector2 position,
        int instance,
        int killableEnemyIndex,
        EnemyPlacementContext placementContext, int replacementZHigh = 0)
    {
        if (!handler.SupportsOrderedConstruction)
            return null;

        if(handler.Handler==EnemyHandlerKind.TargetCartCrystal)
        {
            var visual=enemies.ImportedEnemy(EnemyId.TargetCartCrystal);
            string animation=visual.Animations[0];
            var actor = NpcCharacter.CreateFromRecord(new(rooms!.ActiveGroup,room.Id,InteractionId.Accessory,source.SubId,0,0,0,0,
                visual.Sprites[0],visual.TileBase,visual.Palette,0,false,animation,animation,animation,animation,"",
                NpcImplementationClassification.EventOwned));
            actor.SetSourceGrayscaleInverted(visual.SourceGrayscaleInverted);
            return new TargetCartCrystalRoomEntity(actor,source.SubId,_runtimeState,new GoronCaveDatabase(),owner.OnSoundRequested);
        }

        if (handler.Handler == EnemyHandlerKind.GreatFairy)
        {
            return new FountainFairyRoomEntity(enemies.ImportedEnemy(EnemyId.GreatFairy),
                resources.FountainFairies, position, owner.OnSoundRequested, owner.OnRoomEntityDialogueRequested,
                () => owner.OnSoundRequested(SoundId.MusFairyFountain), owner.ReadDisplayedHealth);
        }

        if (handler.Handler == EnemyHandlerKind.FireballShooter)
            return new FireballShooterRoomEntity(room, position, source.SubId, source.Var03,
                _random, owner.CanAllocateEnemies, () => owner.PartSlotAvailable, () => owner.RoomEnemyCount);

        if (handler.Handler == EnemyHandlerKind.Beamos)
        {
            var beamos = new BeamosCharacter { Name = $"Beamos_16_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
            beamos.Initialize(enemies.ImportedEnemy(EnemyId.Beamos), room, position, _random,
                owner.OnSoundRequested, () => owner.PartSlotAvailable, _animationTick);
            return new BeamosRoomEntity(beamos, (source.Flags & 2) == 0);
        }

        if (handler.Handler == EnemyHandlerKind.VineSprout)
        {
            if (_saveData is null)
            {
                throw new InvalidOperationException(
                    $"{source.Source} cannot create ENEMY_VINE_SPROUT " +
                    "without live save state.");
            }
            return new VineSproutRoomEntity(
                enemies.VineSprouts,
                enemies.VineSprouts.Record(source.SubId),
                room,
                _saveData,
                position,
                owner.OnSoundRequested,
                owner.OnRoomTileChanged,
                _animationTick);
        }
        if (handler.Handler == EnemyHandlerKind.BabyCucco)
        {
            if (!enemies.TryGetImportedEnemyDefinition(
                source, out ImportedEnemyDefinition babyCuccoRecord))
            {
                throw MissingEnemyDefinition(handler, source);
            }
            var babyCucco = new BabyCuccoCharacter
            {
                Name = $"BabyCucco_{source.Order}_{instance}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            babyCucco.Initialize(
                babyCuccoRecord,
                room,
                position,
                _random,
                resources.Bracelet.Data,
                resources.Bomb.Data,
                owner.OnSoundRequested,
                owner.ApplyThrownObjectHit);
            return new BabyCuccoRoomEntity(babyCucco);
        }
        if (handler.Handler == EnemyHandlerKind.Cucco)
        {
            if (!enemies.TryGetImportedEnemyDefinition(
                source, out ImportedEnemyDefinition cuccoRecord))
            {
                throw MissingEnemyDefinition(handler, source);
            }
            var cucco = new CuccoCharacter
            {
                Name = $"Cucco_{source.Order}_{instance}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            cucco.Initialize(
                cuccoRecord,
                enemies.ImportedEnemy(
                    EnemyBehaviorTables.Shared.Cucco.GiantReplacementId),
                room,
                position,
                _random,
                resources.Bracelet.Data,
                resources.Bomb.Data,
                owner.OnSoundRequested,
                owner.BeginScreenShake,
                owner.ApplyThrownObjectHit);
            return new CuccoRoomEntity(cucco);
        }

        EnemyCombatSourceDescriptor combatSource =
            handler.CombatSource(source, killableEnemyIndex);

        switch (handler.Handler)
        {
            case EnemyHandlerKind.KingMoblin:
                var king = new KingMoblinBoss();
                king.Initialize(new KingMoblinEnvironment(resources.KingMoblin,room,_random,_saveData,
                    owner.CanAllocateEnemies,() => owner.PartSlotAvailable,() => owner.InteractionSlotAvailable,owner.OnSoundRequested,
                    owner.BeginScreenShake,() => owner.ScreenIsShaking,owner.EnableLinkCollisionsAndMenu,
                    owner.OnRoomEntityDialogueRequested,owner.OnRoomWarpRequested,_animationTick,resources.Bracelet.Data,resources.Bomb.Data),position);
                return new KingMoblinRoomEntity(king);
            case EnemyHandlerKind.CheepCheep:
                var cheepCheep = new CheepCheepCharacter
                {
                    Name = $"CheepCheep_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                cheepCheep.Initialize(enemies.ImportedEnemy(source.Id, source.SubId),
                    room, position, source.Var03);
                return new CheepCheepRoomEntity(cheepCheep, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Keese:
                if (!enemies.TryGetKeeseDefinition(
                    source, out EnemyDatabaseEnemyRecord keeseRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var keese = new KeeseCharacter
                {
                    Name = $"Keese_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                keese.Initialize(keeseRecord, room, position, _random);
                return new KeeseRoomEntity(
                    keese, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Crow:
                if (!enemies.TryGetCrowDefinition(
                    source, out CrowRecord crowRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var crow = new CrowCharacter
                {
                    Name = $"Crow_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
                };
                crow.Initialize(crowRecord, room, position, _random);
                return new CrowRoomEntity(
                    crow, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Octorok:
                if (!enemies.TryGetOctorokDefinition(
                    source, out OctorokRecord octorokRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var octorok = new OctorokCharacter
                {
                    Name = $"Octorok_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                octorok.Initialize(octorokRecord, room, position, _random);
                return new OctorokRoomEntity(
                    octorok, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.RiverZora:
                var zora = new RiverZoraCharacter
                {
                    Name = $"RiverZora_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
                };
                zora.Initialize(enemies.ImportedEnemy(source.Id, source.SubId), room, position, _random);
                return new RiverZoraRoomEntity(zora, combatSource, owner.OnSoundRequested,
                    () => -owner.ToScreen(Vector2.Zero));

            case EnemyHandlerKind.GopongaFlower:
                var flower = new GopongaFlowerCharacter { Name = $"GopongaFlower_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex };
                flower.Initialize(enemies.ImportedEnemy(source.Id, source.SubId), position, _random);
                return new GopongaFlowerRoomEntity(flower, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.BuzzBlob:
                var buzzBlob = new BuzzBlobCharacter
                {
                    Name = $"BuzzBlob_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                buzzBlob.Initialize(enemies.ImportedEnemy(source.Id, source.SubId), room, position, _random);
                return new BuzzBlobRoomEntity(buzzBlob, combatSource, owner.OnSoundRequested,
                    (id, _, origin) => owner.OnRoomEntityDialogueRequested(id, enemies.CukemanText(id), origin));

            case EnemyHandlerKind.Gibdo:
                var gibdo = new GibdoCharacter { Name = $"Gibdo_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                gibdo.Initialize(enemies.ImportedEnemy(source.Id, source.SubId), room, position, _random);
                return new GibdoRoomEntity(gibdo, source, combatSource, owner.OnSoundRequested, () => owner.PartSlotAvailable, () => _random.Next().Value);

            case EnemyHandlerKind.LikeLike:
                var likeLike = new LikeLikeCharacter { Name = $"LikeLike_{source.Order}_{instance}" };
                likeLike.Initialize(enemies.ImportedEnemy(source.Id, source.SubId), room, position, _random);
                return new LikeLikeRoomEntity(likeLike, combatSource, owner.OnSoundRequested, () => owner.PartSlotAvailable,
                    () => _random.Next().Value, () => owner.OnRoomEntityDialogueRequested(0x510b, enemies.LikeLikeShieldText, likeLike.Position));

            case EnemyHandlerKind.BallChainSoldier:
                var soldier = new BallChainSoldierCharacter { Name = $"BallChain_{source.Order}_{instance}" };
                soldier.Initialize(enemies.ImportedEnemy(source.Id, source.SubId), room, position, _random);
                return new BallChainSoldierRoomEntity(soldier, combatSource, owner.OnSoundRequested, owner.CanAllocateEnemies, resources.SpikedBall);

            case EnemyHandlerKind.FireKeese:
                var fireKeese = new FireKeeseCharacter { Name = $"FireKeese_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                fireKeese.Initialize(enemies.ImportedEnemy(source.Id, source.SubId), room, position, _random);
                return new FireKeeseRoomEntity(fireKeese, combatSource, owner.OnSoundRequested, () => owner.PartSlotAvailable, () => _random.Next().Value);

            case EnemyHandlerKind.Stalfos:
                if (!enemies.TryGetStalfosDefinition(
                    source, out StalfosRecord stalfosRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var stalfos = new StalfosCharacter
                {
                    Name = $"Stalfos_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                stalfos.Initialize(stalfosRecord, room, position, _random, owner.OnSoundRequested, replacementZHigh);
                return new StalfosRoomEntity(
                    stalfos, combatSource, owner.OnSoundRequested, () => owner.PartSlotAvailable);

            case EnemyHandlerKind.Zol:
                if (!enemies.TryGetZolDefinition(
                    source, out ZolRecord zolRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var zol = new ZolCharacter
                {
                    Name = $"Zol_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                zol.Initialize(zolRecord, room, position, _random, owner.OnSoundRequested);
                return new ZolRoomEntity(
                    zol, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.BoomerangMoblin:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition moblinRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var moblin = new BoomerangMoblinCharacter
                {
                    Name = $"BoomerangMoblin_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                moblin.Initialize(moblinRecord, room, position, _random);
                return new BoomerangMoblinRoomEntity(
                    moblin, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Leever:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition leeverRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var leever = new LeeverCharacter
                {
                    Name = $"Leever_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                leever.Initialize(leeverRecord, room, position, _random);
                return new LeeverRoomEntity(
                    leever, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.ArrowDarknut:
                if (!enemies.TryGetImportedEnemyDefinition(source, out ImportedEnemyDefinition darknutRecord))
                    throw MissingEnemyDefinition(handler, source);
                var darknut = new ArrowDarknutCharacter { Name = $"ArrowDarknut_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                darknut.Initialize(darknutRecord, room, position, _random);
                return new ArrowDarknutRoomEntity(darknut, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.PodobooTower:
                if (!enemies.TryGetImportedEnemyDefinition(source, out ImportedEnemyDefinition towerRecord))
                    throw MissingEnemyDefinition(handler, source);
                var tower = new PodobooTowerCharacter { Name = $"PodobooTower_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                tower.Initialize(towerRecord, position, _random);
                return new PodobooTowerRoomEntity(tower, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.ArrowMoblin:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition arrowMoblinRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var arrowMoblin = new ArrowMoblinCharacter
                {
                    Name = $"ArrowMoblin_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                arrowMoblin.Initialize(
                    arrowMoblinRecord, room, position, _random);
                return new ArrowMoblinRoomEntity(
                    arrowMoblin, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.MaskedMoblin:
                var maskedMoblin = new MaskedMoblinCharacter
                {
                    Name =
                        $"MaskedMoblin_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                maskedMoblin.Initialize(
                    enemies.MaskedMoblin, room, position, _random);
                return new MaskedMoblinRoomEntity(
                    maskedMoblin, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Rope:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition ropeRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var rope = new RopeCharacter
                {
                    Name = $"Rope_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                rope.Initialize(ropeRecord, room, position, _random, owner.OnSoundRequested,
                    () => -(int)owner.ToScreen(Vector2.Zero).Y);
                return new RopeRoomEntity(
                    rope, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.BladeTrap:
                if (!enemies.TryGetImportedEnemyDefinition(source, out ImportedEnemyDefinition trapRecord))
                    throw MissingEnemyDefinition(handler, source);
                var trap = new BladeTrapCharacter { Name = $"BladeTrap_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                trap.Initialize(trapRecord, room, position, _random, owner.OnSoundRequested);
                return new BladeTrapRoomEntity(trap, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.PolsVoice:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition polsVoiceRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var polsVoice = new PolsVoiceCharacter
                {
                    Name = $"PolsVoice_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                polsVoice.Initialize(
                    polsVoiceRecord, room, position, _random);
                return new PolsVoiceRoomEntity(
                    polsVoice, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Moldorm:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition moldormRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var moldorm = new MoldormSpawnerCharacter
                {
                    Name = $"Moldorm_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                moldorm.Initialize(moldormRecord, enemies, room, position, _random, combatSource,
                    owner.CanAllocateEnemies, owner.KillMoldormRelatedParts, owner.OnSoundRequested);
                return new MoldormSpawnerRoomEntity(moldorm);

            case EnemyHandlerKind.Spark:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition sparkRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var spark = new SparkCharacter
                {
                    Name = $"Spark_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                spark.Initialize(sparkRecord, room, position);
                spark.ConfigureTransformation(owner.TryCreateTransformationPuff, owner.InteractionAnimationParameter, owner.TryCreateSparkFairy);
                return new SparkRoomEntity(
                    spark, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Whisp:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition whispRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var whisp = new WhispCharacter
                {
                    Name = $"Whisp_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                whisp.Initialize(whispRecord, room, position, _random);
                whisp.ConfigureTransformation(owner.TryCreateTransformationPuff, owner.InteractionAnimationParameter, owner.TryCreateSparkFairy);
                return new WhispRoomEntity(
                    whisp, combatSource, owner.OnSoundRequested, () => _random.Next().Value);

            case EnemyHandlerKind.SandCrab:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition sandCrabRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var sandCrab = new SandCrabCharacter
                {
                    Name = $"SandCrab_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                sandCrab.Initialize(
                    sandCrabRecord, room, position, _random);
                return new SandCrabRoomEntity(
                    sandCrab, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Thwomp:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition thwompRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var thwomp = new ThwompCharacter
                {
                    Name = $"Thwomp_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                thwomp.Initialize(thwompRecord, room, position);
                return new ThwompRoomEntity(
                    thwomp, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Peahat:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition peahatRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var peahat = new PeahatCharacter
                {
                    Name = $"Peahat_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                peahat.Initialize(peahatRecord, room, position, _random);
                return new PeahatRoomEntity(
                    peahat, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Tektite:
                if (!enemies.TryGetImportedEnemyDefinition(source, out ImportedEnemyDefinition tektiteRecord))
                    throw MissingEnemyDefinition(handler, source);
                var tektite = new TektiteCharacter { Name = $"Tektite_{source.Order}_{instance}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
                tektite.Initialize(tektiteRecord, room, position, _random, owner.OnSoundRequested);
                return new TektiteRoomEntity(tektite, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.ColorChangingGel:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition colorGelRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var colorGel = new ColorChangingGelCharacter
                {
                    Name =
                        $"ColorChangingGel_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                colorGel.Initialize(
                    colorGelRecord,
                    room,
                    position,
                    _random,
                    enemies.ColorChangingGelPalettes, owner.OnSoundRequested);
                return new ColorChangingGelRoomEntity(
                    colorGel, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.SwordEnemy:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition swordEnemyRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var swordEnemy = new SwordEnemyCharacter
                {
                    Name =
                        $"SwordEnemy_{source.Id:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                swordEnemy.Initialize(
                    swordEnemyRecord, room, position, _random);
                return new SwordEnemyRoomEntity(
                    swordEnemy, combatSource, owner.OnSoundRequested, () => owner.PartSlotAvailable, () => _random.Next().Value);

            case EnemyHandlerKind.Ghini:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition ghiniRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var ghini = new GhiniCharacter
                {
                    Name = $"Ghini_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
                };
                ghini.Initialize(ghiniRecord, room, position, _random);
                return new GhiniRoomEntity(
                    ghini, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.SpikedBeetle:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition spikedBeetleRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var spikedBeetle = new SpikedBeetleCharacter
                {
                    Name =
                        $"SpikedBeetle_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                spikedBeetle.Initialize(
                    spikedBeetleRecord,
                    room,
                    position,
                    _random,
                    owner.OnSoundRequested);
                return new SpikedBeetleRoomEntity(
                    spikedBeetle, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.SpinyBeetle:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition spinyBeetleRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var spinyBeetle = new SpinyBeetleCharacter
                {
                    Name =
                        $"SpinyBeetle_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
                };
                spinyBeetle.Initialize(
                    spinyBeetleRecord,
                    room,
                    position,
                    _random,
                    resources.Bracelet.Data,
                    resources.Bomb.Data,
                    owner.ApplyThrownObjectHit);
                return new SpinyBeetleRoomEntity(
                    spinyBeetle, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.Wallmaster:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition wallmasterRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var wallmaster = new WallmasterCharacter
                {
                    Name = $"Wallmaster_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
                };
                wallmaster.Initialize(
                    wallmasterRecord, room, position, source.Y);
                (int destinationGroup, int destinationRoom) =
                    ResolveWallmasterDestination(source);
                return new WallmasterRoomEntity(
                    wallmaster, owner.OnSoundRequested, owner.OnRoomWarpRequested,
                    source.Group, source.Room,
                    destinationGroup, destinationRoom,
                    combatSource);

            case EnemyHandlerKind.HardhatBeetle:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition hardhatBeetleRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var hardhatBeetle = new HardhatBeetleCharacter
                {
                    Name =
                        $"HardhatBeetle_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                hardhatBeetle.Initialize(
                    hardhatBeetleRecord, room, position);
                return new HardhatBeetleRoomEntity(
                    hardhatBeetle, combatSource, owner.OnSoundRequested, () =>
                    {
                        if (source.Id == 0x5f) owner.OnObjectFellInHole(ObjectFellInHoleKind.HarmlessHardhatBeetle);
                    });

            case EnemyHandlerKind.ArmMimic:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition armMimicRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var armMimic = new ArmMimicCharacter
                {
                    Name = $"ArmMimic_{source.Order}_{instance}",
                    ZIndex = ObjectDrawPriority.BehindLinkZIndex
                };
                armMimic.Initialize(
                    armMimicRecord,
                    room,
                    position,
                    placementContext.Kind is
                        EnemyPlacementEntryKind.Scrolling or
                        EnemyPlacementEntryKind.ScreenWarp
                            ? placementContext.ScrollDirection
                            : Vector2I.Zero);
                return new ArmMimicRoomEntity(
                    armMimic, combatSource, owner.OnSoundRequested);

            case EnemyHandlerKind.FlyingTile:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition flyingTileRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                return new FlyingTileSpawnerRoomEntity(
                    source.SubId,
                    flyingTileRecord,
                    combatSource.CountsAsEnemy);

            case EnemyHandlerKind.Gel:
                return CreateGel(
                    new GelSpawn(
                        position, $"RoomGel_{source.Order}_{instance}"),
                    room, combatSource);

            default:
                throw new InvalidOperationException(
                    $"{handler.Source} classified {handler.EnemyName} " +
                    $"${handler.Id:x2}:${handler.SubId:x2} as an ordered " +
                    $"handler, but '{handler.Handler}' has no factory path.");
        }
    }

    internal IRoomEntity? CreateStandaloneEnemy(
        int id, int subId, OracleRoomData room, Vector2 position, string sourceLabel, out string error)
    {
        string origin = $"{sourceLabel}: enemy ${id:x2}:${subId:x2}";
        EnemyHandlerDescriptor? handler = enemies.EnemyHandlers.Handlers
            .FirstOrDefault(value => value.Id == id && value.SubId == subId);
        if (handler is null || !handler.SupportsOrderedConstruction ||
            !handler.SupportsCombatSource && handler.Handler!=EnemyHandlerKind.TargetCartCrystal)
        {
            error = $"{origin} has no standalone combat factory. " +
                (handler?.Source ?? "No imported handler.");
            return null;
        }

        // Native allocation bypasses parseObjectData and its placement RNG.
        // objectLoading.s:decEnemyCounterIfApplicable uses flags bit $02;
        // index $00 also leaves the source room's recent-defeat bits alone.
        var source = new RoomObjectRecord(
            rooms!.ActiveGroup, room.Id, 0, RoomObjectKind.FixedEnemy,
            id, subId, 0x02, 1, (int)position.Y, (int)position.X, 0, 0xff)
        {
            SourceOverride = $"{origin} via {handler.Source}"
        };
        if (handler.Handler == EnemyHandlerKind.Wallmaster)
        {
            // Check room metadata before allocating a node or consuming RNG.
            try { _ = ResolveWallmasterDestination(source); }
            catch (InvalidOperationException exception)
            {
                error = $"{origin}: {exception.Message}";
                return null;
            }
        }
        error = string.Empty;
        return CreateOrderedEnemy(handler, source, room, position, 0, 0,
            EnemyPlacementContext.Unrestricted);
    }

    internal IRoomEntity CreateEnemyReplacement(RoomEnemyReplacement replacement, OracleRoomData room)
    {
        var source = replacement.Source;
        if (source.Id != 0x31 || source.SubId != 2)
            throw new InvalidOperationException($"{source.Source}: unsupported enemyReplaceWithID target ${source.Id:x2}:${source.SubId:x2}.");
        var handler = enemies.EnemyHandlers.ResolveHandler(source);
        return CreateOrderedEnemy(handler, source, room, new Vector2(source.X, source.Y), 0,
            replacement.KillableEnemyIndex, EnemyPlacementContext.Unrestricted, replacement.ZHigh)
            ?? throw MissingEnemyDefinition(handler, source);
    }

    private static InvalidOperationException MissingEnemyDefinition(
        EnemyHandlerDescriptor handler,
        RoomObjectRecord source) => new(
            $"{source.Source} resolves through {handler.Source} to " +
            $"'{handler.Handler}', but its typed definition is unavailable.");

    internal Vector2 ResolveVineSproutPosition(
        RoomObjectRecord source,
        OracleRoomData room)
    {
        if (_saveData is null)
        {
            throw new InvalidOperationException(
                $"{source.Source} cannot resolve wVinePositions without " +
                "live save state.");
        }
        return enemies.VineSprouts.ResolvePosition(
            source.SubId, room, _saveData);
    }

    internal IRoomEntity CreateGel(
        GelSpawn spawn,
        OracleRoomData room,
        EnemyCombatSourceDescriptor? combatSource = null)
    {
        var gel = new GelCharacter { Name = spawn.Name, ZIndex = ObjectDrawPriority.BehindLinkZIndex };
        gel.Initialize(enemies.Gel, room, spawn.Position, _random);
        EnemyCombatSourceDescriptor source = combatSource ??
            enemies.EnemyHandlers.ResolveHandler(
                enemies.Gel.Id,
                enemies.Gel.SubId,
                $"dynamic {spawn.Name} ENEMY_GEL")
            .CombatSource(
                objectFlags: 0,
                killableEnemyIndex: spawn.KillableEnemyIndex,
                source: $"dynamic {spawn.Name} ENEMY_GEL");
        return new GelRoomEntity(
            gel, source, owner.OnSoundRequested);
    }

    private (int Group, int Room) ResolveWallmasterDestination(
        RoomObjectRecord source)
    {
        int dungeon = rooms?.World.GetDungeonIndex(source.Group, source.Room) ?? -1;
        DungeonInfo info;
        if (dungeon >= 0)
        {
            info = resources.DungeonMaps.GetDungeon(dungeon);
        }
        else if (!resources.DungeonMaps.TryGetDungeonForRoom(
            source.Group, source.Room, out info))
        {
            throw new InvalidOperationException(
                $"Wallmaster room {source.Group:x1}:{source.Room:x2} has no " +
                "unambiguous imported dungeon metadata.");
        }
        if (info.Group != source.Group)
        {
            throw new InvalidOperationException(
                $"Wallmaster room {source.Group:x1}:{source.Room:x2} resolved " +
                $"dungeon ${info.Index:x2} in group ${info.Group:x1}.");
        }
        return (info.Group, info.WallmasterDestinationRoom);
    }
}
