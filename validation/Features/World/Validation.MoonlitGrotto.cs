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
            RoomEntityItemCollision.Bomb,
            crystalHitbox,
            crystal.Position,
            damage: 4,
            collisionSpawns);
        FailIf(
            !crystal.Broken || crystal.CrystalAnimation != 1 ||
            !crystal.BreakEffectActive ||
            (_runtimeState.ReadWramByte(
                OracleRuntimeState.SwitchStateAddress) & 0x40) == 0,
            "A bomb explosion did not break PART_GROTTO_CRYSTAL `$24, " +
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
