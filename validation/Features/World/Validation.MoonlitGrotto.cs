using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom456MoonlitGrotto()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static Vector2 Point(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }

        var data = new DungeonMechanicDatabase();
        FailIf(
            data.GetRoomRecords(4, 0x56) is not
                [{ Order: 0, Id: 0x21, SubId: 0x0a }],
            "Room 4:56 lost its source-order INTERAC_DUNGEON_EVENTS $21:$0a.");

        _saveData.SetRoomFlag(
            4, 0x56, OracleSaveData.RoomFlagItem, value: false);
        _runtimeState.SetWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress, 0xff);
        _runtimeState.SetWramByte(OracleRuntimeState.ArmosTriggerAddress, 0);

        // INTERAC_DUNGEON_EVENTS state 0 still runs while the destination is
        // preloaded. Its PART_ORB child must therefore already be drawn at the
        // incoming offset during the 4:57 -> 4:56 leftward scroll, while the
        // event's state-1 watcher remains frozen.
        OracleRoomData transitionSource = _world.LoadRoom(4, 0x57);
        OracleRoomData transitionDestination = _world.LoadRoom(4, 0x56);
        _entities.LoadRoom(4, transitionSource);
        Vector2 incomingOffset = Vector2.Left * transitionDestination.Width;
        _entities.BeginScreenTransition(
            4, transitionDestination, incomingOffset);
        MoonlitGrottoOrbRoomEntity preloadedOrb =
            _entities.Entities<MoonlitGrottoOrbRoomEntity>().Single();
        MoonlitGrottoArmosEventRoomEntity preloadedEvent =
            _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Single();
        FailIf(
            !preloadedEvent.Initialized || preloadedEvent.Visible ||
            !preloadedOrb.Visible ||
            preloadedOrb.TransitionDrawOffset != incomingOffset ||
            preloadedOrb.Position != Point(0x75) || preloadedOrb.IsOn ||
            _entities.Entities<ArmosCharacter>().Count != 0,
            "Room 4:56 did not preload its visible PART_ORB at the incoming " +
            "screen-transition offset while keeping the Armos watcher frozen.");
        _entities.Update(1.0, _player);
        FailIf(
            !preloadedOrb.Visible || preloadedOrb.IsOn ||
            _entities.Entities<ArmosCharacter>().Count != 0,
            "Room 4:56 advanced its orb event while destination entities " +
            "were frozen during the screen transition.");
        _entities.FinishScreenTransition();

        LoadValidationRoom(4, 0x56);
        FailIf(
            _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Count != 1 ||
            _entities.Entities<MoonlitGrottoOrbRoomEntity>().Count != 0 ||
            _entities.Entities<ItemDropProducer>().Select(value => value.Position)
                .ToArray() is not [{ X: 0x98, Y: 0x18 }, { X: 0x98, Y: 0x28 }] ||
            _currentRoom.GetMetatile(Point(0x44)) != 0x26 ||
            _currentRoom.GetMetatile(Point(0x69)) == data.ChestTile,
            "Room 4:56 did not retain its event-first stream, two fixed drop " +
            "producers, hidden $26 statue, and unspawned Compass chest.");

        Step();
        MoonlitGrottoOrbRoomEntity orb =
            _entities.Entities<MoonlitGrottoOrbRoomEntity>().Single();
        FailIf(
            orb.Position != Point(0x75) || orb.ToggleMask != 0x10 ||
            orb.IsOn || orb.Palette != 1 ||
            _currentRoom.GetTerrainInfo(orb.Position).Collision != 0x0a ||
            OracleGraphicsCache.PixelHash(orb.CurrentTexture.GetImage()) == 0 ||
            (_runtimeState.ReadWramByte(
                OracleRuntimeState.ToggleBlocksStateAddress) & 0x10) != 0,
            "The $21:$0a initializer did not clear toggle bit $10 and create " +
            "PART_ORB $03:$04 at $75 with palette 1 and collision $0a.");

        _sound.ClearPlayRequestAudit();
        var hitSpawns = new List<RoomEntitySpawn>();
        orb.ApplySwordHit(
            orb.CollisionBounds,
            orb.Position,
            damage: 2,
            EnemyKnockbackStrength.Low,
            hitSpawns);
        FailIf(
            !orb.IsOn || orb.Palette != 2 ||
            orb.HitLockout != data.SwitchHitLockout ||
            _sound.PlayRequestsFor(data.SwitchSound) != 1,
            "PART_ORB did not XOR bit $10, select palette 2, and play " +
            "SND_SWITCH with ENEMYDMG_34's lockout on an active collision.");

        // ITEM_BOMB retains an active explosion collision for multiple
        // updates. ENEMYDMG_34 must suppress every overlapping update after
        // the first so the orb neither toggles back nor restarts SND_SWITCH.
        orb.ApplyItemCollision(
            RoomEntityItemCollision.Bomb,
            orb.CollisionBounds,
            orb.Position,
            damage: 4,
            hitSpawns);
        FailIf(
            !orb.IsOn || orb.HitLockout != data.SwitchHitLockout ||
            _sound.PlayRequestsFor(data.SwitchSound) != 1,
            "A continuing bomb explosion retriggered PART_ORB during its " +
            "ENEMYDMG_34 collision lockout.");

        Step();
        ArmosCharacter armos = _entities.Entities<ArmosCharacter>().Single();
        EnemyClearChestRoomEntity chest =
            _entities.Entities<EnemyClearChestRoomEntity>().Single();
        FailIf(
            _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Count != 0 ||
            _entities.Entities<ArmosSpawnerRoomEntity>().Count != 0 ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.ArmosTriggerAddress) != 1 ||
            armos.Position != new Vector2(0x48, 0x46) ||
            armos.State != ArmosState.Waiting || armos.Visible ||
            chest.Position != Point(0x69) || chest.Counter != 0,
            "Orb activation did not set $cca2, expand the $26 statue into a " +
            "hidden red Armos at source offset +$06, and arm the $69 chest.");

        Step();
        FailIf(
            armos.State != ArmosState.Activating || chest.Counter != 0,
            "The Armos did not consume $cca2 while the enemy-clear chest " +
            "continued to observe a nonzero enemy count.");
        Step();
        FailIf(
            armos.State != ArmosState.Flickering || armos.Counter != 60 ||
            armos.Position != Point(0x44) || !armos.Visible ||
            armos.CollisionEnabled,
            "Red Armos state 9 did not move Y by two, become visible, and " +
            "start its exact 60-update non-colliding flicker.");
        Step(59);
        FailIf(
            armos.Counter != 1 || armos.CollisionEnabled ||
            _currentRoom.GetMetatile(Point(0x44)) != 0x26,
            "Red Armos ended its flicker or replaced the statue before " +
            "counter update 60.");
        Step();
        FailIf(
            armos.State != ArmosState.ChoosingDirection ||
            !armos.CollisionEnabled || !armos.Visible ||
            _currentRoom.GetMetatile(Point(0x44)) != 0xa0 ||
            armos.ActiveCollisionMode != 0x1e,
            "Red Armos did not enable collision mode $1e/radius 6 and replace " +
            "its underlying $26 statue with $a0 on the counter-zero update.");

        OracleRandomState beforeArmosDirection = _random.CaptureState();
        var expectedArmosRandom = new OracleRandom();
        expectedArmosRandom.RestoreState(beforeArmosDirection);
        OracleRandomResult directionRoll = expectedArmosRandom.Next();
        int expectedArmosAngle = directionRoll.Value & 0x18;
        Vector2 positionBeforeMovement = armos.Position;
        var expectedArmosPosition = new Node2D
        {
            Position = positionBeforeMovement
        };
        var expectedArmosMovement = new EnemyTerrainMovement(
            expectedArmosPosition, _currentRoom);
        expectedArmosMovement.MoveUsingAdjacentWalls(
            expectedArmosAngle,
            armos.SpeedRaw,
            allowHoles: false,
            topDown: true);
        Vector2 expectedArmosMovementPosition = expectedArmosPosition.Position;
        expectedArmosPosition.Free();
        Step();
        FailIf(
            _random.Calls != beforeArmosDirection.Calls + 1 ||
            armos.Angle != expectedArmosAngle ||
            armos.State != ArmosState.Moving || armos.Counter != 60 ||
            !armos.Position.IsEqualApprox(expectedArmosMovementPosition),
            "Red Armos did not use ecom_setRandomCardinalAngle's returned " +
            "RNG byte and immediately perform the first SPEED_80 movement " +
            "update after loading counter 61 " +
            $"(calls={_random.Calls}/{beforeArmosDirection.Calls + 1}, " +
            $"roll=${directionRoll.Value:x2}/${directionRoll.High:x2}, " +
            $"angle=${armos.Angle:x2}/${expectedArmosAngle:x2}, " +
            $"state={armos.State}, counter={armos.Counter}, " +
            $"position={armos.Position}/{expectedArmosMovementPosition}).");

        int healthBeforeSword = armos.Health;
        bool swordAccepted = _entities.ApplySwordHit(
            armos.CollisionBounds,
            armos.Position,
            damage: 2,
            EnemyKnockbackStrength.Low,
            swordState: SwordActionState.Swing,
            swordLevel: 1);
        FailIf(
            !swordAccepted || armos.Health != healthBeforeSword ||
            armos.InvincibilityCounter != -0x1c ||
            _entities.Entities<ClinkEffect>().Count != 1,
            "ENEMYCOLLISION_ACTIVE_RED_ARMOS $1e did not armor an L1 sword " +
            "hit with ENEMYDMG_34 and a clink.");

        armos.InvincibilityCounter = 0;
        var adapter = new ArmosRoomEntity(armos);
        var bombSpawns = new List<RoomEntitySpawn>();
        bool bombAccepted = adapter.ApplyItemCollision(
            RoomEntityItemCollision.Bomb,
            armos.CollisionBounds,
            armos.Position,
            damage: 4,
            bombSpawns);
        FailIf(
            !bombAccepted || !armos.IsDead ||
            bombSpawns is not [EnemyDeathPuffSpawn
                { EnemyId: 0x1d, DecrementsRoomCount: true }],
            "The red-Armos collision row did not apply bomb damage without " +
            "knockback and transfer its room count to the death puff.");

        Step();
        FailIf(
            chest.Counter != 30 ||
            _entities.Entities<PuzzlePuffEffect>().Count != 1,
            "Defeating the red Armos did not start createChestWhenNoEnemies " +
            "with SND_SOLVEPUZZLE, puff, and wait 30.");
        Step(29);
        FailIf(
            chest.Counter != 1 ||
            _currentRoom.GetMetatile(Point(0x69)) == data.ChestTile,
            "Room 4:56's Compass chest appeared before wait update 30.");
        Step();
        FailIf(
            _entities.Entities<EnemyClearChestRoomEntity>().Count != 0 ||
            _currentRoom.GetMetatile(Point(0x69)) != data.ChestTile,
            "Room 4:56 did not create TILEINDEX_CHEST $f1 at packed $69 on " +
            "the exact enemy-clear boundary.");

        _saveData.SetRoomFlag(
            4, 0x56, OracleSaveData.RoomFlagItem);
        _runtimeState.SetWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress, 0xff);
        LoadValidationRoom(4, 0x56);
        Step();
        FailIf(
            _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Count != 0 ||
            _entities.Entities<MoonlitGrottoOrbRoomEntity>().Count != 1 ||
            _entities.Entities<ArmosCharacter>().Count != 0 ||
            _entities.Entities<EnemyClearChestRoomEntity>().Count != 0 ||
            (_runtimeState.ReadWramByte(
                OracleRuntimeState.ToggleBlocksStateAddress) & 0x10) != 0 ||
            _currentRoom.GetMetatile(Point(0x44)) != 0xa0,
            "ROOMFLAG_ITEM re-entry did not retain the orb, clear bit $10, " +
            "suppress the watcher/Armos/chest trigger, and apply the $44:$a0 " +
            "single-tile change.");

        GD.Print(
            "Validated full room 4:56: event-first stream, orb $03:$04, " +
            "toggle bit $10/palettes/SND_SWITCH, dynamic red Armos statue " +
            "expansion, exact 60/61 timing and top-down activation, armored " +
            "sword plus bomb collision, enemy-clear Compass chest $69, two " +
            "drop producers, and ROOMFLAG_ITEM re-entry.");
    }

    private void ValidateRoom45eMoonlitGrotto()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static Vector2 Point(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }

        var data = new DungeonMechanicDatabase();
        FailIf(
            data.GetRoomRecords(4, 0x5e) is not
                [{ Order: 0, Id: 0x21, SubId: 0x0c },
                 { Order: 1, Id: 0x09, SubId: 0x00,
                   PackedPosition: 0x19 }],
            "Room 4:5e lost its source-ordered $21:$0c event and one-shot " +
            "PART_BUTTON $09:$00 at $19.");

        _saveData.SetRoomFlag(
            4, 0x5e, OracleSaveData.RoomFlagItem, value: false);
        _runtimeState.SetWramByte(OracleRuntimeState.ArmosTriggerAddress, 0);
        LoadValidationRoom(4, 0x5e);
        OracleRoomData room = _currentRoom;
        Vector2 buttonPosition = Point(0x19);
        int[] statuePositions = [0x33, 0x37, 0x73, 0x77];
        FailIf(
            room.Width != 240 || room.Height != 176 ||
            _entities.RoomEnemyCount != 0 ||
            _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Count != 1 ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ SubId: 0x00, PackedPosition: 0x19,
                   TriggerBit: 0, Reusable: false }] ||
            room.GetMetatile(buttonPosition) != data.ButtonTile ||
            statuePositions.Any(position =>
                room.GetMetatile(Point(position)) != data.MoonlitArmosSourceTile),
            "Room 4:5e did not load its large-room event/button stream and " +
            "four $26 Armos statues without counting them as enemies.");

        _player.WarpTo(new Vector2(0x78, 0x58));
        Step();
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(buttonPosition);
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x01 ||
            room.GetMetatile(buttonPosition) != data.PressedButtonTile ||
            _entities.Entities<GroundButtonRoomEntity>().Count != 0 ||
            _entities.Entities<ArmosCharacter>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 1,
            "Room 4:5e's one-shot button did not latch trigger bit 0 before " +
            "the earlier $21:$0c event observed it.");

        Step();
        List<ArmosCharacter> armos = _entities.Entities<ArmosCharacter>();
        Vector2[] expectedHiddenPositions =
        [
            new(0x38, 0x36), new(0x78, 0x36),
            new(0x38, 0x76), new(0x78, 0x76)
        ];
        FailIf(
            _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Count != 0 ||
            _entities.Entities<ArmosSpawnerRoomEntity>().Count != 0 ||
            _entities.Entities<DungeonRewardRoomEntity>().Count != 1 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.ArmosTriggerAddress) != 0x01 ||
            _entities.RoomEnemyCount != 4 || armos.Count != 4 ||
            !armos.Select(value => value.Position)
                .SequenceEqual(expectedHiddenPositions) ||
            armos.Any(value =>
                value.State != ArmosState.Waiting || value.Visible),
            "Room 4:5e's $21:$0c event did not copy wActiveTriggers to $cca2, " +
            "create the $12:$01 key watcher first, and scan all four $26 " +
            "statues into hidden red Armos in room-layout order.");

        Step();
        FailIf(
            armos.Any(value => value.State != ArmosState.Activating) ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room 4:5e's Armos did not consume $cca2 while the falling-key " +
            "watcher continued waiting on the live enemy count.");
        Step();
        FailIf(
            armos.Any(value =>
                value.State != ArmosState.Flickering ||
                value.Counter != 60 || !value.Visible ||
                value.CollisionEnabled) ||
            !armos.Select(value => value.Position)
                .SequenceEqual(statuePositions.Select(Point)),
            "Room 4:5e's four red Armos did not enter their exact visible, " +
            "non-colliding 60-update activation at the statue centers.");
        Step(60);
        FailIf(
            armos.Any(value =>
                value.State != ArmosState.ChoosingDirection ||
                !value.CollisionEnabled) ||
            statuePositions.Any(position =>
                room.GetMetatile(Point(position)) !=
                    data.MoonlitArmosReplacementTile),
            "Room 4:5e's four Armos did not replace every $26 statue with " +
            "$a0 and enable collision on activation update 60.");

        foreach (ArmosCharacter enemy in armos)
        {
            var deathSpawns = new List<RoomEntitySpawn>();
            bool accepted = new ArmosRoomEntity(enemy).ApplyItemCollision(
                RoomEntityItemCollision.Bomb,
                enemy.CollisionBounds,
                enemy.Position,
                damage: 4,
                deathSpawns);
            FailIf(
                !accepted || !enemy.IsDead ||
                deathSpawns is not [EnemyDeathPuffSpawn
                    { EnemyId: 0x1d, DecrementsRoomCount: true }],
                "Room 4:5e's active red Armos did not retain its bomb-only " +
                "damage/death-count behavior.");
        }
        FailIf(_entities.RoomEnemyCount != 0,
            "Room 4:5e retained an enemy count after all four Armos died.");

        Step();
        GroundTreasurePickup fallingKey =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            _entities.Entities<DungeonRewardRoomEntity>().Count != 0 ||
            fallingKey.Position != new Vector2(0x58, 0x58) ||
            fallingKey.Record.TreasureObject != "TREASURE_OBJECT_SMALL_KEY_01" ||
            fallingKey.Record.SpawnMode != 2 || fallingKey.Record.GrabMode != 2 ||
            fallingKey.Record.SpawnDelayFrames != 40 ||
            fallingKey.Record.BounceCount != 2 ||
            fallingKey.Record.Gravity != 0x10 ||
            fallingKey.Record.BounceSpeed != -0xaa ||
            !fallingKey.Record.InitialZAboveScreen,
            "Defeating room 4:5e's four Armos did not make $12:$01 spawn " +
            "the original falling small key at exact Y/X $58/$58.");

        _sound.ClearPlayRequestAudit();
        Step(2);
        FailIf(
            fallingKey.State != PickupState.Spawning ||
            fallingKey.SpawnSubstate != 1 || fallingKey.SpawnCounter != 40 ||
            fallingKey.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
            "Room 4:5e's key did not begin its source 40-update hidden " +
            "SND_SOLVEPUZZLE delay.");
        for (int frame = 0;
             frame < 300 && fallingKey.State != PickupState.Waiting;
             frame++)
        {
            Step();
        }
        int dungeon = _rooms.CurrentDungeonIndex;
        int keysBeforePickup = _inventory.GetDungeonSmallKeys(dungeon);
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(fallingKey.Position, recordSafe: false);
        Step();
        FailIf(
            fallingKey.State != PickupState.Collected ||
            _inventory.GetDungeonSmallKeys(dungeon) != keysBeforePickup + 1 ||
            !_saveData.HasRoomFlag(4, 0x5e, OracleSaveData.RoomFlagItem) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 1,
            "Room 4:5e's landed key did not grant one dungeon-$03 small key " +
            "and set ROOMFLAG_ITEM on contact.");

        // The room has no item-flag tile substitution. Re-entry restores all
        // four statues and allows the button event to run again, while the
        // dynamically created $12:$01 script immediately suppresses a second
        // key because stopifitemflagset is its first command.
        LoadValidationRoom(4, 0x5e);
        room = _currentRoom;
        Step();
        _player.WarpTo(buttonPosition, recordSafe: false);
        Step(2);
        Step();
        FailIf(
            _entities.Entities<DungeonRewardRoomEntity>().Count != 0 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.Entities<ArmosCharacter>().Count != 4 ||
            statuePositions.Any(position =>
                room.GetMetatile(Point(position)) != data.MoonlitArmosSourceTile),
            "ROOMFLAG_ITEM re-entry did not restore room 4:5e's statues, " +
            "rerun its Armos event, and suppress only the second key watcher.");

        GD.Print(
            "Validated full room 4:5e: source-ordered one-shot button, " +
            "dynamic four-Armos scan/activation, bomb-only kills, falling " +
            "small key $58/$58, collection, and ROOMFLAG_ITEM re-entry.");
    }

    private void ValidateRoom458MoonlitGrotto()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static Vector2 Point(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }

        var enemyData = new EnemyDatabase();
        IReadOnlyList<RoomObjectRecord> objects =
            enemyData.GetRoomObjects(4, 0x58);
        FailIf(
            objects is not
                [{ Order: 0, Kind: RoomObjectKind.FixedEnemy,
                   Id: 0x4e, SubId: 0x00, Flags: 0x00, Count: 1,
                   Y: 0x48, X: 0x58, PackedPosition: 0x45 },
                 { Order: 1, Kind: RoomObjectKind.FixedEnemy,
                   Id: 0x4e, SubId: 0x00, Flags: 0x00, Count: 1,
                   Y: 0x68, X: 0x98, PackedPosition: 0x69 },
                 { Order: 2, Kind: RoomObjectKind.RandomEnemy,
                   Id: 0x4f, SubId: 0x00, Flags: 0x40, Count: 2 }],
            "Room 4:58 lost its two fixed Arm Mimics followed by two " +
            "random Moldorms in source object order.");
        var mechanics = new DungeonMechanicDatabase();
        FailIf(
            mechanics.GetRoomRecords(4, 0x58) is not
                [{ Order: 0, Id: 0x12, SubId: 0x02,
                   PackedPosition: 0x59, Parameter: 0x00,
                   Predicate: TriggerPredicate.None,
                   CountSourceComplete: true }],
            "Room 4:58 lost INTERAC_DUNGEON_STUFF $12:$02 and its " +
            "enemy-clear Seed Shooter chest at $59.");
        var chests = new ChestDatabase();
        FailIf(
            !chests.TryGet(4, 0x58, 0x59, out ChestRecord shooterChest) ||
            shooterChest is not
                {
                    TreasureObject: "TREASURE_OBJECT_SHOOTER_00",
                    TreasureId: 0x0f, SubId: 0x00, Parameter: 0x01
                },
            "Room 4:58's $59 chest lost its Seed Shooter treasure row.");

        ImportedEnemyDefinition definition = enemyData.ImportedEnemy(0x4e);
        EnemyHandlerDescriptor handler =
            enemyData.EnemyHandlers.ResolveHandler(objects[0]);
        FailIf(
            definition is not
                {
                    TileBase: 0, Palette: 3,
                    RadiusY: 6, RadiusX: 6,
                    DamageQuarters: 4, Health: 5,
                    Animations.Length: 4
                } ||
            handler is not
                {
                    CollisionMode: 0xb9,
                    Classification:
                        EnemyHandlerClassification.OrderedImplemented,
                    Handler: EnemyHandlerKind.ArmMimic,
                    EnemyName: "ENEMY_ARM_MIMIC",
                    ShieldLevel1Effect: 0x10,
                    ShieldLevel2Effect: 0x0f,
                    ShieldLevel3Effect: 0x0f
                },
            "ENEMY_ARM_MIMIC $4e:$00 lost its imported definition, " +
            "collision mode, handler, or shield row.");

        OracleRoomData movementRoom = _world.LoadRoom(4, 0x58);
        var movementMimic = new ArmMimicCharacter();
        movementMimic.Initialize(definition, movementRoom, Point(0x45));
        movementMimic.UpdateFrame(0xff, Vector2I.Up);
        FailIf(
            !movementMimic.Initialized || !movementMimic.Visible ||
            movementMimic.Direction != 2 || movementMimic.AnimationIndex != 2 ||
            movementMimic.Position != Point(0x45) ||
            movementMimic.SpeedRaw != 0x28,
            "Arm Mimic state 0 did not face opposite Link and become " +
            "visible at SPEED_100 without moving.");
        int stoppedFrame = movementMimic.AnimationFrame;
        movementMimic.UpdateFrame(0xff, Vector2I.Right);
        FailIf(
            movementMimic.Position != Point(0x45) ||
            movementMimic.AnimationFrame != stoppedFrame ||
            movementMimic.Direction != 2,
            "wLinkAngle=$ff did not return before Arm Mimic movement, " +
            "direction changes, and enemyAnimate.");

        Vector2 movementOrigin = movementMimic.Position;
        var expectedNode = new Node2D { Position = movementOrigin };
        var expectedMovement = new EnemyTerrainMovement(
            expectedNode, movementRoom);
        expectedMovement.MoveUsingAdjacentWalls(
            0x18,
            movementMimic.SpeedRaw,
            allowHoles: false,
            topDown: false);
        Vector2 expectedPosition = expectedNode.Position;
        movementMimic.UpdateFrame(0x08, Vector2I.Right);
        FailIf(
            movementMimic.Angle != 0x18 ||
            movementMimic.Direction != 3 ||
            movementMimic.AnimationIndex != 3 ||
            !movementMimic.Position.IsEqualApprox(expectedPosition),
            "Arm Mimic did not reverse Link's rightward angle/facing and " +
            "apply the side-view no-holes movement helper.");
        expectedNode.Free();
        movementMimic.Free();

        ArmMimicRoomEntity Combatant(out ArmMimicCharacter mimic)
        {
            mimic = new ArmMimicCharacter();
            mimic.Initialize(definition, movementRoom, Point(0x45));
            mimic.UpdateFrame(0xff, Vector2I.Up);
            return new ArmMimicRoomEntity(
                mimic,
                handler.CombatSource(objects[0], killableEnemyIndex: 1),
                static _ => { });
        }

        ArmMimicRoomEntity sword = Combatant(out ArmMimicCharacter swordMimic);
        var combatSpawns = new List<RoomEntitySpawn>();
        FailIf(
            !sword.ApplySwordHit(
                swordMimic.CollisionBounds,
                swordMimic.Position,
                damage: 1,
                EnemyKnockbackStrength.Low,
                combatSpawns) ||
            swordMimic.Health != 4 ||
            swordMimic.InvincibilityCounter != 0x10 ||
            swordMimic.KnockbackCounter != 0x08,
            "Arm Mimic's L1 sword effect $08 did not damage it with the " +
            "low-recoil ENEMYDMG_$00 profile.");
        swordMimic.Free();

        ArmMimicRoomEntity bomb = Combatant(out ArmMimicCharacter bombMimic);
        combatSpawns.Clear();
        FailIf(
            !bomb.ApplyItemCollision(
                RoomEntityItemCollision.Bomb,
                bombMimic.CollisionBounds,
                bombMimic.Position,
                damage: 4,
                combatSpawns) ||
            bombMimic.Health != 1 ||
            bombMimic.InvincibilityCounter != 0x1a ||
            bombMimic.KnockbackCounter != 0x0f,
            "Arm Mimic's bomb collision effect $0a did not apply high " +
            "damage recoil without inventing armor.");
        bombMimic.Free();

        ArmMimicRoomEntity mystery =
            Combatant(out ArmMimicCharacter mysteryMimic);
        combatSpawns.Clear();
        FailIf(
            mystery.ApplySeedHit(
                mysteryMimic.CollisionBounds,
                mysteryMimic.Position,
                seedItem: 0x24,
                combatSpawns) != SeedHitResult.Activate ||
            !mysteryMimic.IsDead ||
            combatSpawns is not
                [EnemyDeathPuffSpawn
                    { EnemyId: 0x4e, DecrementsRoomCount: true }],
            "Arm Mimic's Mystery Seed collision effect $35 did not force " +
            "zero health and transfer its enemy count to the death puff.");
        mysteryMimic.Free();

        ArmMimicRoomEntity ember = Combatant(out ArmMimicCharacter emberMimic);
        combatSpawns.Clear();
        FailIf(
            ember.ApplySeedHit(
                emberMimic.CollisionBounds,
                emberMimic.Position,
                seedItem: 0x20,
                combatSpawns) != SeedHitResult.Ignite,
            "Arm Mimic rejected its collisionEffect27 Ember burn.");
        ember.CompleteSeedBurn(combatSpawns);
        FailIf(emberMimic.Health != 3,
            "Arm Mimic's completed Ember burn did not apply two health units.");
        emberMimic.Free();

        _saveData.SetRoomFlag(
            4, 0x58, OracleSaveData.RoomFlagItem, value: false);

        OracleRoomData transitionSource = _world.LoadRoom(4, 0x57);
        OracleRoomData transitionDestination = _world.LoadRoom(4, 0x58);
        _entities.LoadRoom(4, transitionSource);
        Vector2 incomingOffset =
            Vector2.Right * transitionDestination.Width;
        _entities.BeginScreenTransition(
            4,
            transitionDestination,
            incomingOffset,
            Vector2I.Right,
            entryPackedPosition: 0x55);
        List<ArmMimicCharacter> incomingMimics =
            _entities.Entities<ArmMimicCharacter>();
        List<MoldormCharacter> incomingMoldorms =
            _entities.Entities<MoldormCharacter>();
        FailIf(
            incomingMimics.Count != 2 ||
            incomingMimics.Any(value =>
                !value.Initialized || !value.Visible ||
                value.Direction != 3 || value.AnimationIndex != 3 ||
                value.TransitionDrawOffset != incomingOffset) ||
            !incomingMimics.Select(value => value.Position)
                .SequenceEqual([Point(0x45), Point(0x69)]),
            "Room 4:58 scrolling preload did not run Arm Mimic source " +
            "state 0 with Link's right-facing direction and incoming offset.");
        FailIf(
            incomingMoldorms.Count != 2 ||
            incomingMoldorms.Any(value =>
                !value.Initialized || !value.Visible ||
                value.TurnCounter != 8 || value.AngularSpeed != 2 ||
                value.Angle is < 0 or > 0x1f ||
                value.Tail1Position != value.Position ||
                value.Tail2Position != value.Position ||
                value.TransitionDrawOffset != incomingOffset ||
                OracleGraphicsCache.PixelHash(
                    value.CurrentAnimationTexture.GetImage()) == 0),
            "Room 4:58 scrolling preload did not replace both Moldorm " +
            "controllers with visible initialized heads and coincident " +
            "tails at the incoming offset.");
        Vector2[] frozenMimicPositions =
            incomingMimics.Select(value => value.Position).ToArray();
        int[] frozenMimicFrames =
            incomingMimics.Select(value => value.AnimationFrame).ToArray();
        var frozenMoldormState = incomingMoldorms.Select(value =>
            (value.Position, value.Tail1Position, value.Tail2Position,
             value.Angle, value.AngularSpeed, value.TurnCounter,
             value.AnimationFrame)).ToArray();
        _entities.Update(1.0, _player);
        FailIf(
            !incomingMimics.Select(value => value.Position)
                .SequenceEqual(frozenMimicPositions) ||
            !incomingMimics.Select(value => value.AnimationFrame)
                .SequenceEqual(frozenMimicFrames) ||
            !incomingMoldorms.Select(value =>
                (value.Position, value.Tail1Position, value.Tail2Position,
                 value.Angle, value.AngularSpeed, value.TurnCounter,
                 value.AnimationFrame)).SequenceEqual(frozenMoldormState),
            "Room 4:58 advanced Arm Mimic or multipart Moldorm state while " +
            "destination entities were frozen during scrolling.");
        _entities.FinishScreenTransition();

        LoadValidationRoom(4, 0x58);
        List<ArmMimicCharacter> mimics =
            _entities.Entities<ArmMimicCharacter>();
        FailIf(
            _currentRoom.Width != 240 || _currentRoom.Height != 176 ||
            _entities.RoomEnemyCount != 4 ||
            !mimics.Select(value => value.Position)
                .SequenceEqual([Point(0x45), Point(0x69)]) ||
            _entities.Entities<MoldormCharacter>().Count != 2 ||
            _entities.Entities<EnemyClearChestRoomEntity>() is not
                [{ Position: { X: 0x98, Y: 0x58 }, Counter: 0 }] ||
            _currentRoom.GetMetatile(Point(0x59)) == mechanics.ChestTile,
            "Room 4:58 did not construct its four counted enemies and " +
            "hidden $59 Seed Shooter chest in source order.");
        Step();
        FailIf(
            mimics.Any(value => !value.Initialized || !value.Visible) ||
            _entities.Entities<MoldormCharacter>()
                .Any(value => !value.Initialized),
            "Room 4:58's Arm Mimics and Moldorms did not initialize on " +
            "their first active enemy update.");

        foreach (ArmMimicCharacter mimic in mimics)
            FailIf(!mimic.TakeSwordHit(mimic.Position, damage: 0x7f),
                "A room 4:58 Arm Mimic rejected a lethal test hit.");
        foreach (MoldormCharacter moldorm in
            _entities.Entities<MoldormCharacter>())
        {
            FailIf(!moldorm.TakeSwordHit(moldorm.Position, damage: 0x7f),
                "A room 4:58 Moldorm rejected a lethal test hit.");
        }
        _sound.ClearPlayRequestAudit();
        Step();
        EnemyClearChestRoomEntity chest =
            _entities.Entities<EnemyClearChestRoomEntity>().Single();
        FailIf(
            _entities.RoomEnemyCount != 0 ||
            _entities.Entities<ArmMimicCharacter>().Count != 0 ||
            _entities.Entities<MoldormCharacter>().Count != 0 ||
            chest.Counter != 30 ||
            _entities.Entities<PuzzlePuffEffect>().Count != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
            "Room 4:58 did not clear all four enemies and immediately " +
            "start the chest's exact 30-update solve wait, puff, and sound.");
        Step(29);
        FailIf(
            chest.Counter != 1 ||
            _currentRoom.GetMetatile(Point(0x59)) == mechanics.ChestTile,
            "Room 4:58 created its Seed Shooter chest before wait update 30.");
        Step();
        FailIf(
            _entities.Entities<EnemyClearChestRoomEntity>().Count != 0 ||
            _currentRoom.GetMetatile(Point(0x59)) != mechanics.ChestTile,
            "Room 4:58 did not create TILEINDEX_CHEST $f1 at $59 on the " +
            "exact enemy-clear boundary.");

        GD.Print(
            "Validated full room 4:58: two opposite-input Arm Mimics, " +
            "no-hole motion, collision row $39 weapon/seed responses, two " +
            "Moldorms, live enemy count, and delayed Seed Shooter chest $59.");
    }

    private void ValidateRoom45bMoonlitGrotto()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static Vector2 Point(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }

        var enemyData = new EnemyDatabase();
        FailIf(
            enemyData.GetRoomObjects(4, 0x5b) is not
                [{ Order: 0, Kind: RoomObjectKind.RandomEnemy,
                   Id: 0x52, SubId: 0x00, Flags: 0x21, Count: 1 },
                 { Order: 1, Kind: RoomObjectKind.RandomEnemy,
                   Id: 0x34, SubId: 0x01, Flags: 0x61, Count: 3 }],
            "Room 4:5b lost its source-ordered one ENEMY_FLYING_TILE " +
            "$52:$00 controller followed by three red Zols.");
        var mechanics = new DungeonMechanicDatabase();
        FailIf(
            mechanics.GetRoomRecords(4, 0x5b) is not
                [{ Order: 0, Id: 0x1e, SubId: 0x0b,
                   PackedPosition: 0x50 },
                 { Order: 1, Id: 0x1e, SubId: 0x0a,
                   PackedPosition: 0xa7 }],
            "Room 4:5b lost its left/down enemy-shutter stream.");

        int[] layout =
        [
            0x57, 0x56, 0x46, 0x47, 0x48, 0x58, 0x68, 0x67,
            0x66, 0x65, 0x55, 0x45, 0x36, 0x37, 0x38, 0x49,
            0x59, 0x69, 0x78, 0x77, 0x76, 0x54, 0x5a
        ];

        LoadValidationRoom(4, 0x5b);
        OracleRoomData room = _currentRoom;
        FlyingTileSpawnerRoomEntity spawner =
            _entities.Entities<FlyingTileSpawnerRoomEntity>().Single();
        FailIf(
            room.Width != 240 || room.Height != 176 ||
            _entities.RoomEnemyCount != 4 ||
            _entities.Entities<ZolCharacter>().Count != 3 ||
            _entities.Entities<FlyingTileCharacter>().Count != 0 ||
            _entities.Entities<DungeonDoorRoomEntity>().Select(door =>
                (door.SubId, door.PackedPosition,
                 door.EnemyCompletionSupported)).ToArray() is not
                [(0x0b, 0x50, true), (0x0a, 0xa7, true)] ||
            layout.Any(position => room.GetMetatile(Point(position)) != 0x9c) ||
            room.GetMetatile(Point(0x50)) != 0x7b ||
            room.GetMetatile(Point(0xa7)) != 0x7a ||
            !room.IsSolid(Point(0x50)) || !room.IsSolid(Point(0xa7)),
            "Room 4:5b did not load its large layout, 23 source $9c tiles, " +
            "four counted enemies, and closed left/down enemy shutters.");

        Step();
        FailIf(
            spawner.State != FlyingTileSpawnerState.Initializing ||
            _entities.Entities<FlyingTileCharacter>().Count != 0,
            "ENEMY_FLYING_TILE state 0 did not enter its controller state " +
            "without starting the initial counter early.");
        Step();
        FailIf(
            spawner.State != FlyingTileSpawnerState.Waiting ||
            spawner.Counter != 120,
            "Flying Tile controller substate 0 did not load counter1=120.");
        Step(119);
        FailIf(
            spawner.Counter != 1 || spawner.LayoutIndex != 0 ||
            _entities.Entities<FlyingTileCharacter>().Count != 0 ||
            room.GetMetatile(Point(layout[0])) != 0x9c,
            "Room 4:5b spawned its first flying tile before countdown 120.");
        Step();

        FlyingTileCharacter tile =
            _entities.Entities<FlyingTileCharacter>().Single();
        FailIf(
            spawner.Counter != 60 || spawner.LayoutIndex != 1 ||
            tile.Position != Point(layout[0]) ||
            tile.State != FlyingTileState.Rising || tile.ZFixed != 0 ||
            !tile.Visible || !tile.CollisionEnabled ||
            tile.SpeedRaw != 0x46 ||
            room.GetMetatile(Point(layout[0])) != 0xa0 ||
            _entities.RoomEnemyCount != 5 ||
            OracleGraphicsCache.PixelHash(
                tile.CurrentAnimationTexture.GetImage()) == 0,
            "The first Flying Tile did not replace $57:$9c on its spawn " +
            "update, become visible/collidable, and preserve the parent plus " +
            "child enemy counts.");
        Step(6);
        FailIf(
            tile.State != FlyingTileState.Rising || tile.ZFixed != -0x300,
            "Flying Tile left its -$0080 rise before the seventh update.");
        Step();
        FailIf(
            tile.State != FlyingTileState.Waiting ||
            tile.ZFixed != -0x380 || tile.Counter != 15,
            "Flying Tile did not enter its 15-update aim wait immediately " +
            "after Z's high byte passed -3.");

        var tileAdapter = new FlyingTileRoomEntity(tile, countsAsEnemy: true);
        int healthBeforeBeam = tile.Health;
        bool beamAccepted = tileAdapter.ApplyItemCollision(
            RoomEntityItemCollision.SwordBeam,
            tile.CollisionBounds,
            tile.Position,
            damage: 2,
            new List<RoomEntitySpawn>());
        FailIf(
            !beamAccepted || tile.IsDead || tile.Health != healthBeforeBeam ||
            tile.CollisionEnabled,
            "ENEMY_FLYING_TILE collision effect $20 did not queue the sword " +
            "beam's status death without reducing health on contact.");
        Step();
        FailIf(
            _entities.RoomEnemyCount != 4 ||
            _entities.Entities<FlyingTileCharacter>().Count != 0 ||
            _entities.Entities<RockDebrisEffect>().Count != 1,
            "A sword-beam-shattered Flying Tile did not delete on its next " +
            "object update, decrement its count, and create " +
            "INTERAC_ROCKDEBRIS $06 on its deletion update.");

        for (int layoutIndex = 1; layoutIndex < layout.Length; layoutIndex++)
        {
            Step(spawner.Counter);
            tile = _entities.Entities<FlyingTileCharacter>().Single();
            FailIf(
                tile.Position != Point(layout[layoutIndex]) ||
                tile.State != FlyingTileState.Rising ||
                room.GetMetatile(Point(layout[layoutIndex])) != 0xa0,
                $"Flying Tile layout index {layoutIndex} did not spawn in " +
                "the imported source order at its 60-update boundary " +
                $"(count={_entities.Entities<FlyingTileCharacter>().Count}, " +
                $"position={tile.Position}, state={tile.State}, " +
                $"tile=${room.GetMetatile(Point(layout[layoutIndex])):x2}, " +
                $"controller={spawner.State}/{spawner.Counter}/" +
                $"{spawner.LayoutIndex}).");
            var adapter = new FlyingTileRoomEntity(tile, countsAsEnemy: true);
            var collisionSpawns = new List<RoomEntitySpawn>();
            bool broke = layoutIndex switch
            {
                1 => adapter.ApplyItemCollision(
                    RoomEntityItemCollision.Bomb,
                    tile.CollisionBounds,
                    tile.Position,
                    damage: 4,
                    collisionSpawns),
                2 => adapter.ApplyItemCollision(
                    RoomEntityItemCollision.ThrownObject,
                    tile.CollisionBounds,
                    tile.Position,
                    damage: 2,
                    collisionSpawns),
                3 => adapter.ApplyExpertPunch(
                    tile.CollisionBounds,
                    tile.Position,
                    damage: 4,
                    collisionSpawns),
                4 => adapter.ApplySeedHit(
                    tile.CollisionBounds,
                    tile.Position,
                    seedItem: 0x20,
                    collisionSpawns) == SeedHitResult.Activate,
                _ => adapter.ApplySwordHit(
                    tile.CollisionBounds,
                    tile.Position,
                    damage: 1,
                    EnemyKnockbackStrength.Low,
                    collisionSpawns)
            };
            FailIf(!broke || tile.IsDead || tile.CollisionEnabled,
                $"Flying Tile layout index {layoutIndex} rejected its " +
                "source status-setting item collision.");
        }
        Step();
        FailIf(
            _entities.Entities<FlyingTileSpawnerRoomEntity>().Count != 0 ||
            _entities.RoomEnemyCount != 3 ||
            layout.Any(position => room.GetMetatile(Point(position)) != 0xa0),
            "Room 4:5b did not consume the controller after all 23 ordered " +
            "tiles or retain only its three red Zols.");

        Step();
        bool zolsAccepted = true;
        foreach (ZolCharacter zol in _entities.Entities<ZolCharacter>())
        {
            zol.InvincibilityCounter = 0;
            zolsAccepted &= zol.TakeSwordHit(zol.Position, damage: 0x7f);
        }
        _sound.ClearPlayRequestAudit();
        Step();
        FailIf(
            !zolsAccepted || _entities.RoomEnemyCount != 0 ||
            _entities.Entities<ZolCharacter>().Count != 0,
            "Room 4:5b did not clear its three red Zols from the live enemy count.");
        FailIf(
            _sound.PlayRequestsFor(mechanics.SolveSound) != 2 ||
            !room.IsSolid(Point(0x50)) || !room.IsSolid(Point(0xa7)),
            "Room 4:5b's two enemy shutters did not independently begin " +
            "their source eight-update solve delays after the final enemy.");
        Step(mechanics.SolveWait);
        FailIf(
            room.GetMetatile(Point(0x50)) != 0x7b ||
            room.GetMetatile(Point(0xa7)) != 0x7a,
            "Room 4:5b changed either shutter before the post-solve ready update.");
        Step();
        FailIf(
            room.GetMetatile(Point(0x50)) != 0xa0 ||
            room.GetMetatile(Point(0xa7)) != 0xa0 ||
            !room.IsSolid(Point(0x50)) || !room.IsSolid(Point(0xa7)),
            "Room 4:5b did not begin both interleaved shutter openings while " +
            "retaining collision.");
        Step(mechanics.DoorFrameWait);
        FailIf(
            room.IsSolid(Point(0x50)) || room.IsSolid(Point(0xa7)) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0,
            "Room 4:5b did not finish both enemy shutters on the exact " +
            "six-update interleaving boundary.");

        // Exercise the active tile's remaining source paths independently of
        // the controller cadence: state A aims without moving, state B uses
        // precise SPEED_1c0 movement and breaks at allow-holes wall collision.
        var wallTile = new FlyingTileCharacter();
        wallTile.Initialize(
            enemyData.ImportedEnemy(0x52, 0x00),
            room,
            Point(0x23),
            roomTileChanged: static () => { },
            animationTick: static () => 0);
        Vector2 wallTarget = new(0, Point(0x23).Y);
        wallTile.UpdateFrame(wallTarget);
        for (int update = 0; update < 7; update++)
            wallTile.UpdateFrame(wallTarget);
        Vector2 beforeAim = wallTile.Position;
        for (int update = 0; update < 15; update++)
            wallTile.UpdateFrame(wallTarget);
        int expectedAngle = OracleObjectMovement.Shared.RelativeAngle(
            beforeAim, wallTarget);
        FailIf(
            wallTile.State != FlyingTileState.Charging ||
            wallTile.Counter != 0 || wallTile.Position != beforeAim ||
            wallTile.Angle != expectedAngle,
            "Flying Tile state A did not wait exactly 15 updates and aim at " +
            "Link without applying speed on the counter-zero update.");
        for (int update = 0; update < 16 && !wallTile.IsDead; update++)
            wallTile.UpdateFrame(wallTarget);
        FailIf(
            !wallTile.IsDead || !wallTile.TakeDebrisRequest(),
            "Flying Tile state B did not apply SPEED_1c0 and shatter when " +
            "objectCheckTileCollision_allowHoles reached the left wall.");
        wallTile.Free();

        var shieldSave = OracleSaveData.CreateStandardGame();
        var shieldInventory = new InventoryState(_treasures, shieldSave);
        shieldInventory.GiveTreasure(
            _treasures.GetObject("TREASURE_OBJECT_SHIELD_00"));
        shieldInventory.EquipA(InventoryState.ItemShield);
        var shieldPlayer = new Player { Name = "FlyingTileShieldPlayer" };
        AddChild(shieldPlayer);
        shieldPlayer.Initialize(
            new ValidationRingPlayerWorld(),
            shieldInventory,
            new Vector2(80, 80),
            new OracleRandom());
        shieldPlayer.Face(Vector2I.Right);
        shieldPlayer.UpdateShieldForValidation(
            attackHeld: true,
            itemHeld: false);
        var shieldTile = new FlyingTileCharacter();
        shieldTile.Initialize(
            enemyData.ImportedEnemy(0x52, 0x00),
            room,
            shieldPlayer.ShieldCollisionBounds.GetCenter(),
            roomTileChanged: static () => { },
            animationTick: static () => 0);
        shieldTile.UpdateFrame(shieldPlayer.Position);
        new FlyingTileRoomEntity(shieldTile, countsAsEnemy: false)
            .HandleLinkContact(shieldPlayer);
        FailIf(
            shieldTile.IsDead || shieldTile.CollisionEnabled ||
            shieldPlayer.InvincibilityFrames != -0x16 ||
            shieldPlayer.KnockbackFrames != 0x19,
            "Flying Tile's Wooden Shield effect $07 did not apply " +
            "ENEMYDMG_$1c plus LINKDMG_$18's $16/$19 recoil.");
        shieldTile.UpdateFrame(shieldPlayer.Position);
        FailIf(!shieldTile.IsDead,
            "Flying Tile did not dispatch the Wooden Shield's pending " +
            "ENEMYDMG_$1c status on its next object update.");
        shieldTile.Free();
        shieldPlayer.Free();

        GD.Print(
            "Validated full room 4:5b: source enemy/shutter order, 23-tile " +
            "Flying Tile layout, 120/60 spawn cadence, 7-update rise, " +
            "15-update aim wait, deferred effect-$1c/$20 status deaths, " +
            "red Zols, live enemy counts, and left/down shutter clear.");
    }

    private void ValidateRoom464MoonlitGrotto()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static Vector2 Point(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }

        var data = new DungeonMechanicDatabase();
        IReadOnlyList<DungeonMechanicDatabaseRecord> records =
            data.GetRoomRecords(4, 0x64);
        IReadOnlyList<DungeonTilePatternRecord> pattern =
            data.TilePattern(0x21, 0x09);
        FailIf(
            records is not
                [{ Order: 0, Id: 0x21, SubId: 0x09,
                   PackedPosition: 0x68, Parameter: 0xb8 }] ||
            pattern.Select(cell =>
                    (cell.Order, cell.Tile, cell.PackedPosition)).ToArray() is not
                [(0, 0x1d, 0x3b), (1, 0x1d, 0x59), (2, 0x1d, 0x5d)],
            "Room 4:64 lost its source INTERAC_DUNGEON_EVENTS $21:$09 " +
            "placement or ordered $1d:$3b/$59/$5d tile pattern.");

        _saveData.SetRoomFlag(
            4, 0x64, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(4, 0x64);
        OracleRoomData room = _currentRoom;
        DungeonTilePatternFallingKeyRoomEntity roomEvent =
            _entities.Entities<DungeonTilePatternFallingKeyRoomEntity>().Single();
        var pushables = new PushableTileDatabase();
        FailIf(
            room.ActiveCollisions != 2 || room.Width != 240 || room.Height != 176 ||
            _entities.RoomEnemyCount != 0 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            room.GetMetatile(Point(0x4b)) != 0x18 ||
            room.GetMetatile(Point(0x5a)) != 0x1b ||
            room.GetMetatile(Point(0x5c)) != 0x19 ||
            room.GetMetatile(Point(0x3b)) != 0xa0 ||
            room.GetMetatile(Point(0x59)) != 0xa0 ||
            room.GetMetatile(Point(0x5d)) != 0xa0 ||
            room.GetMetatile(Point(0x79)) != 0x1d ||
            room.GetMetatile(Point(0x7d)) != 0x1d ||
            room.GetMetatile(Point(0x9b)) != 0x1d,
            "Room 4:64 did not load its enemy-free large-room geometry, " +
            "three one-way source blocks, three goals, and decorative $1d blocks.");
        FailIf(
            !pushables.TryGet(2, 0x18, out PushableTileRecord up) ||
            up is not
                { InteractionParameter: 0x00, SourceReplacement: 0xa0,
                  DestinationTile: 0x1d, PropertyFlags: 0x01 } ||
            !pushables.TryGet(2, 0x1b, out PushableTileRecord left) ||
            left is not
                { InteractionParameter: 0x30, SourceReplacement: 0xa0,
                  DestinationTile: 0x1d, PropertyFlags: 0x01 } ||
            !pushables.TryGet(2, 0x19, out PushableTileRecord right) ||
            right is not
                { InteractionParameter: 0x10, SourceReplacement: 0xa0,
                  DestinationTile: 0x1d, PropertyFlags: 0x01 },
            "Room 4:64's $18/$1b/$19 blocks lost their imported up/left/right " +
            "push directions or $a0->$1d completion rule.");

        _sound.ClearPlayRequestAudit();
        void PushBlock(int sourcePacked, Vector2I direction, int goalPacked,
            int expectedMoveSounds)
        {
            Vector2 source = Point(sourcePacked);
            Vector2 link = source - (Vector2)direction * 10.0f;
            for (int frame = 0;
                 frame < PushBlockController.PushDelayFrames;
                 frame++)
            {
                _pushBlocks.UpdatePushAttempt(link, direction, direction);
            }
            FailIf(
                !_pushBlocks.Active || room.GetMetatile(source) != 0xa0 ||
                room.GetMetatile(Point(goalPacked)) != 0xa0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) !=
                    expectedMoveSounds,
                $"Room 4:64 block ${sourcePacked:x2} did not begin its " +
                "source 20-update push over floor $a0.");
            for (int frame = 0;
                 frame < PushBlockController.MoveFrames - 1;
                 frame++)
            {
                _pushBlocks.Advance(Update);
            }
            FailIf(
                !_pushBlocks.Active ||
                room.GetMetatile(Point(goalPacked)) != 0xa0,
                $"Room 4:64 block ${sourcePacked:x2} completed before its " +
                "32nd SPEED_80 movement update.");
            _pushBlocks.Advance(Update);
            FailIf(
                _pushBlocks.Active ||
                room.GetMetatile(Point(goalPacked)) != 0x1d,
                $"Room 4:64 block ${sourcePacked:x2} did not write goal " +
                $"${goalPacked:x2}:$1d on movement update 32.");
        }

        Step();
        PushBlock(0x4b, Vector2I.Up, 0x3b, 1);
        Step();
        FailIf(
            roomEvent.Finished ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room 4:64 accepted only its first completed goal.");
        PushBlock(0x5a, Vector2I.Left, 0x59, 2);
        Step();
        FailIf(
            roomEvent.Finished ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room 4:64 accepted only two completed goals.");
        PushBlock(0x5c, Vector2I.Right, 0x5d, 3);
        FailIf(roomEvent.Finished,
            "Room 4:64's event advanced outside the ordered interaction update.");

        Func<Vector2, Vector2> priorWorldToScreen = _entities.WorldToScreen;
        _entities.WorldToScreen = static position =>
            position - new Vector2(80, 0);
        Step();
        GroundTreasurePickup fallingKey =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            _entities.Entities<DungeonTilePatternFallingKeyRoomEntity>().Count != 0 ||
            fallingKey.Position != new Vector2(0xb8, 0x68) ||
            fallingKey.Record.TreasureObject != "TREASURE_OBJECT_SMALL_KEY_01" ||
            fallingKey.Record.SpawnMode != 2 ||
            fallingKey.Record.GrabMode != 2 ||
            fallingKey.Record.SpawnDelayFrames != 40 ||
            fallingKey.Record.BounceCount != 2 ||
            fallingKey.Record.Gravity != 0x10 ||
            fallingKey.Record.BounceSpeed != -0xaa ||
            !fallingKey.Record.InitialZAboveScreen,
            "Room 4:64 did not create its source falling small key at exact " +
            "Y/X $68/$b8 after all three goals matched.");

        for (int frame = 0;
             frame < 300 && fallingKey.State != PickupState.Waiting;
             frame++)
        {
            Step();
        }
        int dungeon = _rooms.CurrentDungeonIndex;
        int keysBeforePickup = _inventory.GetDungeonSmallKeys(dungeon);
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(fallingKey.Position, recordSafe: false);
        Step();
        FailIf(
            fallingKey.State != PickupState.Collected ||
            _inventory.GetDungeonSmallKeys(dungeon) != keysBeforePickup + 1 ||
            !_saveData.HasRoomFlag(4, 0x64, OracleSaveData.RoomFlagItem) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 1,
            "Room 4:64's landed key did not grant one dungeon-$03 small key, " +
            "SND_GETSEED, and ROOMFLAG_ITEM on contact.");
        Step();
        FailIf(
            !fallingKey.Held ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1,
            "Room 4:64's collected key did not enter its held SND_GETITEM pose.");

        LoadValidationRoom(4, 0x64);
        FailIf(
            _entities.Entities<DungeonTilePatternFallingKeyRoomEntity>().Count != 0 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "ROOMFLAG_ITEM re-entry did not suppress room 4:64's completed " +
            "tile-pattern watcher and falling key.");
        _entities.WorldToScreen = priorWorldToScreen;

        GD.Print(
            "Validated full room 4:64: empty source stream apart from $21:$09, " +
            "large-room geometry, directional $18/$1b/$19 pushes with exact " +
            "20/32 timing, ordered $1d goals, falling key Y/X $68/$b8, " +
            "collection, and ROOMFLAG_ITEM re-entry.");
    }

    private void ValidateMoonlitGrottoCrystalCutsceneFreeze()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }

        var data = new DungeonMechanicDatabase();
        _saveData.SetGlobalFlag(data.MoonlitGlobalFlag, value: false);
        _saveData.SetRoomFlag(
            4, 0x5f, (byte)data.MoonlitRoomFlag, value: false);
        _runtimeState.SetWramByte(
            OracleRuntimeState.SwitchStateAddress, 0);
        LoadValidationRoom(4, 0x5f);
        MoonlitGrottoCrystalEventRoomEntity roomEvent =
            _entities.Entities<MoonlitGrottoCrystalEventRoomEntity>().Single();
        MoonlitGrottoCrystalRoomEntity crystal =
            _entities.Entities<MoonlitGrottoCrystalRoomEntity>().Single();
        List<PeahatCharacter> peahats =
            _entities.Entities<PeahatCharacter>();
        List<ZolCharacter> zols = _entities.Entities<ZolCharacter>();
        FailIf(
            crystal.SwitchMask != 0x20 || peahats.Count == 0 || zols.Count == 0,
            "Room 4:5f did not load its mask-$20 crystal with source Peahats " +
            "and Zols for the wDisabledObjects freeze regression.");

        Step(3);
        var spawns = new List<RoomEntitySpawn>();
        crystal.ApplySwordHit(
            crystal.CollisionBounds,
            crystal.Position,
            damage: 2,
            EnemyKnockbackStrength.None,
            spawns);
        Step();
        FailIf(
            !crystal.Broken ||
            roomEvent.Phase != MoonlitCrystalEventPhase.FirstWait ||
            roomEvent.Counter != data.MoonlitFirstWait ||
            !roomEvent.FreezesRoomEntities,
            "Room 4:5f's crystal hit did not enter the source disableinput " +
            "freeze before moonlitGrottoScript_brokeCrystal wait 30.");

        var frozenPeahats = peahats.Select(enemy =>
            (enemy.State, enemy.Counter, enemy.Position)).ToArray();
        var frozenZols = zols.Select(enemy =>
            (enemy.State, enemy.Counter1, enemy.Counter2, enemy.Position)).ToArray();
        Step(data.MoonlitFirstWait - 1);
        FailIf(
            !frozenPeahats.SequenceEqual(peahats.Select(enemy =>
                (enemy.State, enemy.Counter, enemy.Position))) ||
            !frozenZols.SequenceEqual(zols.Select(enemy =>
                (enemy.State, enemy.Counter1, enemy.Counter2, enemy.Position))) ||
            roomEvent.Counter != 1,
            "Room 4:5f advanced enemies during the frozen pre-shake wait.");

        Step();
        FailIf(
            roomEvent.Phase != MoonlitCrystalEventPhase.Rumbling ||
            roomEvent.Counter != data.MoonlitRumbleWait ||
            _entities.ScreenShakeCounter != data.MoonlitRumbleWait - 1,
            "Room 4:5f did not begin its 180-update shake on the wait-30 boundary.");
        Step(60);
        FailIf(
            !frozenPeahats.SequenceEqual(peahats.Select(enemy =>
                (enemy.State, enemy.Counter, enemy.Position))) ||
            !frozenZols.SequenceEqual(zols.Select(enemy =>
                (enemy.State, enemy.Counter1, enemy.Counter2, enemy.Position))) ||
            roomEvent.Phase != MoonlitCrystalEventPhase.Rumbling ||
            roomEvent.Counter != data.MoonlitRumbleWait - 60 ||
            _entities.ScreenShakeCounter != data.MoonlitRumbleWait - 61,
            "Room 4:5f advanced Peahats or Zols while the crystal screen " +
            "shake continued.");

        Step(data.MoonlitRumbleWait - 60);
        FailIf(
            roomEvent.Phase != MoonlitCrystalEventPhase.FirstDialogue ||
            !_dialogue.IsOpen || !roomEvent.FreezesRoomEntities ||
            !frozenPeahats.SequenceEqual(peahats.Select(enemy =>
                (enemy.State, enemy.Counter, enemy.Position))) ||
            !frozenZols.SequenceEqual(zols.Select(enemy =>
                (enemy.State, enemy.Counter1, enemy.Counter2, enemy.Position))),
            "Room 4:5f did not retain the object freeze through TX_1200.");

        _dialogue.Close();
        Step();
        FailIf(
            _entities.Entities<MoonlitGrottoCrystalEventRoomEntity>().Count != 0 ||
            !_saveData.HasRoomFlag(4, 0x5f, OracleSaveData.RoomFlag40),
            "Closing TX_1200 did not persist the crystal and release its event.");
        Step();
        FailIf(
            frozenPeahats.SequenceEqual(peahats.Select(enemy =>
                (enemy.State, enemy.Counter, enemy.Position))),
            "Room 4:5f's Peahats remained frozen after control was restored.");

        GD.Print(
            "Validated Moonlit Grotto crystal cutscene freeze: source " +
            "disableinput, 30-update lead-in, 180-update shake, TX_1200, " +
            "frozen Peahats/Zols, and next-update control restoration.");
    }

    private void ValidateRoom461MoonlitGrotto()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static Vector2 Point(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        static void Step(RoomEntityManager entities, Player player, int count = 1)
        {
            for (int index = 0; index < count; index++)
                entities.Update(Update, player);
        }

        var data = new DungeonMechanicDatabase();
        IReadOnlyList<DungeonMechanicDatabaseRecord> records =
            data.GetRoomRecords(4, 0x61);
        FailIf(
            records.Select(record =>
                    (record.Order, record.Id, record.SubId,
                     record.PackedPosition, record.Parameter))
                .ToArray() is not
                [(0, 0x21, 0x0d, 0x00, 0x00),
                 (1, 0x21, 0x0e, 0x58, 0xb8),
                 (2, 0x24, 0x40, 0x57, 0x00)],
            "Room 4:61 lost its source-ordered `$21:$0d, `$21:$0e, " +
            "PART_GROTTO_CRYSTAL `$24:$40 stream.");

        _saveData.SetGlobalFlag(data.MoonlitGlobalFlag, value: false);
        _saveData.SetRoomFlag(
            4, 0x61, (byte)data.MoonlitRoomFlag, value: false);
        _saveData.SetRoomFlag(
            4, 0x61, OracleSaveData.RoomFlagItem, value: false);
        _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
        LoadValidationRoom(4, 0x61);
        Func<Vector2, Vector2> priorWorldToScreen = _entities.WorldToScreen;
        _entities.WorldToScreen = static position =>
            position - new Vector2(80, 0);
        MoonlitGrottoCrystalEventRoomEntity roomEvent =
            _entities.Entities<MoonlitGrottoCrystalEventRoomEntity>().Single();
        MoonlitGrottoFallingKeyRoomEntity key =
            _entities.Entities<MoonlitGrottoFallingKeyRoomEntity>().Single();
        MoonlitGrottoCrystalRoomEntity crystal =
            _entities.Entities<MoonlitGrottoCrystalRoomEntity>().Single();
        ItemDropProducer producer =
            _entities.Entities<ItemDropProducer>().Single();
        FailIf(
            key.GoalPosition != 0x4a || key.GoalTile != 0x2a ||
            crystal.Position != Point(0x57) || crystal.SwitchMask != 0x40 ||
            crystal.Broken || crystal.CrystalAnimation != 0 ||
            _currentRoom.GetTerrainInfo(crystal.Position).Collision != 0x0a ||
            OracleGraphicsCache.PixelHash(crystal.CrystalTexture.GetImage()) == 0 ||
            producer.Position != Point(0x9c) ||
            _currentRoom.GetMetatile(Point(0x4a)) != 0x1f,
            "Room 4:61 did not create its intact mask-$40 crystal, collision " +
            "`$0a, block-goal `$4a:$2a, or fixed drop producer at `$9c.");

        // Completing the room's ordinary push writes tile $2a at $4a. The
        // event then creates TREASURE_SMALL_KEY:$01 at exact Y/X $58/$b8.
        _currentRoom.SetPositionTileAndCollision(
            Point(0x4a), 0x2a, null, (long)_animationTicks);
        Step(_entities, _player);
        GroundTreasurePickup fallingKey =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            _entities.Entities<MoonlitGrottoFallingKeyRoomEntity>().Count != 0 ||
            fallingKey.Position != new Vector2(0xb8, 0x58) ||
            fallingKey.Record.TreasureObject != "TREASURE_OBJECT_SMALL_KEY_01" ||
            fallingKey.Record.SpawnMode != 2 ||
            fallingKey.Record.SpawnDelayFrames != 40 ||
            fallingKey.Record.BounceCount != 2 ||
            fallingKey.Record.Gravity != 0x10 ||
            fallingKey.Record.BounceSpeed != -0xaa ||
            !fallingKey.Record.InitialZAboveScreen,
            "INTERAC_DUNGEON_EVENTS `$21:$0e did not spawn the original " +
            "falling small key after layout `$4a became `$2a.");

        // Room 4:61 is a large room and the key's world X is $b8. The source
        // objectCheckWithinScreenBoundary subtracts the camera origin before
        // applying its -7..167/-7..135 bounds. Keep that distinction covered:
        // using the world X directly hides the entire falling animation.
        _sound.ClearPlayRequestAudit();
        Step(_entities, _player, 2);
        FailIf(
            fallingKey.State != PickupState.Spawning ||
            fallingKey.SpawnSubstate != 1 ||
            fallingKey.SpawnCounter != 40 || fallingKey.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
            "Room 4:61's key did not begin its source 40-update hidden " +
            "SND_SOLVEPUZZLE delay.");
        Step(_entities, _player, 39);
        FailIf(fallingKey.SpawnCounter != 1 || fallingKey.Visible,
            "Room 4:61's key appeared before delay update 40.");
        Step(_entities, _player);
        Vector2 groundScreenPosition =
            _entities.WorldToScreen(fallingKey.Position);
        int expectedInitialZ = System.Math.Max(
            -128, -Mathf.FloorToInt(groundScreenPosition.Y) - 8);
        FailIf(
            fallingKey.SpawnSubstate != 2 ||
            fallingKey.ZFixed != expectedInitialZ << 8 ||
            fallingKey.SpeedZ != 0 || fallingKey.Visible ||
            groundScreenPosition.X is < -7 or >= 168 ||
            fallingKey.Position.X < 168,
            "Room 4:61's key did not initialize immediately above the " +
            "camera-relative screen while remaining hidden on the `$ff " +
            $"boundary update (substate={fallingKey.SpawnSubstate}, " +
            $"z={fallingKey.ZFixed >> 8}, expected={expectedInitialZ}, " +
            $"speed={fallingKey.SpeedZ}, visible={fallingKey.Visible}, " +
            $"screen={groundScreenPosition}, world={fallingKey.Position}).");

        bool becameVisibleWhileFalling = false;
        for (int update = 0;
             update < 240 && fallingKey.State != PickupState.Waiting;
             update++)
        {
            Vector2 screenBeforeMotion = groundScreenPosition +
                new Vector2(0, fallingKey.ZFixed >> 8);
            bool expectedVisible =
                OracleObjectMath.IsInsideOriginalScreenBoundary(
                    screenBeforeMotion);
            Step(_entities, _player);
            if (fallingKey.State == PickupState.Spawning)
            {
                FailIf(
                    fallingKey.Visible != expectedVisible,
                    "Room 4:61's falling key did not apply the source " +
                    "camera-relative pre-motion visibility boundary.");
                becameVisibleWhileFalling |= fallingKey.Visible;
            }
        }
        FailIf(
            fallingKey.State != PickupState.Waiting ||
            !becameVisibleWhileFalling || !fallingKey.Visible ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDropEssence) != 2,
            "Room 4:61's key did not visibly fall and complete both source " +
            "bounces with SND_DROPESSENCE.");

        var collisionSpawns = new List<RoomEntitySpawn>();
        _sound.ClearPlayRequestAudit();
        Rect2 crystalHitbox = new(
            crystal.Position - new Vector2(8, 8), new Vector2(16, 16));
        crystal.ApplyItemCollision(
            RoomEntityItemCollision.ThrownObject,
            crystalHitbox,
            crystal.Position,
            damage: 4,
            collisionSpawns);
        crystal.ApplyItemCollision(
            RoomEntityItemCollision.Bomb,
            crystalHitbox,
            crystal.Position,
            damage: 4,
            collisionSpawns);
        FailIf(
            crystal.Broken || crystal.CrystalAnimation != 0 ||
            crystal.BreakEffectActive ||
            (_runtimeState.ReadWramByte(
                OracleRuntimeState.SwitchStateAddress) & 0x40) != 0,
            "PART_GROTTO_CRYSTAL `$24 accepted thrown-object `$16 or bomb " +
            "`$18 despite both bits being clear in active-collision row `$24.");
        crystal.ApplySwordHit(
            crystalHitbox,
            crystal.Position,
            damage: 2,
            EnemyKnockbackStrength.Low,
            collisionSpawns);
        FailIf(
            !crystal.Broken || crystal.CrystalAnimation != 1 ||
            !crystal.BreakEffectActive ||
            (_runtimeState.ReadWramByte(
                OracleRuntimeState.SwitchStateAddress) & 0x40) == 0,
            "A sword collision did not break PART_GROTTO_CRYSTAL `$24, " +
            "select animation 1, create its break effect, and XOR mask `$40.");
        Step(_entities, _player);
        FailIf(
            roomEvent.Phase != MoonlitCrystalEventPhase.FirstWait ||
            roomEvent.Counter != data.MoonlitFirstWait ||
            !_entities.PlayerMovementDisabled ||
            !_entities.PlayerItemUsageDisabled ||
            !_entities.PlayerMenusDisabled,
            "The `$21:$0d watcher did not observe the crystal bit on the " +
            "following interaction update and disable player control.");
        Step(_entities, _player, data.MoonlitBreakSoundDelay);
        FailIf(
            _sound.PlayRequestsFor(data.MoonlitBreakSound) != 1,
            "The crystal's INTERAC_SARCOPHAGUS `$82:$80 break effect did " +
            "not emit SND_KILLENEMY after its source two-update delay.");

        // Exercise both scripts independently of the live dialogue box so
        // every exact counter boundary and flag side effect is observable.
        var eventSave = OracleSaveData.CreateStandardGame();
        var eventRuntime = new OracleRuntimeState();
        eventRuntime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0xb0);
        eventRuntime.SetWramByte(OracleRuntimeState.SpinnerStateAddress, 0xa5);
        var sounds = new List<int>();
        var shakes = new List<int>();
        var texts = new List<int>();
        bool dialogueOpen = false;
        var scriptedEvent = new MoonlitGrottoCrystalEventRoomEntity(
            records[0],
            data,
            eventSave,
            eventRuntime,
            sounds.Add,
            shakes.Add,
            (textId, _, _) =>
            {
                texts.Add(textId);
                dialogueOpen = true;
            },
            () => dialogueOpen);
        var scriptedSpawns = new List<RoomEntitySpawn>();
        void ScriptStep(int count = 1)
        {
            for (int index = 0; index < count; index++)
            {
                scriptedEvent.UpdateFrame(
                    new RoomEntityFrame(_player, index, false),
                    scriptedSpawns);
            }
        }

        eventRuntime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0xf0);
        ScriptStep();
        ScriptStep(29);
        FailIf(
            scriptedEvent.Phase != MoonlitCrystalEventPhase.FirstWait ||
            scriptedEvent.Counter != 1 || !scriptedEvent.RestrictsPlayer,
            "moonlitGrottoScript_brokeCrystal ended wait 30 early.");
        ScriptStep();
        FailIf(
            scriptedEvent.Phase != MoonlitCrystalEventPhase.Rumbling ||
            scriptedEvent.Counter != 180 ||
            !sounds.SequenceEqual(
                [OracleSoundEngine.SndCtrlStopSfx, data.MoonlitRumbleSound]) ||
            !shakes.SequenceEqual([180]),
            "The first crystal script did not stop SFX, shake 180, and play " +
            "SND_RUMBLE2 on the wait-30 boundary.");
        ScriptStep(179);
        FailIf(
            texts.Count != 0 || scriptedEvent.Counter != 1,
            "The first crystal script showed TX_1200 before wait 180 ended.");
        ScriptStep();
        FailIf(
            texts is not [0x1200] ||
            scriptedEvent.Phase != MoonlitCrystalEventPhase.FirstDialogue,
            "The first crystal script did not show TX_1200 after 180 updates.");
        ScriptStep(3);
        FailIf(
            eventSave.HasRoomFlag(4, 0x61, OracleSaveData.RoomFlag40),
            "TX_1200 did not suspend the crystal script until dialogue closed.");
        dialogueOpen = false;
        ScriptStep();
        FailIf(
            !eventSave.HasRoomFlag(4, 0x61, OracleSaveData.RoomFlag40) ||
            scriptedEvent.Phase != MoonlitCrystalEventPhase.AllWait ||
            scriptedEvent.Counter != 30 ||
            eventRuntime.ReadWramByte(
                OracleRuntimeState.SpinnerStateAddress) != 0,
            "Closing TX_1200 did not set room flag `$40, select the all-four " +
            "script, and clear wSpinnerState.");
        ScriptStep(30);
        FailIf(
            scriptedEvent.Phase != MoonlitCrystalEventPhase.ExplosionWait ||
            scriptedEvent.Counter != 90 ||
            !shakes.SequenceEqual([180, 100]) ||
            sounds[^1] != data.MoonlitBigExplosionSound,
            "moonlitGrottoScript_brokeAllCrystals did not shake 100 and play " +
            "SND_BIG_EXPLOSION after wait 30.");
        ScriptStep(90);
        FailIf(
            scriptedEvent.Phase != MoonlitCrystalEventPhase.SolveWait ||
            scriptedEvent.Counter != 30 || sounds[^1] != data.MoonlitSolveSound,
            "The all-crystals script did not wait 90 before SND_SOLVEPUZZLE.");
        ScriptStep(30);
        FailIf(
            texts is not [0x1200, 0x1201] ||
            scriptedEvent.Phase != MoonlitCrystalEventPhase.AllDialogue,
            "The all-crystals script did not wait 30 before TX_1201.");
        dialogueOpen = false;
        ScriptStep();
        FailIf(
            !scriptedEvent.Finished || scriptedEvent.RestrictsPlayer ||
            !eventSave.HasGlobalFlag(data.MoonlitGlobalFlag),
            "Closing TX_1201 did not set GLOBALFLAG_D3_CRYSTALS `$0f and " +
            "release every player restriction.");
        scriptedEvent.Free();

        _saveData.SetRoomFlag(
            4, 0x61, OracleSaveData.RoomFlag40);
        _saveData.SetRoomFlag(
            4, 0x61, OracleSaveData.RoomFlagItem);
        LoadValidationRoom(4, 0x61);
        FailIf(
            _entities.Entities<MoonlitGrottoCrystalEventRoomEntity>().Count != 0 ||
            _entities.Entities<MoonlitGrottoFallingKeyRoomEntity>().Count != 0 ||
            _entities.Entities<MoonlitGrottoCrystalRoomEntity>() is not
                [{ Broken: true, CrystalAnimation: 1 }],
            "Room flags `$40/$20 did not suppress the completed event/key " +
            "while retaining the crystal's persistent broken frame.");

        _entities.WorldToScreen = priorWorldToScreen;

        GD.Print(
            "Validated full room 4:61: source order, fixed drop, push-layout " +
            "falling key, item-passable bomb fence coverage, mask-$40 crystal " +
            "collision/break persistence, `$82 break sound, exact 30/180 and " +
            "30/90/30 scripts, TX_1200/TX_1201, flags, spinner reset, and " +
            "player restrictions.");
    }
}
