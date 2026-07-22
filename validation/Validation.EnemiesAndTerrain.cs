using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyPlacementRules()
    {
        var spawnTiles = new EnemySpawnTileDatabase();
        var normal = new OracleRoomData.TerrainInfo(
            0x1a, 0x00, OracleRoomData.TerrainType.Normal,
            OracleRoomData.HazardType.None);
        var collision10Water = new OracleRoomData.TerrainInfo(
            0xfc, 0x10, OracleRoomData.TerrainType.SeaWater,
            OracleRoomData.HazardType.Water);
        var overworldWhirlpool = new OracleRoomData.TerrainInfo(
            0xe9, 0x00, OracleRoomData.TerrainType.Whirlpool,
            OracleRoomData.HazardType.Water);
        var dungeonException = new OracleRoomData.TerrainInfo(
            0x44, 0x00, OracleRoomData.TerrainType.Normal,
            OracleRoomData.HazardType.None);
        var sidescrollHoleIndex = new OracleRoomData.TerrainInfo(
            0xf3, 0x00, OracleRoomData.TerrainType.Normal,
            OracleRoomData.HazardType.None);
        if (spawnTiles.RecordCount != 63 ||
            !spawnTiles.IsValid(0, normal) ||
            spawnTiles.IsValid(0, collision10Water) ||
            spawnTiles.IsValid(0, overworldWhirlpool) ||
            spawnTiles.IsValid(2, dungeonException) ||
            !spawnTiles.IsValid(3, sidescrollHoleIndex))
        {
            throw new InvalidOperationException(
                "checkTileValidForEnemySpawn did not require collision `$00 or " +
                "select the original exception list by wActiveCollisions.");
        }

        OracleRoomData room1db = _world.LoadRoom(1, 0xdb);
        int validCells = 0;
        int rejectedWaterCells = 0;
        int centerOpenButRejected = 0;
        for (int tileY = 0; tileY < room1db.HeightInTiles; tileY++)
        for (int tileX = 0; tileX < room1db.WidthInTiles; tileX++)
        {
            Vector2 center = new(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            OracleRoomData.TerrainInfo terrain = room1db.GetTerrainInfo(center);
            bool valid = spawnTiles.IsValid(room1db.ActiveCollisions, terrain);
            if (valid)
                validCells++;
            if (terrain is { Tile: 0xfc, Collision: 0x10 })
            {
                if (valid)
                    throw new InvalidOperationException(
                        "Room 1:db deep water remained valid for random enemies.");
                rejectedWaterCells++;
            }
            if (!room1db.IsSolid(center) && !valid)
                centerOpenButRejected++;
        }
        if (validCells != 32 || rejectedWaterCells != 18 ||
            centerOpenButRejected != 19)
        {
            throw new InvalidOperationException(
                $"Room 1:db enemy-placement pool mismatch: valid={validCells}, " +
                $"water={rejectedWaterCells}, center-open rejected={centerOpenButRejected}.");
        }

        EnemyPlacementContext warp = EnemyPlacementContext.Warp(0x33);
        EnemyPlacementContext scrollUp = EnemyPlacementContext.Scrolling(Vector2I.Up);
        EnemyPlacementContext scrollRight = EnemyPlacementContext.Scrolling(Vector2I.Right);
        EnemyPlacementContext scrollDown = EnemyPlacementContext.Scrolling(Vector2I.Down);
        EnemyPlacementContext scrollLeft = EnemyPlacementContext.Scrolling(Vector2I.Left);
        OracleRoomData largeRoom = _world.LoadRoom(4, 0x39);
        if (warp.Allows(room1db, 0x11) || !warp.Allows(room1db, 0x03) ||
            scrollUp.Allows(room1db, 0x59) || !scrollUp.Allows(room1db, 0x49) ||
            scrollRight.Allows(room1db, 0x42) || !scrollRight.Allows(room1db, 0x43) ||
            scrollDown.Allows(room1db, 0x29) || !scrollDown.Allows(room1db, 0x39) ||
            scrollLeft.Allows(room1db, 0x47) || !scrollLeft.Allows(room1db, 0x46) ||
            scrollLeft.Allows(largeRoom, 0x5b) || !scrollLeft.Allows(largeRoom, 0x5a) ||
            EnemyPlacementContext.FromWarpDestination(0xf5).Allows(room1db, 0x59))
        {
            throw new InvalidOperationException(
                "checkPositionValidForEnemySpawn lost its warp square, incoming " +
                "three-tile edge, or large-room DIR_LEFT `$0b boundary.");
        }

        var validationRoot = new Node { Name = "EnemySpawnTerrainValidation" };
        AddChild(validationRoot);
        var enemies = new EnemyDatabase();
        for (int rngAdvance = 0; rngAdvance <= 24; rngAdvance++)
        {
            var random = new OracleRandom();
            for (int index = 0; index < rngAdvance; index++)
                random.Next();
            var manager = new RoomEntityManager(
                validationRoot, new NpcDatabase(), enemies,
                new ItemDropDatabase(), new TimePortalDatabase(), random);
            manager.LoadRoom(1, room1db);
            List<OctorokCharacter> octoroks = manager.Entities<OctorokCharacter>();
            if (octoroks.Count != 2 || octoroks.Any(octorok =>
                !spawnTiles.IsValid(
                    room1db.ActiveCollisions,
                    room1db.GetTerrainInfo(octorok.Position))))
            {
                throw new InvalidOperationException(
                    $"Room 1:db placed a blue Octorok on ROM-invalid terrain " +
                    $"after {rngAdvance} pre-parse RNG calls.");
            }
            manager.Clear();
        }
        RemoveChild(validationRoot);
        validationRoot.Free();

        GD.Print("Validated 63 enemy-unspawnable tile records, whole-metatile collision " +
            "checks, room 1:db's 32-cell land pool / 18 rejected water cells, 25 RNG " +
            "states, warp-distance exclusion, and all scrolling entry boundaries.");
    }

    private void ValidateEnemyObjectPlacementOrder()
    {
        var database = new EnemyDatabase();
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room5b0 =
            database.GetRoomObjects(5, 0xb0);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room5db =
            database.GetRoomObjects(5, 0xdb);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room501 =
            database.GetRoomObjects(5, 0x01);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room036 =
            database.GetRoomObjects(0, 0x36);
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room006 =
            database.GetRoomObjects(0, 0x06);
        if (database.RoomObjectRecordCount != 1141 ||
            room5b0.Count != 3 ||
            room5b0[0] is not { Order: 0, Kind: EnemyDatabase.RoomObjectKind.FixedEnemy,
                Id: 0x1b, PackedPosition: 0x63 } ||
            room5b0[1] is not { Order: 1, Kind: EnemyDatabase.RoomObjectKind.FixedEnemy,
                Id: 0x34, PackedPosition: 0x75 } ||
            room5b0[2] is not { Order: 2, Kind: EnemyDatabase.RoomObjectKind.RandomEnemy,
                Id: 0x32, Count: 2 } ||
            room5db.Count != 7 ||
            room5db.Take(4).Any(record =>
                record.Kind != EnemyDatabase.RoomObjectKind.ItemDrop) ||
            room5db[6] is not { Kind: EnemyDatabase.RoomObjectKind.RandomEnemy,
                Id: 0x32, Count: 3 } ||
            room501.Count != 3 ||
            room501[0].Kind != EnemyDatabase.RoomObjectKind.ReservingPart ||
            room501[1].Kind != EnemyDatabase.RoomObjectKind.ReservingPart ||
            room501[2].Kind != EnemyDatabase.RoomObjectKind.RandomEnemy ||
            room036.Count != 2 || room036[0].ConditionMask != 0x01 ||
            room036[1].ConditionMask != 0xff ||
            room006.Count != 1 || room006[0].Order != 0)
        {
            throw new InvalidOperationException(
                "The ordered room-object stream lost source order, aliases, conditions, " +
                "unsupported enemies, item drops, or reserving parts.");
        }

        var wrappedReservations = new EnemyPlacementReservations();
        for (int position = 0; position < 15; position++)
            wrappedReservations.Add(position);
        if (wrappedReservations.Count != 15 || !wrappedReservations.Contains(0x00) ||
            !wrappedReservations.Contains(0x0e))
        {
            throw new InvalidOperationException(
                "wPlacedEnemyPositions did not retain its first 15 packed positions.");
        }
        wrappedReservations.Add(0x0f);
        if (wrappedReservations.Count != 0 || wrappedReservations.Contains(0x00) ||
            wrappedReservations.Contains(0x0f))
        {
            throw new InvalidOperationException(
                "wEnemyPlacement.numEnemies did not wrap after its 16th reservation.");
        }
        wrappedReservations.Add(0xaa);
        if (wrappedReservations.Count != 1 || !wrappedReservations.Contains(0xaa) ||
            wrappedReservations.Contains(0x01))
        {
            throw new InvalidOperationException(
                "The wrapped placement table did not restart from entry zero.");
        }

        var validationRoot = new Node { Name = "EnemyPlacementOrderValidation" };
        AddChild(validationRoot);
        var npcs = new NpcDatabase();
        var itemDrops = new ItemDropDatabase();
        var timePortals = new TimePortalDatabase();

        static int Pack(Vector2 position) =>
            ((int)position.Y & 0xf0) | (((int)position.X >> 4) & 0x0f);

        void ValidateRoom(
            int group,
            int roomId,
            int rngAdvance,
            int expectedKeese,
            int expectedZols,
            params int[] reservedPositions)
        {
            var random = new OracleRandom();
            for (int index = 0; index < rngAdvance; index++)
                random.Next();
            var manager = new RoomEntityManager(
                validationRoot, npcs, database, itemDrops, timePortals, random);
            manager.LoadRoom(group, _world.LoadRoom(group, roomId));

            List<KeeseCharacter> keese = manager.Entities<KeeseCharacter>();
            List<ZolCharacter> zols = manager.Entities<ZolCharacter>();
            int[] allPacked = keese.Select(enemy => Pack(enemy.Position))
                .Concat(zols.Select(enemy => Pack(enemy.Position)))
                .ToArray();
            HashSet<int> reserved = reservedPositions.ToHashSet();
            if (keese.Count != expectedKeese || zols.Count != expectedZols ||
                allPacked.Distinct().Count() != allPacked.Length ||
                keese.Any(enemy => reserved.Contains(Pack(enemy.Position))))
            {
                throw new InvalidOperationException(
                    $"Room {group:x1}:{roomId:x2} lost ordered placement reservations " +
                    $"(Keese={string.Join(',', keese.Select(enemy => $"${Pack(enemy.Position):x2}"))}, " +
                    $"Zols={string.Join(',', zols.Select(enemy => $"${Pack(enemy.Position):x2}"))}).");
            }
            manager.Clear();
        }

        // These advances deliberately make the first shuffled candidate collide
        // with a reservation: fixed Zol $43, unsupported enemy $63, and item $1d.
        ValidateRoom(4, 0x9a, 11, 2, 2, 0x39, 0x43);
        ValidateRoom(5, 0xb0, 30, 2, 1, 0x63, 0x75);
        ValidateRoom(5, 0xdb, 16, 3, 2, 0x1d, 0x92, 0x82, 0x83, 0x73, 0x63);

        RemoveChild(validationRoot);
        validationRoot.Free();
        GD.Print("Validated 1,141 ordered room placement records, mid-stream aliases, " +
            "condition masks, 16-entry reservation wrapping, and fixed/unsupported/item " +
            "reservations before random Keese in rooms 4:9a, 5:b0, and 5:db.");
    }

    private void ValidateKeese()
    {
        var database = new EnemyDatabase();
        if (database.KeeseRecordCount != 53 || database.KeeseInstanceCount != 158)
            throw new InvalidOperationException(
                $"Expected 53 ENEMY_KEESE room records / 158 instances, got " +
                $"{database.KeeseRecordCount} / {database.KeeseInstanceCount}.");

        LoadValidationRoom(4, 0x39);
        if (_entities.Entities<KeeseCharacter>().Count != 2 || _entities.Entities<KeeseCharacter>().Exists(keese => keese.Record.SubId != 1))
            throw new InvalidOperationException(
                $"Room 4:39 should contain two random-position ENEMY_KEESE subid `$01 objects, " +
                $"got {_entities.Entities<KeeseCharacter>().Count}.");

        KeeseCharacter approachKeese = _entities.Entities<KeeseCharacter>()[0];
        if (approachKeese.SpriteHeight != -1)
            throw new InvalidOperationException("Keese subid `$01 did not preserve its original z-height `$ff.");
        approachKeese.Position = new Vector2(80, 80);
        _player.WarpTo(new Vector2(128, 80));
        _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.State != KeeseCharacter.KeeseState.Moving ||
            approachKeese.Counter1 != 12 || approachKeese.Counter2 != 12 ||
            !approachKeese.Flying)
            throw new InvalidOperationException(
                "Keese subid `$01 did not wake inside strict Manhattan distance `$31 with 12x12 counters.");

        for (int frame = 0; frame < 4; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.CurrentAnimationFrame != 1)
            throw new InvalidOperationException(
                "Flying Keese did not switch OAM frames after the original 4 animation calls.");
        for (int frame = 4; frame < 144; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.State != KeeseCharacter.KeeseState.Resting || approachKeese.Flying)
            throw new InvalidOperationException(
                "Keese subid `$01 did not return to rest after 12 turning intervals of 12 updates.");

        LoadValidationRoom(4, 0xcb);
        if (_entities.Entities<KeeseCharacter>().Count != 4 || _entities.Entities<KeeseCharacter>().Exists(keese => keese.Record.SubId != 0))
            throw new InvalidOperationException(
                $"Room 4:cb should contain four random-position ENEMY_KEESE subid `$00 objects, " +
                $"got {_entities.Entities<KeeseCharacter>().Count}.");

        KeeseCharacter normalKeese = _entities.Entities<KeeseCharacter>()[0];
        // Keep the target hitbox independent of the room-wide RNG stream.
        // Earlier exact-RNG interaction loads may legitimately place another
        // random Keese near the validation's synthetic $30/$30 target.
        for (int index = 1; index < _entities.Entities<KeeseCharacter>().Count; index++)
        {
            _entities.Entities<KeeseCharacter>()[index].Position =
                new Vector2(0xc0 + index * 8, 0x20 + index * 16);
        }
        normalKeese.Position = new Vector2(48, 48);
        _player.WarpTo(new Vector2(160, 120));
        _entities.Update(31.0 / 60.0, _player);
        if (normalKeese.State != KeeseCharacter.KeeseState.Resting || normalKeese.Counter1 != 1)
            throw new InvalidOperationException(
                "Normal Keese did not preserve its original 32-update initial rest counter.");
        _entities.Update(1.0 / 60.0, _player);
        if (normalKeese.State != KeeseCharacter.KeeseState.Moving || !normalKeese.Flying ||
            normalKeese.Counter1 is < 0xc0 or > 0xff)
            throw new InvalidOperationException(
                "Normal Keese did not choose its original random `$c0-`$ff flight counter and angle.");

        _player.RefillHealth();
        _player.WarpTo(normalKeese.Position, recordSafe: false);
        int healthBeforeContact = _player.HealthQuarters;
        int damageSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndDamageLink);
        _entities.Update(0.0, _player);
        if (_player.HealthQuarters != healthBeforeContact - 2 ||
            !Mathf.IsEqualApprox(_player.InvincibilityFrames, 0x22) ||
            !Mathf.IsEqualApprox(_player.KnockbackFrames, 0x0f) ||
            _sound.LastPlayRequest != OracleSoundEngine.SndDamageLink ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDamageLink) != damageSoundRequests + 1)
            throw new InvalidOperationException(
                "Keese contact did not apply half-heart damage, 34 invincibility updates, " +
                "15 knockback updates, and SND_DAMAGE_LINK $5f.");
        _entities.Update(0.0, _player);
        if (_player.HealthQuarters != healthBeforeContact - 2 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDamageLink) != damageSoundRequests + 1)
        {
            throw new InvalidOperationException(
                "Keese contact bypassed Link's invincibility counter or replayed SND_DAMAGE_LINK $5f.");
        }

        _player.WarpTo(normalKeese.Position + Vector2.Down * 16.0f);
        Vector2 expectedPuffPosition = normalKeese.Position +
            Vector2.Down * normalKeese.SpriteHeight;
        int countBeforeSword = _entities.Entities<KeeseCharacter>().Count;
        int killSoundsBeforeSword =
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy);
        if (!_entities.ApplySwordHit(normalKeese.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<KeeseCharacter>().Count != countBeforeSword - 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) !=
                killSoundsBeforeSword + 1)
            throw new InvalidOperationException(
                "The level-1 sword did not defeat ENEMY_KEESE with one SND_KILLENEMY request.");
        if (_entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].Position != expectedPuffPosition ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].HighKnockback ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x32 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].DurationFrames != 20 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].CurrentPalette != 2)
        {
            throw new InvalidOperationException(
                "Defeating Keese did not create the ordinary 20-update PART_ENEMY_DESTROYED puff at its visual position.");
        }

        KeeseCharacter transitionKeese = _entities.Entities<KeeseCharacter>()[0];
        int transitionCounter = transitionKeese.Counter1;
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x39);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<KeeseCharacter>().Count != 3 || _entities.Entities<KeeseCharacter>().Count != 2 ||
            _entities.OutgoingEntities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.OutgoingEntities<EnemyDeathPuffEffect>()[0].ElapsedFrames != 0 ||
            transitionKeese.Counter1 != transitionCounter ||
            !_entities.Entities<KeeseCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze outgoing Keese/death puffs and preload/freeze destination Keese.");
        _entities.SetScreenTransitionOffsets(
            Vector2.Right * 4.0f,
            Vector2.Left * (incomingRoom.Width - 4.0f));
        if (!_entities.OutgoingEntities<EnemyDeathPuffEffect>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Right * 4.0f))
            throw new InvalidOperationException("The death puff did not move with its outgoing room during scrolling.");
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<KeeseCharacter>().Count != 0 || _entities.OutgoingEntities<EnemyDeathPuffEffect>().Count != 0 ||
            !_entities.Entities<KeeseCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
            throw new InvalidOperationException(
                "Keese/death-puff transition ownership and offsets were not normalized after scrolling.");

        KeeseCharacter burningKeese = _entities.Entities<KeeseCharacter>()[0];
        burningKeese.Position = new Vector2(64, 64);
        _player.WarpTo(new Vector2(152, 112), recordSafe: false);
        int keeseBeforeBurn = _entities.Entities<KeeseCharacter>().Count;
        int killSoundsBeforeBurn =
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy);
        SeedSatchelDatabase.SeedRecord emberRecord =
            new SeedSatchelDatabase().Ember;
        EmberSeedEffect attachedFlame = _entities.Spawn<EmberSeedEffect>(
            new EmberSeedSpawn(
                burningKeese.Position - emberRecord.RightOffset,
                Vector2I.Right, emberRecord, 4));
        _entities.Update(1.0 / 60.0, _player);
        if (!burningKeese.IsVisibleInTree() ||
            attachedFlame.State != EmberSeedEffect.EmberState.Burning ||
            attachedFlame.FlameCounter != 59 ||
            attachedFlame.Position != burningKeese.Position ||
            _entities.Entities<KeeseCharacter>().Count != keeseBeforeBurn ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0)
        {
            throw new InvalidOperationException(
                "COLLISIONEFFECT_BURN did not attach PART_BURNING_ENEMY to the live Keese before damage resolution.");
        }
        KeeseCharacter.KeeseState burningState = burningKeese.State;
        int burningCounter1 = burningKeese.Counter1;
        int burningCounter2 = burningKeese.Counter2;

        burningKeese.Position += new Vector2(3, 2);
        _entities.Update(1.0 / 60.0, _player);
        if (attachedFlame.Position != burningKeese.Position ||
            attachedFlame.FlameCounter != 58)
        {
            throw new InvalidOperationException(
                "PART_BURNING_ENEMY did not follow its related enemy on the first burn update.");
        }
        for (int update = 1; update < 58; update++)
            _entities.Update(1.0 / 60.0, _player);
        if (attachedFlame.FlameCounter != 1 || burningKeese.State != burningState ||
            burningKeese.Counter1 != burningCounter1 ||
            burningKeese.Counter2 != burningCounter2 ||
            _entities.Entities<KeeseCharacter>().Count != keeseBeforeBurn)
        {
            throw new InvalidOperationException(
                "PART_BURNING_ENEMY did not stun Keese through counter1 value 1 without killing it.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<EmberSeedEffect>().Count != 0 ||
            _entities.Entities<KeeseCharacter>().Count != keeseBeforeBurn - 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) !=
                killSoundsBeforeBurn + 1)
        {
            throw new InvalidOperationException(
                "PART_BURNING_ENEMY did not resolve Ember damage and enemy death on update 59.");
        }

        // Reproduce the edge case where the moving seed narrowly misses but
        // its already-landed flame overlaps the enemy on a later collision pass.
        KeeseCharacter edgeKeese = _entities.Entities<KeeseCharacter>().Single();
        edgeKeese.Position = new Vector2(180, 120);
        _player.WarpTo(new Vector2(220, 150), recordSafe: false);
        EmberSeedEffect landedFlame = _entities.Spawn<EmberSeedEffect>(
            new EmberSeedSpawn(
                new Vector2(80, 80), Vector2I.Up, emberRecord, 4));
        for (int update = 0; update < 9; update++)
            _entities.Update(1.0 / 60.0, _player);
        if (landedFlame.State != EmberSeedEffect.EmberState.Burning ||
            landedFlame.FlameCounter != 0x3a ||
            !landedFlame.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "The edge-hit regression could not establish an active landed Ember flame.");
        }

        float edgeOverlap = emberRecord.CollisionRadiusX +
            edgeKeese.Record.CollisionRadiusX - 1.0f;
        edgeKeese.Position = landedFlame.Position + Vector2.Right * edgeOverlap;
        int edgeKillSounds =
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy);
        _entities.Update(1.0 / 60.0, _player);
        KeeseCharacter.KeeseState edgeState = edgeKeese.State;
        int edgeCounter1 = edgeKeese.Counter1;
        int edgeCounter2 = edgeKeese.Counter2;
        if (landedFlame.FlameCounter != 59 ||
            landedFlame.Position != edgeKeese.Position)
        {
            throw new InvalidOperationException(
                "A landed Ember flame touching the edge of ENEMY_KEESE did not adopt its burn target.");
        }
        for (int update = 1; update < 59; update++)
            _entities.Update(1.0 / 60.0, _player);
        if (landedFlame.FlameCounter != 1 || edgeKeese.State != edgeState ||
            edgeKeese.Counter1 != edgeCounter1 ||
            edgeKeese.Counter2 != edgeCounter2)
        {
            throw new InvalidOperationException(
                "The edge-contact Ember flame did not retain its enemy through burn counter 1.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<EmberSeedEffect>().Count != 0 ||
            _entities.Entities<KeeseCharacter>().Count != 0 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) !=
                edgeKillSounds + 1)
        {
            throw new InvalidOperationException(
                "An edge-contact Ember burn left ENEMY_KEESE inactive instead of resolving its death.");
        }

        var normalPuff = new EnemyDeathPuffEffect();
        normalPuff.Initialize(Vector2.Zero);
        for (int frame = 1; frame <= 20; frame++)
        {
            normalPuff.UpdateFrame(frame);
            if (frame == 1 &&
                (normalPuff.AnimationFrame != 0 || normalPuff.CurrentPalette != 2))
            {
                throw new InvalidOperationException(
                    "PART_ENEMY_DESTROYED changed frame/palette before its first 2-update record elapsed.");
            }
            if (frame == 2 &&
                (normalPuff.AnimationFrame != 1 || normalPuff.CurrentPalette != 3))
            {
                throw new InvalidOperationException(
                    "PART_ENEMY_DESTROYED did not advance after 2 updates and toggle OAM flags `$0a -> `$0b.");
            }
            if (frame < 20 && normalPuff.Finished)
                throw new InvalidOperationException("The ordinary enemy death puff ended before 20 updates.");
        }
        if (!normalPuff.Finished || normalPuff.ElapsedFrames != 20)
            throw new InvalidOperationException("The ordinary enemy death puff did not end after 20 updates.");
        normalPuff.Free();

        var highKnockbackPuff = new EnemyDeathPuffEffect();
        highKnockbackPuff.Initialize(Vector2.Zero, highKnockback: true);
        for (int frame = 1; frame <= 28; frame++)
        {
            highKnockbackPuff.UpdateFrame(frame);
            if (frame == 6 && highKnockbackPuff.AnimationFrame != 3)
                throw new InvalidOperationException(
                    "The high-knockback death puff did not enter its extra 8-update burst frame.");
            if (frame < 28 && highKnockbackPuff.Finished)
                throw new InvalidOperationException("The high-knockback enemy death puff ended before 28 updates.");
        }
        if (!highKnockbackPuff.Finished || highKnockbackPuff.DurationFrames != 28)
            throw new InvalidOperationException("The high-knockback enemy death puff did not end after 28 updates.");
        highKnockbackPuff.Free();
        _player.RefillHealth();

        GD.Print("Validated 53 imported ENEMY_KEESE room records, subid `$00/`$01 timing, " +
            "original RNG-driven flight, 4-update wing animation, collision radii, contact damage, " +
            "invincibility/knockback counters, flying and landed-edge 59-update Ember burn/stun, " +
            "one-hit level-1 sword defeat, common 20/28-update " +
            "death puffs with SND_KILLENEMY and palette toggling, and retained/preloaded scrolling.");
    }

    private void ValidateOctoroks()
    {
        var database = new EnemyDatabase();
        if (database.OctorokRecordCount != 33 || database.OctorokInstanceCount != 48)
        {
            throw new InvalidOperationException(
                $"Expected 33 ENEMY_OCTOROK room records / 48 instances, got " +
                $"{database.OctorokRecordCount} / {database.OctorokInstanceCount}.");
        }
        EnemyDatabase.OctorokProjectileRecord projectile = database.OctorokProjectile;
        if (projectile.TileBase != 0x0c || projectile.Palette != 3 ||
            projectile.CollisionRadiusY != 2 || projectile.CollisionRadiusX != 2 ||
            projectile.DamageQuarters != 2 || projectile.SpeedRaw != 0x50)
        {
            throw new InvalidOperationException(
                "PART_OCTOROK_PROJECTILE did not retain tile `$0c, palette 3, radius 2x2, " +
                "half-heart damage, and SPEED_200 (`$50)." );
        }

        LoadValidationRoom(0, 0x74);
        if (_entities.Entities<OctorokCharacter>().Count != 2 ||
            _entities.Entities<OctorokCharacter>().Find(octorok => octorok.Record.SubId == 0) is not OctorokCharacter red ||
            _entities.Entities<OctorokCharacter>().Find(octorok => octorok.Record.SubId == 1) is not OctorokCharacter fastRed ||
            red.Record.SpeedRaw != 0x14 || fastRed.Record.SpeedRaw != 0x1e ||
            red.Record.Health != 2 || red.Record.DamageQuarters != 1)
        {
            throw new InvalidOperationException(
                "Room 0:74 did not load one random red and one fast-red Octorok with " +
                "SPEED_80/SPEED_c0, two health, and quarter-heart contact damage.");
        }

        int redCount = _entities.Entities<OctorokCharacter>().Count;
        if (!_entities.ApplySwordHit(red.CollisionBounds.Grow(1.0f), red.Position + Vector2.Down * 16.0f) ||
            _entities.Entities<OctorokCharacter>().Count != redCount - 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x09)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not defeat a two-health red Octorok in one hit and " +
                "create its ordinary ENEMY_OCTOROK `$09 death puff.");
        }

        LoadValidationRoom(1, 0xbc);
        if (_entities.Entities<OctorokCharacter>().Count != 2 ||
            _entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Record.SubId != 2) ||
            !_entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Position == new Vector2(0x48, 0x48)) ||
            !_entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Position == new Vector2(0x58, 0x48)))
        {
            throw new InvalidOperationException(
                "Room 1:bc did not preserve its fixed blue Octoroks at `$48,`$48 and `$58,`$48.");
        }

        OctorokCharacter blue = _entities.Entities<OctorokCharacter>()[0];
        OctorokCharacter otherBlue = _entities.Entities<OctorokCharacter>()[1];
        if (blue.Record.Health != 3 || blue.Record.DamageQuarters != 2 ||
            blue.Record.CounterMask != 3)
        {
            throw new InvalidOperationException(
                "Blue Octorok subid `$02 did not retain three health, half-heart contact damage, " +
                "and decision mask `$03.");
        }

        _player.WarpTo(new Vector2(144, 120), recordSafe: false);
        otherBlue.SetStateForValidation(OctorokCharacter.OctorokState.Standing, counter1: 1000);
        blue.SetStateForValidation(
            OctorokCharacter.OctorokState.Shooting, counter1: 0x10, angle: 0x18);
        for (int frame = 1; frame < 0x10; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 0 || blue.Counter1 != 1)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK fired before completing its original `$10-update windup.");
        }
        Vector2 projectileOrigin = blue.Position;
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 1 ||
            _entities.Entities<OctorokRockProjectile>()[0].State != OctorokRockProjectile.RockState.Flying ||
            _entities.Entities<OctorokRockProjectile>()[0].ElapsedFrames != 1 ||
            _entities.Entities<OctorokRockProjectile>()[0].Position != projectileOrigin ||
            blue.State != OctorokCharacter.OctorokState.Standing || blue.Counter1 != 0x20)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK did not spawn PART_OCTOROK_PROJECTILE after 16 updates and " +
                "enter its 32-update post-shot stand.");
        }

        OctorokRockProjectile rock = _entities.Entities<OctorokRockProjectile>()[0];
        _entities.Update(1.0 / 60.0, _player);
        if (rock.Position != projectileOrigin + Vector2.Left * 2.0f)
            throw new InvalidOperationException("The Octorok rock did not move at SPEED_200 (2 pixels/update).");
        for (int frame = 0; frame < 4; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (!_entities.ApplySwordHit(rock.CollisionBounds.Grow(1.0f), _player.Position) ||
            rock.State != OctorokRockProjectile.RockState.Bouncing ||
            rock.Angle != 0x08 || rock.Counter != 0x20)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not reverse an Octorok rock and start its `$20-update bounce.");
        }
        for (int frame = 1; frame < 0x20; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (rock.Finished || _entities.Entities<OctorokRockProjectile>().Count != 1 || rock.Counter != 1)
            throw new InvalidOperationException("The deflected Octorok rock ended before bounce update `$20.");
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 0)
            throw new InvalidOperationException("The deflected Octorok rock survived bounce update `$20.");

        Vector2 terrainCollisionOrigin = Vector2.Zero;
        bool foundTerrainCollision = false;
        for (int y = 8; y < _currentRoom.Height - 8 && !foundTerrainCollision; y++)
        {
            for (int x = 8; x < _currentRoom.Width - 2; x++)
            {
                var origin = new Vector2(x, y);
                if (!_currentRoom.IsSolid(origin) &&
                    _currentRoom.IsSolid(origin + Vector2.Right * 2.0f) &&
                    origin.DistanceTo(_player.Position) > 16.0f)
                {
                    terrainCollisionOrigin = origin;
                    foundTerrainCollision = true;
                    break;
                }
            }
        }
        if (!foundTerrainCollision)
            throw new InvalidOperationException("Room 1:bc has no usable Octorok-rock collision edge.");
        var terrainRock = new OctorokRockProjectile();
        terrainRock.Initialize(projectile, _currentRoom, terrainCollisionOrigin, angle: 0x08);
        terrainRock.UpdateFrame(_player);
        terrainRock.UpdateFrame(_player);
        if (terrainRock.State != OctorokRockProjectile.RockState.CollisionPending ||
            terrainRock.Position != terrainCollisionOrigin + Vector2.Right * 2.0f)
        {
            throw new InvalidOperationException(
                "A terrain-striking Octorok rock did not enter state 2 after applying its final flying step.");
        }
        terrainRock.UpdateFrame(_player);
        if (terrainRock.State != OctorokRockProjectile.RockState.Bouncing ||
            terrainRock.Counter != 0x20 || terrainRock.Angle != 0x18)
        {
            throw new InvalidOperationException(
                "Octorok-rock terrain collision state 2 did not initialize the reversed bounce on the next update.");
        }
        terrainRock.Free();

        blue = _entities.Entities<OctorokCharacter>()[0];
        otherBlue.SetStateForValidation(OctorokCharacter.OctorokState.Standing, counter1: 1000);
        int blueCount = _entities.Entities<OctorokCharacter>().Count;
        if (!_entities.ApplySwordHit(
                blue.CollisionBounds.Grow(1.0f), blue.Position + Vector2.Left * 16.0f) ||
            blue.Health != 1 || blue.InvincibilityCounter != 0x10 ||
            blue.KnockbackCounter != 0x08 || _entities.Entities<OctorokCharacter>().Count != blueCount)
        {
            throw new InvalidOperationException(
                "A blue Octorok did not survive its first level-1 sword hit with one health, " +
                "16 invincibility updates, and 8 SPEED_200 knockback updates. Got " +
                $"health {blue.Health}, invincibility {blue.InvincibilityCounter}, " +
                $"knockback {blue.KnockbackCounter}, count {_entities.Entities<OctorokCharacter>().Count}/{blueCount}.");
        }
        if (_entities.ApplySwordHit(blue.CollisionBounds.Grow(1.0f), _player.Position))
            throw new InvalidOperationException("Blue Octorok invincibility accepted a second immediate sword hit.");
        for (int frame = 0; frame < 0x10; frame++)
            blue.UpdateFrame(_player.Position);
        if (blue.InvincibilityCounter != 0 || blue.KnockbackCounter != 0 ||
            !_entities.ApplySwordHit(blue.CollisionBounds.Grow(1.0f), _player.Position) ||
            _entities.Entities<OctorokCharacter>().Count != blueCount - 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x09)
        {
            throw new InvalidOperationException(
                "A blue Octorok did not become vulnerable after 16 updates and die on the second sword hit.");
        }

        OctorokCharacter transitionOctorok = _entities.Entities<OctorokCharacter>()[0];
        transitionOctorok.SetStateForValidation(
            OctorokCharacter.OctorokState.Standing, counter1: 1000, angle: 0x00);
        OctorokRockProjectile transitionRock = _entities.Spawn<OctorokRockProjectile>(
            new OctorokRockSpawn(transitionOctorok.Position, Angle: 0x00));
        OracleRoomData incomingRoom = _world.LoadRoom(0, 0x74);
        _entities.BeginScreenTransition(0, incomingRoom, Vector2.Left * incomingRoom.Width);
        int frozenCounter = transitionOctorok.Counter1;
        int frozenRockFrames = transitionRock.ElapsedFrames;
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<OctorokCharacter>().Count != 1 || _entities.OutgoingEntities<OctorokRockProjectile>().Count != 1 ||
            _entities.Entities<OctorokCharacter>().Count != 2 || transitionOctorok.Counter1 != frozenCounter ||
            transitionRock.ElapsedFrames != frozenRockFrames ||
            !_entities.Entities<OctorokCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
        {
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze outgoing Octoroks and rocks while preloading destination Octoroks.");
        }
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<OctorokCharacter>().Count != 0 || _entities.OutgoingEntities<OctorokRockProjectile>().Count != 0 ||
            !_entities.Entities<OctorokCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
        {
            throw new InvalidOperationException(
                "Octorok/rock transition ownership and offsets were not normalized after scrolling.");
        }

        var drops = new ItemDropDatabase();
        int octorokProbabilityRolls = 0;
        for (int roll = 0; roll < 64; roll++)
        {
            if (drops.ProbabilityAllows(4, roll))
                octorokProbabilityRolls++;
        }
        if (drops.EnemyTableRecord(0x09) != 0x8e || octorokProbabilityRolls != 24 ||
            drops.ChooseDrop(0x09, 0, 0) != ItemDropDatabase.Heart)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK `$09 did not preserve drop record `$8e, its 24-of-64 " +
                "probability `$04, and supported heart/rupee set `$0e.");
        }

        _player.RefillHealth();
        GD.Print("Validated 33 imported ENEMY_OCTOROK room records / 48 instances, random and fixed " +
            "red/fast-red/blue subids, movement/combat attributes, 16-update firing, SPEED_200 rocks, " +
            "sword deflection, 32-update bounce, two-hit blue combat, scrolling, and `$8e item drops.");
    }

    private void ValidateStalfos()
    {
        var database = new EnemyDatabase();
        IReadOnlyList<EnemyDatabase.StalfosRecord> room406 =
            database.GetRoomStalfos(4, 0x06);
        if (database.StalfosRecordCount != 34 || database.StalfosInstanceCount != 37 ||
            room406 is not
            [
                { SubId: 0x00, Y: 0x68, X: 0x68 },
                { SubId: 0x00, Y: 0x68, X: 0x98 }
            ] ||
            room406.Any(record =>
                record.TileBase != 4 || record.Palette != 1 ||
                record.CollisionRadiusY != 6 || record.CollisionRadiusX != 6 ||
                record.DamageQuarters != 2 || record.Health != 2 ||
                record.SpeedRaw != 0x14))
        {
            throw new InvalidOperationException(
                "Expected 34 ordinary ENEMY_STALFOS subid `$00 room records / 37 instances, " +
                "including room 4:06's two fixed SPEED_80, two-health, half-heart Stalfos.");
        }

        _entities.ClearRecentEnemyDefeats();
        LoadValidationRoom(4, 0x06);
        _player.WarpTo(new Vector2(0x28, 0x28), recordSafe: false);
        List<StalfosCharacter> stalfos = _entities.Entities<StalfosCharacter>();
        if (stalfos.Count != 2 ||
            stalfos[0].Position != new Vector2(0x68, 0x68) ||
            stalfos[1].Position != new Vector2(0x98, 0x68) ||
            stalfos.Any(enemy =>
                enemy.State != StalfosCharacter.StalfosState.Uninitialized ||
                enemy.Health != 2 || enemy.AnimationIndex != 0))
        {
            throw new InvalidOperationException(
                "Room 4:06 did not instantiate its two ordinary Stalfos in source order.");
        }

        int randomCalls = _entities.RandomCalls;
        Vector2[] initialPositions = stalfos.Select(enemy => enemy.Position).ToArray();
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.RandomCalls != randomCalls ||
            stalfos.Any(enemy =>
                enemy.State != StalfosCharacter.StalfosState.Deciding))
        {
            throw new InvalidOperationException(
                "ENEMY_STALFOS state `$00 did not initialize state `$08 without consuming RNG.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.RandomCalls != randomCalls + 4 ||
            stalfos.Any(enemy =>
                enemy.State != StalfosCharacter.StalfosState.Walking ||
                enemy.Counter1 is not (0x20 or 0x30 or 0x40 or 0x50) ||
                enemy.Angle is < 0 or > 0x1f) ||
            stalfos.Where((enemy, index) => enemy.Position != initialPositions[index]).Any())
        {
            throw new InvalidOperationException(
                "ENEMY_STALFOS state `$08 did not consume two shared RNG calls per enemy " +
                "and choose a 32/48/64/80-update walk without moving immediately.");
        }

        for (int frame = 0; frame < 3; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (stalfos.Any(enemy => enemy.CurrentAnimationFrame != 0))
            throw new InvalidOperationException(
                "The Stalfos walk animation advanced before four walking updates.");
        _entities.Update(1.0 / 60.0, _player);
        if (stalfos.Any(enemy => enemy.CurrentAnimationFrame != 1) ||
            stalfos.Where((enemy, index) => enemy.Position == initialPositions[index]).Any())
        {
            throw new InvalidOperationException(
                "The Stalfos did not move at SPEED_80 while advancing its four-update walk animation.");
        }

        _entities.ClearRecentEnemyDefeats();
        GD.Print("Validated 34 imported ordinary ENEMY_STALFOS records / 37 instances, " +
            "room 4:06 fixed placement, two-call shared-RNG walk selection, " +
            "32/48/64/80-update counters, SPEED_80 wall/hole-aware movement, and animation.");
    }

    private void ValidateZolsAndGels()
    {
        var database = new EnemyDatabase();
        if (database.ZolRecordCount != 61 || database.ZolInstanceCount != 79 ||
            database.GelRecordCount != 1 || database.GelInstanceCount != 3)
        {
            throw new InvalidOperationException(
                $"Expected 61 ENEMY_ZOL records / 79 instances and one ENEMY_GEL record / " +
                $"3 instances, got {database.ZolRecordCount} / {database.ZolInstanceCount} " +
                $"and {database.GelRecordCount} / {database.GelInstanceCount}.");
        }
        if (database.Gel.Id != 0x43 || database.Gel.TileBase != 0 ||
            database.Gel.Palette != 2 || database.Gel.CollisionRadiusY != 2 ||
            database.Gel.CollisionRadiusX != 2 || database.Gel.DamageQuarters != 2 ||
            database.Gel.Health != 1)
        {
            throw new InvalidOperationException(
                "ENEMY_GEL did not retain id `$43, tile base 0, palette 2, radius 2x2, " +
                "half-heart damage, and one health.");
        }

        LoadValidationRoom(4, 0xcc);
        if (_entities.Entities<ZolCharacter>().Count != 6 ||
            _entities.Entities<ZolCharacter>().FindAll(zol => zol.Record.SubId == 0).Count != 3 ||
            _entities.Entities<ZolCharacter>().FindAll(zol => zol.Record.SubId == 1).Count != 3 ||
            !_entities.Entities<ZolCharacter>().Exists(zol => zol.Position == new Vector2(0x58, 0x78)) ||
            !_entities.Entities<ZolCharacter>().Exists(zol => zol.Position == new Vector2(0x48, 0x98)))
        {
            throw new InvalidOperationException(
                "Room 4:cc did not preserve its three fixed green and three fixed red Zols.");
        }

        ZolCharacter transitionZol = _entities.Entities<ZolCharacter>()[0];
        int frozenCounter = transitionZol.Counter1;
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x0b);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<ZolCharacter>().Count != 6 || _entities.Entities<ZolCharacter>().Count != 0 ||
            _entities.Entities<GelCharacter>().Count != 3 || transitionZol.Counter1 != frozenCounter ||
            !_entities.Entities<GelCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
        {
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze six outgoing Zols and preload/freeze " +
                "the three direct room 4:0b Gels.");
        }
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<ZolCharacter>().Count != 0 ||
            !_entities.Entities<GelCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
        {
            throw new InvalidOperationException(
                "Zol/Gel transition ownership and offsets were not normalized after scrolling.");
        }

        EnemyDatabase.ZolRecord greenRecord = default;
        foreach (EnemyDatabase.ZolRecord record in database.GetRoomZols(4, 0xcc))
        {
            if (record.SubId == 0)
            {
                greenRecord = record;
                break;
            }
        }
        if (greenRecord.Id != 0x34 || greenRecord.Health != 2 ||
            greenRecord.DamageQuarters != 2 || greenRecord.Palette != 0)
        {
            throw new InvalidOperationException(
                "Green ENEMY_ZOL `$34/`$00 attributes were not imported correctly.");
        }

        var timingZol = new ZolCharacter();
        var timingRandom = new OracleRandom();
        timingZol.Initialize(greenRecord, _world.LoadRoom(4, 0xcc), new Vector2(80, 80), timingRandom);
        timingZol.UpdateFrame(new Vector2(120, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHidden)
            throw new InvalidOperationException("Green Zol woke at the excluded Manhattan distance `$28.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenEmerging ||
            timingZol.Counter2 != 4 || !timingZol.Visible)
        {
            throw new InvalidOperationException(
                "Green Zol did not wake inside Manhattan distance `$28 with four hops queued.");
        }
        for (int frame = 0; frame < 32; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenEmerging ||
            timingZol.AnimationParameter != 1 || timingZol.ZFixed != 0)
        {
            throw new InvalidOperationException(
                "Green Zol emergence did not reach its terminal animation parameter after 32 updates.");
        }
        for (int frame = 0; frame < 26; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.ZFixed >= 0 || timingZol.State != ZolCharacter.ZolState.GreenEmerging)
            throw new InvalidOperationException("Green Zol landed before its 27th gravity update.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenWaiting ||
            timingZol.Counter1 != 0x30 || !timingZol.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "Green Zol did not land on gravity update 27 and begin its 48-update wait.");
        }
        for (int frame = 0; frame < 47; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.Counter1 != 1 || timingZol.State != ZolCharacter.ZolState.GreenWaiting)
            throw new InvalidOperationException("Green Zol ended its 48-update wait early.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHopping)
            throw new InvalidOperationException("Green Zol did not begin its first pursuit hop.");
        for (int frame = 0; frame < 27; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.Counter2 != 3 || timingZol.State != ZolCharacter.ZolState.GreenWaiting)
            throw new InvalidOperationException("Green Zol did not consume exactly one of four hops.");
        for (int hop = 0; hop < 3; hop++)
        {
            for (int frame = 0; frame < 48; frame++)
                timingZol.UpdateFrame(new Vector2(119, 80));
            for (int frame = 0; frame < 27; frame++)
                timingZol.UpdateFrame(new Vector2(119, 80));
        }
        if (timingZol.State != ZolCharacter.ZolState.GreenDisappearing ||
            timingZol.Counter2 != 0 || timingZol.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "Green Zol did not disable collision and disappear after exactly four hops.");
        }
        for (int frame = 0; frame < 40; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.AnimationParameter != 1 ||
            timingZol.State != ZolCharacter.ZolState.GreenDisappearing)
        {
            throw new InvalidOperationException(
                "Green Zol disappearance did not reach its terminal parameter after 40 updates.");
        }
        timingZol.UpdateFrame(new Vector2(119, 80));
        for (int frame = 0; frame < 39; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenGone || timingZol.Counter1 != 1)
            throw new InvalidOperationException("Green Zol ended its 40-update underground wait early.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHidden)
            throw new InvalidOperationException("Green Zol did not return to its hidden proximity state.");
        timingZol.Free();

        LoadValidationRoom(4, 0xcc);
        ZolCharacter green = _entities.Entities<ZolCharacter>().Find(zol => zol.Record.SubId == 0)!;
        green.SetStateForValidation(
            ZolCharacter.ZolState.GreenWaiting,
            counter1: 1000,
            animation: 1);
        int greenCount = _entities.Entities<ZolCharacter>().Count;
        if (!_entities.ApplySwordHit(green.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<ZolCharacter>().Count != greenCount - 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x34)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not defeat a surfaced green Zol and create its normal `$34 death puff.");
        }

        LoadValidationRoom(4, 0xcc);
        _player.WarpTo(new Vector2(220, 160), recordSafe: false);
        ZolCharacter red = _entities.Entities<ZolCharacter>().Find(zol => zol.Record.SubId == 1)!;
        Vector2 splitPosition = red.Position;
        int redRoomCount = _entities.Entities<ZolCharacter>().Count;
        _sound.ClearPlayRequestAudit();
        if (!_entities.ApplySwordHit(red.CollisionBounds.Grow(1.0f)) ||
            red.State != ZolCharacter.ZolState.RedSplitting ||
            _entities.Entities<ZolCharacter>().Count != redRoomCount ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 0)
        {
            throw new InvalidOperationException(
                "A sword-hit red Zol did not enter its special split state without a normal death puff.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (red.State != ZolCharacter.ZolState.RedSplitDelay || red.Counter2 != 18 ||
            red.Visible || red.CollisionEnabled || _entities.Entities<KillEnemyPuffEffect>().Count != 1 ||
            _entities.Entities<KillEnemyPuffEffect>()[0].DurationFrames != 20 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 1)
        {
            throw new InvalidOperationException(
                "Red Zol did not create the 20-update INTERAC_KILLENEMYPUFF and begin its 18-update delay.");
        }
        for (int frame = 0; frame < 17; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<GelCharacter>().Count != 0 || red.Counter2 != 1)
            throw new InvalidOperationException("Red Zol spawned Gels before split delay update 18.");
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<ZolCharacter>().Count != redRoomCount - 1 || _entities.Entities<GelCharacter>().Count != 2 ||
            !_entities.Entities<GelCharacter>().Exists(gel => gel.Position == splitPosition + Vector2.Right * 4.0f) ||
            !_entities.Entities<GelCharacter>().Exists(gel => gel.Position == splitPosition + Vector2.Left * 4.0f))
        {
            throw new InvalidOperationException(
                "Red Zol did not replace itself with two Gels at the original +/-4 X offsets.");
        }
        _entities.Update(2.0 / 60.0, _player);
        if (_entities.Entities<KillEnemyPuffEffect>().Count != 0 || _entities.Entities<ItemDropEffect>().Count != 0)
            throw new InvalidOperationException(
                "INTERAC_KILLENEMYPUFF did not end after 20 updates or incorrectly resolved an item drop.");

        GelCharacter defeatedGel = _entities.Entities<GelCharacter>()[0];
        int gelCount = _entities.Entities<GelCharacter>().Count;
        if (!_entities.ApplySwordHit(defeatedGel.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<GelCharacter>().Count != gelCount - 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x43 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 2)
        {
            throw new InvalidOperationException(
                "The one-health ENEMY_GEL did not die to one level-1 sword hit with a `$43 death puff.");
        }

        GelCharacter latchGel = _entities.Entities<GelCharacter>()[0];
        Vector2 latchPosition = new(180, 140);
        latchGel.Position = latchPosition;
        _player.WarpTo(latchPosition, recordSafe: false);
        _player.RefillHealth();
        int healthBeforeLatch = _player.HealthQuarters;
        _entities.Update(0.0, _player);
        if (!latchGel.IsAttached || latchGel.Counter2 != 120 ||
            _player.HealthQuarters != healthBeforeLatch ||
            !_entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Gel contact damaged Link or failed to latch for 120 updates and disable the sword.");
        }
        bool movementPhase = _entities.PlayerMovementDisabled;
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.PlayerMovementDisabled == movementPhase)
            throw new InvalidOperationException("Attached Gel did not immobilize Link on alternating updates.");
        for (int frame = 0; frame < 118; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (!latchGel.IsAttached || latchGel.Counter2 != 1)
            throw new InvalidOperationException(
                "Gel did not remain attached through latch update 119: " +
                $"state={latchGel.State}, counter2={latchGel.Counter2}, " +
                $"entityFrame={_entities.FrameCounter}.");
        _entities.Update(1.0 / 60.0, _player);
        if (latchGel.IsAttached || latchGel.State != GelCharacter.GelState.Hopping ||
            latchGel.Angle != 0x00 || latchGel.CollisionEnabled ||
            _entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Gel did not automatically release after 120 updates with hop collision disabled.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (latchGel.IsAttached || _entities.PlayerSwordDisabled)
            throw new InvalidOperationException(
                "A naturally released Gel immediately relatched before completing its hop.");

        Vector2 buttonLatchPosition = new(120, 140);
        GelCharacter buttonGel = _entities.Spawn<GelCharacter>(
            new GelSpawn(buttonLatchPosition, "ButtonReleaseGel"));
        _player.WarpTo(buttonLatchPosition, recordSafe: false);
        _entities.Update(0.0, _player);
        if (!buttonGel.IsAttached || buttonGel.Counter2 != 120)
            throw new InvalidOperationException("Button-release test Gel did not latch.");
        for (int press = 0; press < 30; press++)
            buttonGel.UpdateFrame(_player.Position, Vector2I.Down, anyButtonJustPressed: true);
        if (!buttonGel.IsAttached || buttonGel.Counter2 != 1)
            throw new InvalidOperationException(
                "Thirty button presses did not reduce the Gel latch counter to one.");
        buttonGel.UpdateFrame(_player.Position, Vector2I.Down, anyButtonJustPressed: false);
        if (buttonGel.IsAttached || buttonGel.State != GelCharacter.GelState.Hopping ||
            buttonGel.Angle != 0x00 || buttonGel.CollisionEnabled ||
            _entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Button presses did not release the Gel with collision disabled and hop it away from Link.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (buttonGel.IsAttached || _entities.PlayerSwordDisabled)
            throw new InvalidOperationException(
                "A button-released Gel immediately relatched before completing its hop.");

        LoadValidationRoom(4, 0x08);
        Vector2 holeCenter = new(0x0a * 16 + 8, 0x04 * 16 + 8);
        if (_currentRoom.GetTerrainInfo(holeCenter).Hazard !=
            OracleRoomData.HazardType.Hole)
        {
            throw new InvalidOperationException(
                "Room 4:08/$4a was not the expected hole for enemy hazard audio validation.");
        }
        _player.WarpTo(new Vector2(0x48, 0x78), recordSafe: false);
        GelCharacter holeGel = _entities.Spawn<GelCharacter>(
            new GelSpawn(holeCenter, "HoleSoundGel"));
        _sound.ClearPlayRequestAudit();
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<GelCharacter>().Contains(holeGel) ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != 0)
        {
            throw new InvalidOperationException(
                "A Gel entering a hole did not request only SND_FALLINHOLE on hazard death.");
        }

        _player.RefillHealth();
        GD.Print("Validated 61 ENEMY_ZOL records / 79 instances, room 4:cc fixed placements, " +
            "strict `$28 emergence, 32/27/48-update green timing, four-hop disappearance, " +
            "red 18-update splitting with INTERAC_KILLENEMYPUFF/SND_KILLENEMY, +/-4 Gel " +
            "spawning, hole SND_FALLINHOLE, direct " +
            "room Gels, non-damaging 120-update latch/button release, collision-safe hop-off, " +
            "alternating movement suppression, " +
            "sword disabling, combat, drops, and retained/preloaded scrolling.");
    }

    private void ValidateItemDrops()
    {
        var database = new ItemDropDatabase();
        if (ItemDropDatabase.SelectionDataSize != 720 ||
            database.EnemyTableRecord(0x32) != 0xae)
        {
            throw new InvalidOperationException(
                "ENEMY_KEESE `$32 did not retain item-drop record `$ae in the 720-byte selection data.");
        }

        int allowedProbabilityRolls = 0;
        int allowedRoll = -1;
        int deniedRoll = -1;
        for (int roll = 0; roll < 64; roll++)
        {
            if (database.ProbabilityAllows(5, roll))
            {
                allowedProbabilityRolls++;
                if (allowedRoll < 0)
                    allowedRoll = roll;
            }
            else if (deniedRoll < 0)
            {
                deniedRoll = roll;
            }
        }
        if (allowedProbabilityRolls != 32 || allowedRoll < 0 || deniedRoll < 0 ||
            database.ChooseDrop(0x32, (byte)deniedRoll, 0).HasValue ||
            database.ChooseDrop(0x32, (byte)allowedRoll, 0) != ItemDropDatabase.Heart)
        {
            throw new InvalidOperationException(
                "Keese probability set 5 did not preserve its original 32-of-64 drop chance.");
        }

        int hearts = 0;
        int oneRupees = 0;
        int fiveRupees = 0;
        for (int roll = 0; roll < ItemDropDatabase.SetSize; roll++)
        {
            switch (database.DropSetValue(0x0e, roll))
            {
                case ItemDropDatabase.Heart: hearts++; break;
                case ItemDropDatabase.OneRupee: oneRupees++; break;
                case ItemDropDatabase.FiveRupees: fiveRupees++; break;
            }
        }
        if (hearts != 16 || oneRupees != 12 || fiveRupees != 4)
        {
            throw new InvalidOperationException(
                $"Keese drop set `$0e should contain 16 hearts, 12 single rupees, and 4 five-rupee drops; " +
                $"got {hearts}, {oneRupees}, and {fiveRupees}.");
        }

        ItemDropDatabase.VisualRecord heartVisual = database.GetVisual(ItemDropDatabase.Heart);
        ItemDropDatabase.VisualRecord oneRupeeVisual = database.GetVisual(ItemDropDatabase.OneRupee);
        ItemDropDatabase.VisualRecord fiveRupeeVisual = database.GetVisual(ItemDropDatabase.FiveRupees);
        ItemDropDatabase.VisualRecord hundredRupeeVisual =
            database.GetVisual(ItemDropDatabase.OneHundredRupeesOrEnemy);
        if (heartVisual.TileBase != 2 || heartVisual.Palette != 5 ||
            oneRupeeVisual.TileBase != 4 || oneRupeeVisual.Palette != 0 ||
            fiveRupeeVisual.TileBase != 6 || fiveRupeeVisual.Palette != 5 ||
            hundredRupeeVisual.TileBase != 8 || hundredRupeeVisual.Palette != 4)
        {
            throw new InvalidOperationException(
                "Heart/rupee PART_ITEM_DROP visuals do not match spriteData tile bases " +
                "`$02/`$04/`$06/`$08.");
        }

        var lifecycleRoot = new Node { Name = "ItemDropLifecycleValidation" };
        AddChild(lifecycleRoot);
        var lifecycleManager = new RoomEntityManager(
            lifecycleRoot,
            new NpcDatabase(),
            new EnemyDatabase(),
            database,
            new TimePortalDatabase(),
            new OracleRandom());
        lifecycleManager.LoadRoom(0, _world.LoadRoom(0, 0x00));
        _player.WarpTo(new Vector2(140, 120), recordSafe: false);
        for (int defeated = 0; defeated < 5; defeated++)
        {
            lifecycleManager.Spawn<EnemyDeathPuffEffect>(
                new EnemyDeathPuffSpawn(new Vector2(24, 24), EnemyId: 0x32));
            for (int frame = 0; frame < 20; frame++)
                lifecycleManager.Update(1.0 / 60.0, _player);
            if (lifecycleManager.Entities<EnemyDeathPuffEffect>().Count != 0)
            {
                throw new InvalidOperationException(
                    "A finished Keese death puff was not replaced/deleted by item-drop resolution.");
            }
        }
        if (lifecycleManager.Entities<ItemDropEffect>().Count != 1 ||
            lifecycleManager.Entities<ItemDropEffect>()[0].SubId != ItemDropDatabase.FiveRupees ||
            lifecycleManager.Entities<ItemDropEffect>()[0].ElapsedFrames != 0)
        {
            throw new InvalidOperationException(
                "The deterministic fifth Keese death did not replace its completed puff with ITEM_DROP_5_RUPEES.");
        }
        lifecycleManager.Clear();
        RemoveChild(lifecycleRoot);
        lifecycleRoot.Free();

        LoadValidationRoom(4, 0xcb);
        Vector2 dropPosition = FindOpenItemDropPosition(_currentRoom);
        Vector2 farPosition = dropPosition + Vector2.Right * 40.0f;
        _player.WarpTo(farPosition, recordSafe: false);

        int[] shovelAngles = { 0x00, 0x08, 0x10, 0x18 };
        Vector2[] shovelDirections =
        {
            Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left
        };
        for (int index = 0; index < shovelAngles.Length; index++)
        {
            var dugUpDrop = new ItemDropEffect();
            dugUpDrop.Initialize(
                ItemDropDatabase.OneRupee, dropPosition, _currentRoom,
                oneRupeeVisual, shovelAngles[index], dugUp: true);
            dugUpDrop.UpdateFrame(_player, 1);
            if (dugUpDrop.State != ItemDropEffect.DropState.Bouncing ||
                dugUpDrop.Speed != 0x19 || dugUpDrop.Angle != shovelAngles[index] ||
                dugUpDrop.PrecisePosition != dropPosition)
            {
                throw new InvalidOperationException(
                    $"Shovel item drop angle ${shovelAngles[index]:x2} did not initialize " +
                    "SPEED_a0 without moving during state 0.");
            }
            dugUpDrop.UpdateFrame(_player, 2);
            Vector2 expectedPosition = dropPosition + shovelDirections[index] * 0.625f;
            if (dugUpDrop.PrecisePosition != expectedPosition ||
                dugUpDrop.Position != OracleObjectMath.ToPixelPosition(expectedPosition))
            {
                throw new InvalidOperationException(
                    $"Shovel item drop angle ${shovelAngles[index]:x2} did not apply its " +
                    "exact SPEED_a0 8.8 displacement on the first bounce update.");
            }
            dugUpDrop.Free();
        }

        var stationaryDrop = new ItemDropEffect();
        stationaryDrop.Initialize(
            ItemDropDatabase.OneRupee, dropPosition, _currentRoom, oneRupeeVisual);
        stationaryDrop.UpdateFrame(_player, 1);
        stationaryDrop.UpdateFrame(_player, 2);
        if (stationaryDrop.Speed != 0 || stationaryDrop.PrecisePosition != dropPosition)
        {
            throw new InvalidOperationException(
                "An ordinary enemy item drop incorrectly inherited shovel launch velocity.");
        }
        stationaryDrop.Free();

        var bounceDrop = new ItemDropEffect();
        bounceDrop.Initialize(
            ItemDropDatabase.Heart, dropPosition, _currentRoom, heartVisual);
        bounceDrop.UpdateFrame(_player, 1);
        if (bounceDrop.State != ItemDropEffect.DropState.Bouncing ||
            bounceDrop.ZFixed != 0 || bounceDrop.SpeedZ != -0x160)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not spend its first update initializing speedZ to -`$160.");
        }
        for (int frame = 2; frame < 36; frame++)
        {
            bounceDrop.UpdateFrame(_player, frame);
            if (bounceDrop.State == ItemDropEffect.DropState.Grounded)
                throw new InvalidOperationException("PART_ITEM_DROP finished bouncing before update 36.");
        }
        bounceDrop.UpdateFrame(_player, 36);
        if (bounceDrop.State != ItemDropEffect.DropState.Grounded ||
            bounceDrop.ZFixed != 0 || bounceDrop.SpeedZ != 0 ||
            bounceDrop.Counter != 240 || !bounceDrop.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not complete its original fixed-point bounce and start counter `$f0 on update 36.");
        }
        bounceDrop.Free();

        _player.RefillHealth();
        _player.ApplyDamage(4);
        _statusBar.SynchronizeHealth();
        int heartSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndGainHeart);
        int displayedHealthBefore = _hud.HealthQuarters;
        var heartDrop = new ItemDropEffect();
        heartDrop.Initialize(
            ItemDropDatabase.Heart, dropPosition, _currentRoom, heartVisual);
        for (int frame = 1; frame <= 36; frame++)
            heartDrop.UpdateFrame(_player, frame);
        _player.WarpTo(dropPosition, recordSafe: false);
        heartDrop.UpdateFrame(_player, 37);
        if (!heartDrop.Collected || !heartDrop.Finished ||
            _player.HealthQuarters != _player.MaxHealthQuarters ||
            _hud.HealthQuarters != displayedHealthBefore ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGainHeart) != heartSoundRequests)
        {
            throw new InvalidOperationException(
                "Collecting ITEM_DROP_HEART did not restore live health immediately while " +
                "leaving wDisplayedHearts and SND_GAINHEART pending.");
        }
        heartDrop.Free();

        var healthDisplayUpdates = new List<int>();
        int previousDisplayedHealth = _hud.HealthQuarters;
        for (int update = 1; update <= 16; update++)
        {
            _statusBar.Update(1.0 / 60.0);
            if (_hud.HealthQuarters != previousDisplayedHealth)
            {
                healthDisplayUpdates.Add(update);
                previousDisplayedHealth = _hud.HealthQuarters;
            }
        }
        if (_hud.HealthQuarters != _player.HealthQuarters ||
            healthDisplayUpdates.Count != 4 ||
            healthDisplayUpdates.Skip(1).Zip(healthDisplayUpdates, (next, prior) => next - prior)
                .Any(interval => interval != 4) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGainHeart) != heartSoundRequests + 1)
        {
            throw new InvalidOperationException(
                "ITEM_DROP_HEART did not fill one displayed quarter every four updates and " +
                "request SND_GAINHEART `$57 on the completed-heart boundary.");
        }

        _statusBar.SynchronizeHealth();
        heartSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndGainHeart);
        var fullHealthDrop = new ItemDropEffect();
        fullHealthDrop.Initialize(
            ItemDropDatabase.Heart, dropPosition, _currentRoom, heartVisual);
        _player.WarpTo(farPosition, recordSafe: false);
        for (int frame = 1; frame <= 36; frame++)
            fullHealthDrop.UpdateFrame(_player, frame);
        _player.WarpTo(dropPosition, recordSafe: false);
        fullHealthDrop.UpdateFrame(_player, 37);
        if (!fullHealthDrop.Collected || _player.HealthQuarters != _player.MaxHealthQuarters ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGainHeart) != heartSoundRequests + 1)
        {
            throw new InvalidOperationException(
                "ITEM_DROP_HEART collected at full health did not request SND_GAINHEART `$57 immediately.");
        }
        fullHealthDrop.Free();

        ValidateRupeeItemDrop(oneRupeeVisual, ItemDropDatabase.OneRupee, 1, dropPosition);
        ValidateRupeeItemDrop(fiveRupeeVisual, ItemDropDatabase.FiveRupees, 5, dropPosition);
        ValidateRupeeItemDrop(
            hundredRupeeVisual, ItemDropDatabase.OneHundredRupeesOrEnemy,
            100, dropPosition);
        ValidateRupeeCountDown();
        ValidateCappedRupeeItemDrop(oneRupeeVisual, dropPosition);

        _player.WarpTo(farPosition, recordSafe: false);
        var expiryDrop = new ItemDropEffect();
        expiryDrop.Initialize(
            ItemDropDatabase.OneRupee, dropPosition, _currentRoom, oneRupeeVisual);
        for (int frame = 1; frame <= 395; frame++)
            expiryDrop.UpdateFrame(_player, frame);
        if (expiryDrop.Counter != 60 || !expiryDrop.Visible || expiryDrop.Finished)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not retain visibility through countdown value 60.");
        }
        expiryDrop.UpdateFrame(_player, 396);
        expiryDrop.UpdateFrame(_player, 397);
        if (expiryDrop.Counter != 59 || expiryDrop.Visible)
            throw new InvalidOperationException("PART_ITEM_DROP did not begin flickering below counter 60.");
        expiryDrop.UpdateFrame(_player, 398);
        expiryDrop.UpdateFrame(_player, 399);
        if (!expiryDrop.Visible)
            throw new InvalidOperationException("PART_ITEM_DROP did not alternate visibility while flickering.");
        for (int frame = 400; frame <= 514; frame++)
            expiryDrop.UpdateFrame(_player, frame);
        if (expiryDrop.Finished || expiryDrop.Counter != 1)
            throw new InvalidOperationException("PART_ITEM_DROP expired before its 240th countdown tick.");
        expiryDrop.UpdateFrame(_player, 515);
        if (!expiryDrop.Finished || expiryDrop.Collected)
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not expire after 240 alternating-frame ticks (480 update span).");
        expiryDrop.Free();

        ItemDropEffect transitionDrop = _entities.Spawn<ItemDropEffect>(
            new ItemDropSpawn(ItemDropDatabase.OneRupee, dropPosition));
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x39);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<ItemDropEffect>().Count != 1 ||
            _entities.OutgoingEntities<ItemDropEffect>()[0] != transitionDrop ||
            transitionDrop.ElapsedFrames != 0)
        {
            throw new InvalidOperationException(
                "Scrolling did not retain and freeze the outgoing PART_ITEM_DROP object.");
        }
        _entities.SetScreenTransitionOffsets(
            Vector2.Right * 4.0f,
            Vector2.Left * (incomingRoom.Width - 4.0f));
        if (!transitionDrop.TransitionDrawOffset.IsEqualApprox(Vector2.Right * 4.0f))
            throw new InvalidOperationException("The item drop did not move with its outgoing room.");
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<ItemDropEffect>().Count != 0)
            throw new InvalidOperationException("The outgoing item drop survived completed scrolling.");

        ValidateItemDropWaterSplash(oneRupeeVisual);

        _player.RefillHealth();
        GD.Print("Validated all 144 enemy drop records, Keese `$ae probability/set data, " +
            "PART_ITEM_DROP heart/1/5/100-rupee visuals, shovel SPEED_a0 launch, " +
            "-`$160 fixed-point bounce, heart `$57 and rupee `$61 pickup sounds, " +
            "ground-height INTERAC_SPLASH/`$87 water disposal, one-per-update rupee display and " +
            "SND_RUPEE `$61 requests including the `$0999 cap, " +
            "240 alternating-frame lifetime ticks, final flicker, and frozen scrolling ownership.");
    }

    private void ValidateRupeeItemDrop(
        ItemDropDatabase.VisualRecord visual,
        int subId,
        int amount,
        Vector2 position)
    {
        if (_player.Rupees + amount > 999)
            _inventory.AddRupees(800 - _player.Rupees);
        _statusBar.SynchronizeRupees();
        int rupeesBefore = _player.Rupees;
        int displayedBefore = _hud.Rupees;
        int soundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndRupee);
        _player.WarpTo(position + Vector2.Right * 40.0f, recordSafe: false);
        var drop = new ItemDropEffect();
        drop.Initialize(subId, position, _currentRoom, visual);
        for (int frame = 1; frame <= 36; frame++)
            drop.UpdateFrame(_player, frame);
        _player.WarpTo(position, recordSafe: false);
        drop.UpdateFrame(_player, 37);
        if (!drop.Collected || _player.Rupees != Mathf.Min(999, rupeesBefore + amount) ||
            _hud.Rupees != displayedBefore ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndRupee) != soundRequests)
        {
            throw new InvalidOperationException(
                $"Collecting PART_ITEM_DROP ${subId:x2} did not update the wallet immediately " +
                "while leaving wDisplayedRupees and SND_RUPEE pending for updateStatusBar_body.");
        }
        for (int update = 1; update <= amount; update++)
        {
            _statusBar.Update(1.0 / 60.0);
            if (_hud.Rupees != displayedBefore + update ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndRupee) != soundRequests + update)
            {
                throw new InvalidOperationException(
                    $"PART_ITEM_DROP ${subId:x2} rupee display update {update}/{amount} did not " +
                    "advance by one and request SND_RUPEE `$61 exactly once.");
            }
        }
        drop.Free();
    }

    private void ValidateRupeeCountDown()
    {
        _statusBar.SynchronizeRupees();
        int displayedBefore = _hud.Rupees;
        int soundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndRupee);
        _inventory.AddRupees(-3);
        for (int update = 1; update <= 3; update++)
        {
            _statusBar.Update(1.0 / 60.0);
            if (_hud.Rupees != displayedBefore - update ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndRupee) != soundRequests + update)
            {
                throw new InvalidOperationException(
                    $"Rupee display countdown update {update}/3 did not subtract one and " +
                    "request SND_RUPEE `$61 exactly once.");
            }
        }
        _inventory.AddRupees(3);
        _statusBar.SynchronizeRupees();
    }

    private void ValidateCappedRupeeItemDrop(
        ItemDropDatabase.VisualRecord visual,
        Vector2 position)
    {
        int restoreRupees = _player.Rupees;
        _inventory.AddRupees(999 - restoreRupees);
        _statusBar.SynchronizeRupees();
        int soundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndRupee);

        _player.WarpTo(position + Vector2.Right * 40.0f, recordSafe: false);
        var drop = new ItemDropEffect();
        drop.Initialize(ItemDropDatabase.OneRupee, position, _currentRoom, visual);
        for (int frame = 1; frame <= 36; frame++)
            drop.UpdateFrame(_player, frame);
        _player.WarpTo(position, recordSafe: false);
        drop.UpdateFrame(_player, 37);
        _statusBar.Update(1.0 / 60.0);
        if (!drop.Collected || _player.Rupees != 999 || _hud.Rupees != 999 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndRupee) != soundRequests + 1)
        {
            throw new InvalidOperationException(
                "A PART_ITEM_DROP rupee collected at the `$0999 cap did not retain the cap " +
                "and request the mode `$0e overflow SND_RUPEE `$61 exactly once; got " +
                $"collected={drop.Collected}, wallet={_player.Rupees}, displayed={_hud.Rupees}, " +
                $"sound requests={_sound.PlayRequestsFor(OracleSoundEngine.SndRupee) - soundRequests}.");
        }
        drop.Free();

        _inventory.AddRupees(restoreRupees - _player.Rupees);
        _statusBar.SynchronizeRupees();
    }

    private void ValidateItemDropWaterSplash(ItemDropDatabase.VisualRecord visual)
    {
        const int group = 0;
        const int room = 0xb8;
        Vector2 waterCenter = new(8, 8);
        Vector2 safePosition = new(40, 8);
        LoadValidationRoom(group, room);
        if (_currentRoom.GetTerrainInfo(waterCenter + new Vector2(0, 5)).Hazard !=
            OracleRoomData.HazardType.Water)
        {
            throw new InvalidOperationException(
                "Canonical room 0:b8 position `$00 is not water for PART_ITEM_DROP validation.");
        }

        _player.WarpTo(safePosition, recordSafe: false);
        int splashSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSplash);
        SplashEffect? priorSplash = _terrain.ActiveSplash;
        ItemDropEffect drop = _entities.Spawn<ItemDropEffect>(
            new ItemDropSpawn(ItemDropDatabase.OneRupee, waterCenter));

        for (int update = 1; update <= 23; update++)
        {
            _entities.Update(1.0 / 60.0, _player);
            if (drop.Finished || _terrain.ActiveSplash != priorSplash)
            {
                throw new InvalidOperationException(
                    $"PART_ITEM_DROP created its water splash while still airborne on update {update}.");
            }
        }
        _entities.Update(1.0 / 60.0, _player);
        SplashEffect? splash = _terrain.ActiveSplash;
        if (!drop.Finished || drop.FinishedHazard != OracleRoomData.HazardType.Water ||
            _entities.Entities<ItemDropEffect>().Count != 0 || splash is null ||
            ReferenceEquals(splash, priorSplash) || splash.IsLava ||
            splash.Position != waterCenter || splash.DurationFrames != 12 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != splashSoundRequests + 1)
        {
            throw new InvalidOperationException(
                $"PART_ITEM_DROP did not become INTERAC_SPLASH with SND_SPLASH `$87 on " +
                $"ground-height update 24 in room {group:x1}:{room:x2}.");
        }
    }

    private static Vector2 FindOpenItemDropPosition(OracleRoomData room)
    {
        for (int y = 1; y < room.HeightInTiles - 1; y++)
        for (int x = 1; x < room.WidthInTiles - 1; x++)
        {
            Vector2 point = new(
                x * OracleRoomData.MetatileSize + 8,
                y * OracleRoomData.MetatileSize + 8);
            if (!room.IsSolid(point) &&
                !room.IsSolid(point + new Vector2(0, -5)) &&
                !room.IsSolid(point + new Vector2(4, 0)) &&
                !room.IsSolid(point + new Vector2(0, 4)) &&
                !room.IsSolid(point + new Vector2(-5, 0)) &&
                room.GetTerrainInfo(point).Hazard == OracleRoomData.HazardType.None)
                return point;
        }
        throw new InvalidOperationException(
            $"Room {room.Group:x1}:{room.Id:x2} has no open item-drop validation position.");
    }

    private void ValidateTerrain()
    {
        _dialogue.Close();
        _player.RefillHealth();
        _activeGroup = 0;
        ClearDeactivatedWarp();

        _currentRoom = _world.LoadRoom(_activeGroup, 0xb8);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 waterSafe = new(40, 8);
        _player.WarpTo(waterSafe);
        _player.WarpTo(new Vector2(8, 8), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Water)
            throw new InvalidOperationException("Expected room b8/$00 to be water terrain.");
        ValidateDrowningSequence(waterSafe, OracleRoomData.HazardType.Water);

        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 lavaSafe = new(56, 8);
        _player.WarpTo(lavaSafe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Lava)
            throw new InvalidOperationException("Expected room 03/$10 to be lava terrain.");
        ValidateDrowningSequence(lavaSafe, OracleRoomData.HazardType.Lava);

        if (!TryFindTerrainSample(
            OracleRoomData.HazardType.Hole,
            out int holeGroup,
            out int holeRoom,
            out Vector2 holeCenter,
            out Vector2 holeSafe))
        {
            throw new InvalidOperationException("Could not find a testable hole terrain tile.");
        }

        _player.RefillHealth();
        _activeGroup = holeGroup;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, holeRoom);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(holeSafe);
        Vector2 offCenterHoleEntry = holeCenter + new Vector2(3, -2);
        int beforeHoleHealth = _player.HealthQuarters;
        int fallSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndLinkFall);
        _player.WarpTo(offCenterHoleEntry, recordSafe: false);
        Vector2 expectedHoleCenter = GetActiveTerrain(_player.Position).TileCenter;
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                $"Room {holeGroup:x1}:{holeRoom:x2} hole terrain did not start Link's pull-in state.");
        if (_player.HealthQuarters != beforeHoleHealth)
            throw new InvalidOperationException("Hole damage was applied before the pull/fall animation finished.");
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndLinkFall) != fallSoundRequests)
            throw new InvalidOperationException("Hole pull-in played SND_LINK_FALL $65 before the fall began.");

        AdvanceHolePullUntilFall(expectedHoleCenter);
        if (_sound.LastPlayRequest != OracleSoundEngine.SndLinkFall ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndLinkFall) != fallSoundRequests + 1)
        {
            throw new InvalidOperationException(
                "The fall-in-hole animation did not start exactly one SND_LINK_FALL $65 request.");
        }
        AdvanceHoleFallUntilRespawn(holeSafe);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(holeSafe) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the last safe tile.");
        if (_player.HealthQuarters != beforeHoleHealth - 2)
            throw new InvalidOperationException("Hole terrain did not apply half-heart damage after respawn.");
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndLinkFall) != fallSoundRequests + 1)
            throw new InvalidOperationException("Hole respawn replayed SND_LINK_FALL $65.");

        ValidateRoom01HoleBoundaryCase();

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x11);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 ledgeStart = new(24, 56);
        _player.WarpTo(ledgeStart);
        _player.Face(Vector2I.Down);
        if (!TryStartLedgeHop(_player, _player.Position, Vector2.Down))
            throw new InvalidOperationException("Room 11's south cliff did not start a ledge hop.");
        for (int i = 0; i < 30; i++)
            _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Position.Y <= ledgeStart.Y + OracleRoomData.MetatileSize)
            throw new InvalidOperationException("The ledge hop did not carry Link down across the cliff.");

        ValidateRoom56TileEdgeSlide();

        GD.Print("Validated terrain hazards, hole fall/respawn, ledge hop, and original tile-edge sliding.");
    }

    private void ValidateDrowningSequence(
        Vector2 safePosition,
        OracleRoomData.HazardType hazard)
    {
        string terrainName = hazard.ToString();
        Vector2 hazardPosition = _player.Position;
        int healthBeforeDrowning = _player.HealthQuarters;
        int worldChildCount = _scene.WorldRoot.GetChildCount();
        int damageSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndDamageLink);
        int splashSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSplash);

        _player._PhysicsProcess(1.0 / 60.0);
        SplashEffect? splash = _terrain.ActiveSplash;
        if (_scene.WorldRoot.GetChildCount() != worldChildCount + 1 || splash is null ||
            splash.Position != hazardPosition ||
            splash.IsLava != (hazard == OracleRoomData.HazardType.Lava))
        {
            throw new InvalidOperationException(
                $"{terrainName} drowning did not create its original splash interaction at Link's position.");
        }
        int splashFrameDuration = splash.IsLava ? 2 : 4;
        int splashFrameCount = splash.IsLava ? 10 : 3;
        if (splash.DurationFrames != splashFrameDuration * splashFrameCount ||
            splash.AnimationFrame != 0)
        {
            throw new InvalidOperationException(
                $"{terrainName} splash did not start with the original interaction timing.");
        }
        splash.Advance((splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} splash did not hold its first OAM record for {splashFrameDuration} updates.");
        splash.Advance(1.0 / 60.0);
        if (splash.AnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} splash did not advance to its second OAM record.");
        splash.Advance((splash.DurationFrames - splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != splashFrameCount - 1 || splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not reach and hold its final OAM record.");
        splash.Advance(1.0 / 60.0);
        if (!splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not delete after {splash.DurationFrames} updates.");
        if (!_player.IsDrowning || !_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} terrain did not begin visible LINK_ANIM_MODE_DROWN frame $d4.");
        if (_sound.LastPlayRequest != OracleSoundEngine.SndSplash ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDamageLink) != damageSoundRequests + 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != splashSoundRequests + 1)
        {
            throw new InvalidOperationException(
                $"{terrainName} drowning did not request SND_DAMAGE_LINK `$5f followed by " +
                "the splash interaction's SND_SPLASH `$87 exactly once.");
        }
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the drowning animation finished.");

        _player._PhysicsProcess(5.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} did not hold directional drowning frame $d4 for six updates.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} did not advance to drowning frame $0b after six updates.");

        _player._PhysicsProcess(15.0 / 60.0);
        if (!_player.Visible || _player.Position != hazardPosition)
            throw new InvalidOperationException(
                $"{terrainName} moved or hid Link before the 22-update drowning animation finished.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible || !_player.IsDrowning)
            throw new InvalidOperationException(
                $"{terrainName} did not hide Link after the 22-update drowning animation.");
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the two-update respawn delay.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible)
            throw new InvalidOperationException(
                $"{terrainName} did not preserve the first invisible respawn update.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.IsDrowning || !_player.Visible ||
            _player.Position.DistanceSquaredTo(safePosition) > 1.0f)
        {
            throw new InvalidOperationException(
                $"{terrainName} did not return Link to the last safe tile after drowning.");
        }
        if (_player.HealthQuarters != healthBeforeDrowning - 2)
            throw new InvalidOperationException(
                $"{terrainName} did not apply one half-heart after Link reappeared.");
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndDamageLink) != damageSoundRequests + 1)
            throw new InvalidOperationException($"{terrainName} respawn replayed SND_DAMAGE_LINK $5f.");
    }

    private void ValidateRoom56TileEdgeSlide()
    {
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x56);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();

        // The left end of bridge metatiles $1d/$1e starts at x=$30. These two
        // positions reproduce approaching its upper and lower rails from the
        // west, including the lower corner shown in the reported stuck case.
        ValidateBridgeSlidePath(new Vector2(44, 62), expectUp: true);
        ValidateBridgeSlidePath(new Vector2(44, 92), expectUp: false);

        // A fractional fixed-point remainder is possible when Link walks onto
        // the bridge after moving over slower terrain. At y=70.5 the upper
        // rail correctly blocks another upward step, while the original yh
        // coordinate (and therefore the rendered sprite) remains row 70.
        Vector2 fractionalStop = new(56, 70.5f);
        if (_collision.ResolveMovement(
                fractionalStop, Vector2.Up, allowWallSlide: true) != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "Room 0:56's upper bridge rail did not block the fractional direct approach.");
        }

        _player.WarpTo(fractionalStop, recordSafe: false);
        if (_player.Position != new Vector2(56, 70))
        {
            throw new InvalidOperationException(
                "Link's fixed-point bridge position was rounded instead of rendered from yh/xh.");
        }
    }

    private void ValidateBridgeSlidePath(Vector2 start, bool expectUp)
    {
        Vector2 position = start;
        bool deflected = false;
        for (int frame = 0; frame < 16; frame++)
        {
            Vector2 resolved = _collision.ResolveMovement(
                position, Vector2.Right, allowWallSlide: true);
            if (resolved == Vector2.Zero)
                throw new InvalidOperationException(
                    $"Room 0:56 bridge slide became stuck at {position}.");
            deflected |= expectUp ? resolved.Y < 0.0f : resolved.Y > 0.0f;
            position += resolved;
        }

        Vector2 expected = start + new Vector2(12.0f, expectUp ? -4.0f : 4.0f);
        if (!deflected || position.DistanceSquaredTo(expected) > 0.01f)
            throw new InvalidOperationException(
                $"Room 0:56 bridge did not apply the symmetric four-frame " +
                $"{(expectUp ? "upward" : "downward")} deflection; " +
                $"start={start}, expected={expected}, end={position}.");
    }

    private void ValidateRoom01HoleBoundaryCase()
    {
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x01);
        _roomView.SetRoom(_currentRoom.Texture);

        if (!TryFindHoleWithSafeNeighbor(_currentRoom, out Vector2 holeCenter, out Vector2 safePosition))
            throw new InvalidOperationException("Room 0:01 did not have a testable hole with a safe neighbor.");

        _player.RefillHealth();
        _player.WarpTo(safePosition);
        int beforeHealth = _player.HealthQuarters;

        float tileTop = Mathf.FloorToInt(holeCenter.Y / OracleRoomData.MetatileSize) *
            OracleRoomData.MetatileSize;
        Vector2 boundaryEntry = new(holeCenter.X, tileTop - 5.0f + 0.6f);
        _player.WarpTo(boundaryEntry, recordSafe: false);
        Vector2 expectedCenter = GetActiveTerrain(_player.Position).TileCenter;
        if (expectedCenter.DistanceSquaredTo(holeCenter) > 1.0f)
            throw new InvalidOperationException("Room 0:01 boundary setup did not sample the hole tile.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                "Room 0:01 boundary hole entry did not start the pull-in state.");

        AdvanceHolePullUntilFall(holeCenter);
        AdvanceHoleFallUntilRespawn(safePosition);
        if (_player.IsFallingInHole)
            throw new InvalidOperationException("Room 0:01 boundary hole fall did not complete.");
        if (_player.Position.DistanceSquaredTo(safePosition) > 1.0f)
            throw new InvalidOperationException("Room 0:01 hole respawn did not return to the room entry anchor.");
        if (_player.HealthQuarters != beforeHealth - 2)
            throw new InvalidOperationException("Room 0:01 hole fall did not apply half-heart damage.");
    }

    private void AdvanceHolePullUntilFall(Vector2 expectedCenter)
    {
        for (int i = 0; i < 120 && !_player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (!_player.IsFallingInHole)
            throw new InvalidOperationException("Hole pull-in did not transition to the fall animation.");
        if (_player.Position.DistanceSquaredTo(expectedCenter) > 1.0f)
            throw new InvalidOperationException("Hole pull-in did not center Link on the sampled hole tile.");
    }

    private void AdvanceHoleFallUntilRespawn(Vector2 expectedRespawn)
    {
        for (int i = 0; i < 80 && _player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (_player.IsFallingInHole)
            throw new InvalidOperationException("The falling-in-hole animation did not finish.");
        if (_player.Position.DistanceSquaredTo(expectedRespawn) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the stored respawn anchor.");
    }

    private static bool TryFindHoleWithSafeNeighbor(
        OracleRoomData room,
        out Vector2 holeCenter,
        out Vector2 safePosition)
    {
        for (int tileY = 1; tileY < room.HeightInTiles; tileY++)
        for (int tileX = 0; tileX < room.WidthInTiles; tileX++)
        {
            Vector2 center = new(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            OracleRoomData.TerrainInfo terrain = room.GetTerrainInfo(center);
            if (terrain.Hazard != OracleRoomData.HazardType.Hole)
                continue;

            foreach (Vector2I direction in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                Vector2 candidate = center + (Vector2)direction * OracleRoomData.MetatileSize;
                if (candidate.X < 0 || candidate.X >= room.Width ||
                    candidate.Y < 0 || candidate.Y >= room.Height)
                {
                    continue;
                }

                OracleRoomData.TerrainInfo safeTerrain = room.GetTerrainInfo(candidate);
                if (safeTerrain.Hazard == OracleRoomData.HazardType.None &&
                    !RoomCollides(room, candidate))
                {
                    holeCenter = center;
                    safePosition = candidate;
                    return true;
                }
            }
        }

        holeCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private bool TryFindTerrainSample(
        OracleRoomData.HazardType hazard,
        out int group,
        out int room,
        out Vector2 hazardCenter,
        out Vector2 safePosition)
    {
        for (int candidateGroup = 0; candidateGroup <= 5; candidateGroup++)
        for (int candidateRoom = 0; candidateRoom <= 0xff; candidateRoom++)
        {
            if (!_world.HasRoom(candidateGroup, candidateRoom))
                continue;

            OracleRoomData data = _world.LoadRoom(candidateGroup, candidateRoom);
            Vector2? safe = null;
            Vector2? target = null;

            for (int tileY = 0; tileY < data.HeightInTiles; tileY++)
            for (int tileX = 0; tileX < data.WidthInTiles; tileX++)
            {
                Vector2 center = new(
                    tileX * OracleRoomData.MetatileSize + 8,
                    tileY * OracleRoomData.MetatileSize + 8);
                OracleRoomData.TerrainInfo terrain = data.GetTerrainInfo(center);

                if (terrain.Hazard == OracleRoomData.HazardType.None &&
                    safe == null &&
                    !RoomCollides(data, center))
                {
                    safe = center;
                }
                if (terrain.Hazard == hazard &&
                    terrain.Type == OracleRoomData.TerrainType.Hole &&
                    !RoomCollides(data, center))
                {
                    target = center;
                }
            }

            if (safe != null && target != null)
            {
                group = candidateGroup;
                room = candidateRoom;
                hazardCenter = target.Value;
                safePosition = safe.Value;
                return true;
            }
        }

        group = -1;
        room = -1;
        hazardCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private static bool RoomCollides(OracleRoomData room, Vector2 playerPosition)
    {
        foreach (Vector2 offset in new[]
        {
            new Vector2(-5, -2), new Vector2(5, -2),
            new Vector2(-5, 5), new Vector2(5, 5)
        })
        {
            Vector2 sample = playerPosition + offset;
            if (sample.X < 0 || sample.X >= room.Width ||
                sample.Y < 0 || sample.Y >= room.Height ||
                room.IsSolid(sample))
            {
                return true;
            }
        }
        return false;
    }
}
