using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateWingDungeon()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        int[] rooms = Enumerable.Range(0x27, 0x22).ToArray();
        byte[] originalFlags = rooms
            .Select(room => _saveData.GetRoomFlags(4, room))
            .ToArray();
        var data = new WingDungeonDatabase();

        void SetAllRoomFlags(bool value)
        {
            foreach (int room in rooms)
            for (int bit = 0; bit < 8; bit++)
            {
                _saveData.SetRoomFlag(
                    4, room, (byte)(1 << bit), value);
            }
        }

        void RestoreFlags()
        {
            for (int index = 0; index < rooms.Length; index++)
            for (int bit = 0; bit < 8; bit++)
            {
                byte mask = (byte)(1 << bit);
                _saveData.SetRoomFlag(
                    4,
                    rooms[index],
                    mask,
                    (originalFlags[index] & mask) != 0);
            }
        }

        void PrepareRoom(int room)
        {
            _dialogue.Close();
            _player.EndCutsceneControl();
            _player.EndGetItemTwoHandPose();
            _player.Visible = true;
            _entities.ClearRecentEnemyDefeats();
            LoadValidationRoom(4, room);
        }

        void Step(int count = 1)
        {
            for (int frame = 0; frame < count; frame++)
                _entities.Update(update, _player);
        }

        void CompleteTransition()
        {
            for (int frame = 0;
                 frame < 180 && _transitions.IsTransitioning;
                 frame++)
            {
                _transitions.Update(update);
            }
            FailIf(
                _transitions.IsTransitioning,
                "A Wing Dungeon side-view transition did not finish within 180 updates.");
        }

        SetAllRoomFlags(value: false);
        WingDungeonMinecartState.Reset(
            _entities.RuntimeState, data.Minecarts);

        // func_5933 does not snap shallow turns directly to the input angle.
        // It advances one angular unit only when var12 reaches the terrain's
        // interval. Exercise both the forward and reverse close-angle rows.
        var sidePhysicsWorld = new ValidationRingPlayerWorld
        {
            SideScrolling = true,
            AdjacentWallsBitset =
                new SideScrollPlayerDatabase().Parameters.GroundWallMask
        };
        var sidePhysicsPlayer = new Player
        {
            Name = "SideScrollPhysicsValidationPlayer"
        };
        AddChild(sidePhysicsPlayer);
        sidePhysicsPlayer.Initialize(
            sidePhysicsWorld,
            _inventory,
            new Vector2(80, 80),
            new OracleRandom());

        // linkAdjustAngleInSidescrollingArea horizontalizes only the object's
        // movement angle. updateLinkDirectionFromAngle still consumes the raw
        // wLinkAngle, allowing Up/Down facing without vertical dry movement.
        Vector2 dryFacingStart = sidePhysicsPlayer.PrecisePosition;
        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(Vector2.Up);
        FailIf(
            sidePhysicsPlayer.FacingVector != Vector2I.Up ||
            sidePhysicsPlayer.PrecisePosition != dryFacingStart,
            "Dry side-view Up input did not face Link upward while retaining " +
            "horizontal-only movement.");
        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(Vector2.Down);
        FailIf(
            sidePhysicsPlayer.FacingVector != Vector2I.Down ||
            sidePhysicsPlayer.PrecisePosition != dryFacingStart,
            "Dry side-view Down input did not face Link downward while " +
            "retaining horizontal-only movement.");

        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(Vector2.Right);
        sidePhysicsWorld.SideScrollTerrain = new SideScrollTerrainState(
            0x00, 0x20,
            SideScrollTileType.None, SideScrollTileType.Ice);
        for (int updateIndex = 0; updateIndex < 6; updateIndex++)
        {
            sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(
                new Vector2(1, -1));
        }
        FailIf(
            sidePhysicsPlayer.SideScrollAngle != 0x07 ||
            sidePhysicsPlayer.SideScrollSpeedRaw != 0x28,
            "func_5933 did not turn angle $08 one step toward shallow " +
            "input $04 after ice interval $06.");

        sidePhysicsPlayer.WarpTo(new Vector2(80, 80), recordSafe: false);
        sidePhysicsWorld.SideScrollTerrain = default;
        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(Vector2.Right);
        sidePhysicsWorld.SideScrollTerrain = new SideScrollTerrainState(
            0x00, 0x20,
            SideScrollTileType.None, SideScrollTileType.Ice);
        for (int updateIndex = 0; updateIndex < 6; updateIndex++)
        {
            sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(
                new Vector2(-1, 1));
        }
        FailIf(
            sidePhysicsPlayer.SideScrollAngle != 0x09 ||
            sidePhysicsPlayer.SideScrollSpeedRaw != 0x23,
            "func_5933 did not apply the reverse close-angle $01/$fb row " +
            "after ice interval $06.");

        // wForceIcePhysics stores the literal $06 as a latch, not a
        // countdown. It survives an airborne interval and resumes slippery
        // convergence on the next solid ground.
        sidePhysicsWorld.SideScrollTerrain = default;
        sidePhysicsWorld.AdjacentWallsBitset = 0;
        for (int updateIndex = 0; updateIndex < 8; updateIndex++)
        {
            sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(
                Vector2.Zero);
        }
        sidePhysicsWorld.AdjacentWallsBitset =
            sidePhysicsWorld.SideScrollParameters.GroundWallMask;
        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(Vector2.Zero);
        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(Vector2.Zero);
        FailIf(
            sidePhysicsPlayer.SideScrollAngle >= 0x80 ||
            sidePhysicsPlayer.SideScrollSpeedRaw == 0,
            "wForceIcePhysics lost its source $06 latch while Link was " +
            "airborne.");

        // Moving side platforms call updateLinkPositionGivenVelocity for
        // right/down/left carries, so a blocked Link must not be translated
        // through the wall before the platform's own movement.
        sidePhysicsPlayer.WarpTo(new Vector2(80, 80), recordSafe: false);
        sidePhysicsWorld.BlockMovement = true;
        Vector2 blockedCarryStart = sidePhysicsPlayer.PrecisePosition;
        sidePhysicsPlayer.ApplySideScrollMovingPlatformVelocity(0x14, 0x08);
        FailIf(
            sidePhysicsPlayer.PrecisePosition != blockedCarryStart,
            "interactionCodea1 carried Link through a wall instead of using " +
            "updateLinkPositionGivenVelocity.");
        sidePhysicsWorld.BlockMovement = false;

        // The side-view Y coordinate is a 16-bit 8.8 value. A jump above
        // y=$00 wraps to $fe, then the source's bottom-boundary landing clamp
        // writes high byte $f9.
        sidePhysicsPlayer.WarpTo(new Vector2(80, 0.2f), recordSafe: false);
        sidePhysicsWorld.SideScrollTerrain = default;
        sidePhysicsWorld.AdjacentWallsBitset = 0;
        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(
            Vector2.Zero,
            startJump: true);
        FailIf(
            sidePhysicsPlayer.SideScrollAirborne ||
            (sidePhysicsPlayer.SideScrollYFixed >> 8) != 0xf9,
            "linkUpdateInAir_sidescroll did not retain 16-bit Y wrap and " +
            "the $a9 bottom-boundary landing clamp.");

        // TILETYPE_SS_LAVA is checked only from @positiveSpeedZ. Link may
        // rise through it, but must drown as soon as the descending branch
        // executes.
        sidePhysicsPlayer.WarpTo(new Vector2(80, 80), recordSafe: false);
        sidePhysicsWorld.SideScrollTerrain = new SideScrollTerrainState(
            0x00, 0x00,
            SideScrollTileType.Lava, SideScrollTileType.None);
        sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(
            Vector2.Zero,
            startJump: true);
        FailIf(
            !sidePhysicsPlayer.SideScrollAirborne ||
            sidePhysicsPlayer.RejectsOrdinaryScreenTransition,
            "Ascending through side-view lava drowned Link before " +
            "@positiveSpeedZ.");
        for (int updateIndex = 0;
             updateIndex < 32 &&
             !sidePhysicsPlayer.RejectsOrdinaryScreenTransition;
             updateIndex++)
        {
            sidePhysicsPlayer.AdvanceSideScrollUpdateForValidation(
                Vector2.Zero);
        }
        FailIf(
            !sidePhysicsPlayer.RejectsOrdinaryScreenTransition,
            "Descending through side-view lava did not enter Link's drown state.");
        RemoveChild(sidePhysicsPlayer);
        sidePhysicsPlayer.Free();

        List<ObjectRecord> native = rooms
            .SelectMany(room => data.GetRoomRecords(4, room))
            .ToList();
        var kindCounts = native
            .GroupBy(record => record.Kind)
            .ToDictionary(group => group.Key, group => group.Count());
        FailIf(
            native.Count != 40 ||
            kindCounts.GetValueOrDefault(ObjectKind.ToggleFloor) != 5 ||
            kindCounts.GetValueOrDefault(ObjectKind.SidePlatform) != 4 ||
            kindCounts.GetValueOrDefault(ObjectKind.CircularSidePlatform) != 3 ||
            kindCounts.GetValueOrDefault(ObjectKind.EnemyChest) != 3 ||
            kindCounts.GetValueOrDefault(ObjectKind.ColoredCube) != 2 ||
            kindCounts.GetValueOrDefault(ObjectKind.HeadThwomp) != 1 ||
            kindCounts.GetValueOrDefault(ObjectKind.Swoop) != 1 ||
            kindCounts.GetValueOrDefault(ObjectKind.Essence) != 1 ||
            data.Pattern(ObjectKind.FloorPatternKey, 0)
                .SequenceEqual(new byte[] { 0x67, 0x77 }) == false ||
            data.Pattern(ObjectKind.ColoredBlockKey, 2)
                .SequenceEqual(new byte[] { 0x4a, 0x59, 0x5b, 0x6a }) == false ||
            data.SwitchTiles(0x13) != (0x5c, 0x5a) ||
            data.Minecarts.Count != 3 ||
            !data.EssenceMessage.Contains(
                "Ancient Wood", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(data.SwoopMessage),
            "Wing Dungeon's imported native object, pattern, minecart, or " +
            "text contract is incomplete.");

        var chests = new ChestDatabase();
        (int Room, int Position, string Treasure)[] expectedChests =
        [
            (0x30, 0x57, "TREASURE_OBJECT_SMALL_KEY_03"),
            (0x3e, 0x58, "TREASURE_OBJECT_BOSS_KEY_03"),
            (0x40, 0x87, "TREASURE_OBJECT_MAP_02"),
            (0x41, 0x59, "TREASURE_OBJECT_GASHA_SEED_01"),
            (0x45, 0x1d, "TREASURE_OBJECT_COMPASS_02"),
            (0x48, 0x57, "TREASURE_OBJECT_SMALL_KEY_03")
        ];
        foreach ((int room, int position, string treasure) in expectedChests)
        {
            FailIf(
                !chests.TryGet(4, room, position, out ChestRecord chest) ||
                chest.TreasureObject != treasure,
                $"Wing Dungeon chest 4:{room:x2}/${position:x2} is not " +
                $"{treasure}.");
        }

        // Every D2 room must assemble its complete source-ordered native,
        // shared-mechanic, ordinary-enemy, and static-object stream.
        foreach (int room in rooms)
        {
            PrepareRoom(room);
            FailIf(
                _rooms.CurrentDungeonIndex != 2,
                $"Wing Dungeon room 4:{room:x2} did not select dungeon index 2.");
            switch (room)
            {
                case 0x29:
                    FailIf(
                        _entities.Entities<WingDungeonSideScrollPlatform>().Count != 2,
                        "Room 4:29 did not create both imported side platforms.");
                    break;
                case 0x2a:
                    FailIf(
                        _entities.Entities<WingDungeonSideScrollPlatform>().Count != 2 ||
                        _entities.Entities<ThwompCharacter>().Count != 1,
                        "Room 4:2a did not create its two side platforms and Thwomp.");
                    break;
                case 0x2b:
                    FailIf(
                        _entities.Entities<WingDungeonCircularSidePlatform>().Count != 3 ||
                        _entities.Entities<HeadThwompBoss>().Count != 1,
                        "Room 4:2b did not create three circular platforms and Head Thwomp.");
                    break;
                case 0x2c:
                    FailIf(
                        _entities.Entities<WhispCharacter>().Count != 2 ||
                        _entities.Entities<ArrowMoblinCharacter>().Count != 2,
                        "Room 4:2c did not create both Whisps and Arrow Shrouded Stalfos.");
                    break;
                case 0x2e:
                    FailIf(
                        _entities.Entities<PeahatCharacter>().Count != 2 ||
                        _entities.Entities<WingDungeonPatternKey>().Count != 1 ||
                        _entities.Entities<WingDungeonToggleFloor>().Count != 1,
                        "Room 4:2e did not create its Peahats and floor-pattern key puzzle.");
                    break;
                case 0x30:
                    FailIf(
                        _entities.Entities<SwordEnemyCharacter>().Count != 2 ||
                        _entities.Entities<WingDungeonEnemyChest>().Count != 1,
                        "Room 4:30 did not create its Sword Stalfos and kill chest.");
                    break;
                case 0x33:
                case 0x35:
                case 0x40:
                    FailIf(
                        _entities.Entities<WingDungeonMinecart>().Count != 1,
                        $"Room 4:{room:x2} did not restore its static minecart.");
                    break;
                case 0x34:
                    FailIf(
                        _entities.Entities<SwoopBoss>().Count != 1 ||
                        _entities.Entities<SpiritsGraveRewardController>().Count != 1,
                        "Room 4:34 did not create Swoop and its miniboss reward.");
                    break;
                case 0x36:
                    FailIf(
                        _entities.Entities<SparkCharacter>().Count != 2,
                        "Room 4:36 did not create both Sparks.");
                    break;
                case 0x38:
                    FailIf(
                        _entities.Entities<SpiritsGraveEssence>() is not
                            [{ EssenceIndex: 1 }],
                        "Room 4:38 did not create the second Essence.");
                    break;
                case 0x3e:
                    FailIf(
                        _entities.Entities<ColorChangingGelCharacter>().Count != 7 ||
                        _entities.Entities<WingDungeonFloorColorChanger>().Count != 1 ||
                        _entities.Entities<WingDungeonEnemyChest>().Count != 1,
                        "Room 4:3e did not create seven color Gels, the exact " +
                        "floor changer, and its Boss Key chest.");
                    break;
                case 0x43:
                    FailIf(
                        _entities.Entities<SpiritsGraveColoredCube>().Count != 1 ||
                        _entities.Entities<SpiritsGraveCubeFlame>().Count != 1 ||
                        _entities.Entities<SpiritsGraveCubeSensor>().Count != 1,
                        "Room 4:43 did not create its color-cube/flame/sensor puzzle.");
                    break;
            }
        }

        // Direct development launches of D2's aliased side-view layouts must
        // enter source group $06, exactly as their retail staircase warps do.
        // Exercise every D2 edge-warp quadrant rather than only reading the
        // generated records.
        (int Room, Vector2 Edge, Vector2I Direction, int DestinationRoom)[]
            sideExits =
        [
            (0x29, new Vector2(0x18, 0x06), Vector2I.Up, 0x47),
            (0x2a, new Vector2(0xd8, 0x06), Vector2I.Up, 0x48),
            (0x2b, new Vector2(0x48, 0xa9), Vector2I.Down, 0x37),
            (0x2b, new Vector2(0xa8, 0xa9), Vector2I.Down, 0x37)
        ];
        foreach ((
            int room,
            Vector2 edge,
            Vector2I direction,
            int destinationRoom) in sideExits)
        {
            PrepareRoom(room);
            FailIf(
                _rooms.ActiveGroup != 6 ||
                _currentRoom.Group != 6 ||
                (_currentRoom.TilesetFlags & 0x20) == 0,
                $"Direct Wing Dungeon side room 4:{room:x2} did not select " +
                "active source group $06.");
            _player.WarpTo(edge, recordSafe: false);
            _player.Face(direction);
            if (direction == Vector2I.Up)
            {
                // D2's upper exits are open shafts, not ladder tiles. Reach
                // y <= $05 through linkUpdateInAir_sidescroll exactly as a
                // player does with Roc's Feather.
                _player.AdvanceSideScrollUpdateForValidation(
                    Vector2.Zero,
                    startJump: true);
            }
            else
            {
                if (edge.X == 0xa8)
                {
                    // The bottom-right route is reached by a circular
                    // platform after Link's own air handler has clamped him
                    // to y=$a9. updateInteractions carries him across the
                    // boundary, then screenTransitionState2 sees y=$aa.
                    _player.ApplyMovingPlatformDisplacement(Vector2.Down);
                }
                else
                {
                    _player.AdvanceSideScrollUpdateForValidation(
                        Vector2.Down);
                }
            }
            UpdatePostObjectPlayerState();
            FailIf(
                !_transitions.IsTransitioning,
                $"Side room 6:{room:x2} did not accept its imported edge warp " +
                $"when Link moved {direction} from {edge} through the live " +
                $"application update (position={_player.Position}, " +
                $"precise={_player.PrecisePosition}, " +
                $"airborne={_player.SideScrollAirborne}, " +
                $"walls=${_collision.AdjacentWallsBitset(_player.Position):x2}).");
            CompleteTransition();
            FailIf(
                _rooms.ActiveGroup != 4 ||
                _currentRoom.Id != destinationRoom,
                $"Side room 6:{room:x2} did not return to Wing Dungeon room " +
                $"4:{destinationRoom:x2}.");
        }

        // checkWarpsSidescrolling only narrows warp lookup to edge warps; the
        // generic screenTransitionState2 path still owns ordinary open room
        // edges. Walk the live $29 -> $2a corridor in both directions so a
        // side-view-only guard cannot silently turn it into an outer wall.
        PrepareRoom(0x29);
        Vector2 horizontalExit = new(_currentRoom.Width - 6, 0x58);
        _player.WarpTo(horizontalExit, recordSafe: false);
        _player.AdvanceSideScrollUpdateForValidation(
            Vector2.Right,
            startJump: true);
        UpdatePostObjectPlayerState();
        FailIf(
            _transitions.ScrollActive ||
            _transitions.ScreenTransitionDelay != 4,
            "An airborne side-view edge did not reload " +
            "wScreenTransitionDelay with four updates.");
        _player.WarpTo(horizontalExit, recordSafe: false);
        bool observedAirborneDelay = false;
        for (int airborneUpdate = 0; airborneUpdate < 60; airborneUpdate++)
        {
            _player.AdvanceSideScrollUpdateForValidation(Vector2.Right);
            observedAirborneDelay |= _player.SideScrollAirborne;
            UpdatePostObjectPlayerState();
            FailIf(
                _transitions.ScrollActive,
                "An ordinary side-view scroll began while Link was airborne.");
            if (observedAirborneDelay && !_player.SideScrollAirborne)
                break;
        }
        FailIf(
            !observedAirborneDelay || _player.SideScrollAirborne,
            "The side-view transition delay scenario did not complete its " +
            "natural fall to the corridor floor.");
        while (_transitions.ScreenTransitionDelay != 0)
        {
            int previousDelay = _transitions.ScreenTransitionDelay;
            _player.AdvanceSideScrollUpdateForValidation(Vector2.Right);
            UpdatePostObjectPlayerState();
            FailIf(
                _transitions.ScrollActive ||
                _transitions.ScreenTransitionDelay != previousDelay - 1,
                "screenTransitionState2 did not decrement its airborne exit " +
                $"delay from {previousDelay} to {previousDelay - 1}.");
        }
        _player.AdvanceSideScrollUpdateForValidation(Vector2.Right);
        UpdatePostObjectPlayerState();
        FailIf(
            !_transitions.ScrollActive,
            "Side room 6:29 did not scroll on the first update after its " +
            "four-update airborne exit delay.");
        CompleteTransition();

        PrepareRoom(0x29);
        horizontalExit = new(_currentRoom.Width - 6, 0x58);
        _player.WarpTo(horizontalExit, recordSafe: false);
        for (int frame = 0;
             frame < 24 && !_transitions.ScrollActive;
             frame++)
        {
            _player.AdvanceSideScrollUpdateForValidation(Vector2.Right);
            UpdatePostObjectPlayerState();
        }
        FailIf(
            !_transitions.ScrollActive ||
            _rooms.ActiveGroup != 6 ||
            _currentRoom.Id != 0x2a,
            "Side room 6:29 did not begin its ordinary horizontal scroll to " +
            $"6:2a (position={_player.Position}, " +
            $"precise={_player.PrecisePosition}, " +
            $"walls=${_collision.AdjacentWallsBitset(_player.Position):x2}).");
        CompleteTransition();
        FailIf(
            _rooms.ActiveGroup != 6 ||
            _currentRoom.Id != 0x2a ||
            _player.Position.X != 9,
            "Side room 6:29 did not finish its rightward scroll at 6:2a's " +
            $"left edge (position={_player.Position}).");

        _player.WarpTo(new Vector2(6, 0x58), recordSafe: false);
        for (int frame = 0;
             frame < 24 && !_transitions.ScrollActive;
             frame++)
        {
            _player.AdvanceSideScrollUpdateForValidation(Vector2.Left);
            UpdatePostObjectPlayerState();
        }
        FailIf(
            !_transitions.ScrollActive ||
            _rooms.ActiveGroup != 6 ||
            _currentRoom.Id != 0x29,
            "Side room 6:2a did not begin its ordinary horizontal scroll " +
            "back to 6:29.");
        CompleteTransition();
        FailIf(
            _rooms.ActiveGroup != 6 ||
            _currentRoom.Id != 0x29 ||
            _player.Position.X != _currentRoom.Width - 9,
            "Side room 6:2a did not finish its leftward scroll at 6:29's " +
            $"right edge (position={_player.Position}).");

        // ENEMY_SPARK state 0 resolves visibility and its initial wall angle
        // while the destination room is parsed. Ordinary Spark movement still
        // remains frozen for the duration of the scrolling transition.
        PrepareRoom(0x35);
        OracleRoomData sparkRoom = _world.LoadRoom(4, 0x36);
        _entities.BeginScreenTransition(
            4, sparkRoom, Vector2.Right * sparkRoom.Width);
        List<SparkCharacter> incomingSparks =
            _entities.Entities<SparkCharacter>();
        Vector2[] preloadedSparkPositions =
            incomingSparks.Select(spark => spark.Position).ToArray();
        int[] preloadedSparkAngles =
            incomingSparks.Select(spark => spark.Angle).ToArray();
        Step(8);
        FailIf(
            incomingSparks.Count != 2 ||
            incomingSparks.Any(spark => !spark.Visible || !spark.Initialized) ||
            !incomingSparks.Select(spark => spark.Position)
                .SequenceEqual(preloadedSparkPositions) ||
            !incomingSparks.Select(spark => spark.Angle)
                .SequenceEqual(preloadedSparkAngles),
            "Room 4:36 Sparks were not visible and initialized while frozen " +
            "in the incoming scrolling room.");
        _entities.FinishScreenTransition();
        Step(8);
        FailIf(
            incomingSparks.Select(spark => spark.Position)
                .SequenceEqual(preloadedSparkPositions),
            "Room 4:36 Sparks did not begin moving after scrolling finished.");

        // PART_ENEMY_SWORD $1d, not the Stalfos body, owns blocked sword
        // contacts. Its table ignores spins, emits one clink, and writes the
        // exact LINKDMG_$38/$34 attacker recoil windows.
        PrepareRoom(0x30);
        Step();
        SwordEnemyCharacter swordEnemy =
            _entities.Entities<SwordEnemyCharacter>()[0];
        Vector2 blockingSource = swordEnemy.Position +
            OracleObjectMath.CardinalVector(
                (swordEnemy.Angle + 4) & 0x18) * 16.0f;
        Rect2 bladeHitbox = swordEnemy.EnemySwordCollisionBounds;
        int swordEnemyHealth = swordEnemy.Health;
        var attackerRecoil = new List<SwordAttackerKnockback>();
        _sound.ClearPlayRequestAudit();
        bool firstBladeHit = _entities.ApplySwordHit(
            bladeHitbox,
            blockingSource,
            damage: 2,
            knockbackStrength: EnemyKnockbackStrength.Low,
            collectItemDrops: true,
            attackerKnockback: attackerRecoil.Add,
            swordState: SwordActionState.Swing,
            swordLevel: 1);
        bool repeatedBladeHit = _entities.ApplySwordHit(
            bladeHitbox,
            blockingSource,
            damage: 2,
            knockbackStrength: EnemyKnockbackStrength.Low,
            collectItemDrops: true,
            attackerKnockback: attackerRecoil.Add,
            swordState: SwordActionState.Swing,
            swordLevel: 1);
        Rect2 bodyOnlyHitbox = new(
            swordEnemy.Position - Vector2.One,
            Vector2.One * 2.0f);
        bool blockedBodyHit = _entities.ApplySwordHit(
            bodyOnlyHitbox,
            blockingSource,
            damage: 2,
            knockbackStrength: EnemyKnockbackStrength.Low,
            collectItemDrops: true,
            attackerKnockback: attackerRecoil.Add,
            swordState: SwordActionState.Swing,
            swordLevel: 1);
        FailIf(
            !firstBladeHit ||
            repeatedBladeHit ||
            blockedBodyHit ||
            swordEnemy.Health != swordEnemyHealth ||
            attackerRecoil is not [{ Frames: 8 }] ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1 ||
            _entities.Entities<ClinkEffect>().Count != 1,
            "Sword Stalfos did not silently ignore its guarded body while " +
            "PART_ENEMY_SWORD produced one LINKDMG_$38 clink/recoil " +
            $"(blocks={swordEnemy.BlocksSwordFrom(blockingSource)}, " +
            $"first={firstBladeHit}, repeat={repeatedBladeHit}, " +
            $"body={blockedBodyHit}, health={swordEnemy.Health}/" +
            $"{swordEnemyHealth}, recoil={string.Join(',', attackerRecoil)}, " +
            $"sound={_sound.PlayRequestsFor(OracleSoundEngine.SndClink)}, " +
            $"clinks={_entities.Entities<ClinkEffect>().Count}).");
        Step(8);
        blockingSource = swordEnemy.Position +
            OracleObjectMath.CardinalVector(
                (swordEnemy.Angle + 4) & 0x18) * 16.0f;
        attackerRecoil.Clear();
        FailIf(
            _entities.ApplySwordHit(
                swordEnemy.EnemySwordCollisionBounds,
                blockingSource,
                damage: 2,
                knockbackStrength: EnemyKnockbackStrength.Low,
                collectItemDrops: true,
                attackerKnockback: attackerRecoil.Add,
                swordState: SwordActionState.Swing,
                swordLevel: 1) ||
            attackerRecoil.Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1,
            "PART_ENEMY_SWORD accepted a second contact when Link's " +
            "LINKDMG_$38 recoil expired three updates before the part's " +
            "ENEMYDMG_$4c invincibility counter.");
        Step(3);
        blockingSource = swordEnemy.Position +
            OracleObjectMath.CardinalVector(
                (swordEnemy.Angle + 4) & 0x18) * 16.0f;
        FailIf(
            !_entities.ApplySwordHit(
                swordEnemy.EnemySwordCollisionBounds,
                blockingSource,
                damage: 2,
                knockbackStrength: EnemyKnockbackStrength.Low,
                collectItemDrops: true,
                attackerKnockback: attackerRecoil.Add,
                swordState: SwordActionState.Held,
                swordLevel: 2) ||
            attackerRecoil is not [{ Frames: 6 }],
            "A held Noble Sword did not use PART_ENEMY_SWORD's " +
            "LINKDMG_$34 six-update recoil.");
        Step(9);
        blockingSource = swordEnemy.Position +
            OracleObjectMath.CardinalVector(
                (swordEnemy.Angle + 4) & 0x18) * 16.0f;
        int clinksBeforeSpin =
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink);
        FailIf(
            _entities.ApplySwordHit(
                swordEnemy.EnemySwordCollisionBounds,
                blockingSource,
                damage: 4,
                knockbackStrength: EnemyKnockbackStrength.High,
                collectItemDrops: true,
                swordState: SwordActionState.Spin,
                swordLevel: 2) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) !=
                clinksBeforeSpin,
            "PART_ENEMY_SWORD did not map the Spin Attack collision row to " +
            "effect $00.");
        Vector2 rearSource = swordEnemy.Position -
            OracleObjectMath.CardinalVector(
                (swordEnemy.Angle + 4) & 0x18) * 16.0f;
        swordEnemyHealth = swordEnemy.Health;
        FailIf(
            !_entities.ApplySwordHit(
                swordEnemy.CollisionBounds,
                rearSource,
                damage: 1,
                knockbackStrength: EnemyKnockbackStrength.Low,
                collectItemDrops: true,
                swordState: SwordActionState.Swing,
                swordLevel: 1) ||
            swordEnemy.Health != swordEnemyHealth - 1,
            "Sword Stalfos did not retain ordinary vulnerable body damage " +
            "outside its guarded angle.");

        // Mount after the platform has a nonzero fractional byte. The source
        // copies those fractions to Link once, after which identical 8.8
        // velocities must keep their rendered high-byte offset constant.
        PrepareRoom(0x29);
        _player.WarpTo(new Vector2(0x18, 0x18), recordSafe: false);
        Step();
        WingDungeonSideScrollPlatform movingPlatform =
            _entities.Entities<WingDungeonSideScrollPlatform>()[1];
        FailIf(
            movingPlatform.PrecisePosition.X ==
                Mathf.Floor(movingPlatform.PrecisePosition.X),
            "The D2 side-platform vibration scenario did not begin on a " +
            "nonzero platform fractional byte.");
        _player.WarpTo(
            movingPlatform.Position + Vector2.Up * 15.0f,
            recordSafe: false);
        Step();
        Vector2 ridingDrawOffset =
            _player.Position - movingPlatform.Position;
        FailIf(
            !movingPlatform.LinkRiding ||
            Fraction(_player.PrecisePosition.X) !=
                Fraction(movingPlatform.PrecisePosition.X) ||
            Fraction(_player.PrecisePosition.Y) !=
                Fraction(movingPlatform.PrecisePosition.Y),
            "Mounting a D2 side platform did not copy both source low " +
            "coordinate bytes to Link.");
        for (int frame = 0; frame < 12; frame++)
        {
            _transitions.UpdateCamera();
            Vector2 screenBeforePlatformUpdate =
                WorldToScreen(_player.Position);
            Step();
            UpdatePostObjectPlayerState();
            FailIf(
                _player.Position - movingPlatform.Position != ridingDrawOffset ||
                WorldToScreen(_player.Position) != screenBeforePlatformUpdate,
                "Link's rendered position vibrated relative to a moving D2 " +
                "platform or its post-object camera sample on update " +
                $"{frame + 1}.");
        }

        // thwomp_updateLinkRidingSelf accepts signed X offsets -$13..+$13.
        // Preserve the inclusive positive endpoint and reject the next pixel.
        PrepareRoom(0x2a);
        Step();
        ThwompCharacter ridingThwomp =
            _entities.Entities<ThwompCharacter>().Single();
        float thwompContactY =
            ridingThwomp.Position.Y - ridingThwomp.Record.RadiusY - 6;
        _player.WarpTo(
            new Vector2(ridingThwomp.Position.X + 0x13, thwompContactY),
            recordSafe: false);
        bool ridesAtPositiveEndpoint =
            ridingThwomp.IsLinkRiding(_player, out float thwompTargetY);
        _player.WarpTo(
            new Vector2(ridingThwomp.Position.X + 0x14, thwompContactY),
            recordSafe: false);
        FailIf(
            !ridesAtPositiveEndpoint ||
            thwompTargetY != thwompContactY - 3 ||
            ridingThwomp.IsLinkRiding(_player, out _),
            "thwomp_updateLinkRidingSelf did not preserve its inclusive " +
            "-$13..+$13 X and ±$03 Y riding window.");

        TopDownAirParameters air = TopDownAirDatabase.Shared.Parameters;
        FailIf(
            air.Gravity != 0x20 ||
            air.ReducedGravity != 0x0a ||
            air.MaximumFallSpeed != 0x0300 ||
            air.JumpSpeedZ != -0x01e0 ||
            !air.AnimationPhaseDurations.AsSpan().SequenceEqual([9, 9, 6]),
            "Roc's Feather lost the imported top-down Z and animation table.");
        PrepareRoom(0x2e);
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        _sound.ClearPlayRequestAudit();
        int airborneUpdates = 1;
        int minimumZ = 0;
        _player.AdvanceTopDownAirUpdateForValidation(startJump: true);
        minimumZ = Math.Min(minimumZ, _player.TopDownAirZ);
        while (_player.TopDownAirborne && airborneUpdates < 120)
        {
            _player.AdvanceTopDownAirUpdateForValidation();
            minimumZ = Math.Min(minimumZ, _player.TopDownAirZ);
            airborneUpdates++;
        }
        FailIf(
            _player.TopDownAirborne ||
            airborneUpdates != 31 ||
            minimumZ != -15 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndJump) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndLand) != 1,
            "Top-down Roc's Feather flight lost its exact 31-update " +
            "-$01e0/$20 arc or jump/landing sounds.");

        // Exercise the real Bomb entity handoff into Head Thwomp's mouth and
        // run the complete deceleration into a vulnerable red face.
        PrepareRoom(0x2b);
        _player.WarpTo(new Vector2(0x28, 0x98), recordSafe: false);
        Step();
        HeadThwompBoss head =
            _entities.Entities<HeadThwompBoss>().Single();
        for (int frame = 0;
             frame < 600 &&
             (head.State != HeadThwompState.Spinning ||
              head.Direction != 6);
             frame++)
        {
            Step();
        }
        FailIf(
            head.State != HeadThwompState.Spinning ||
            head.Direction != 6,
            "Head Thwomp did not rotate to its red face in source timing.");
        _player.WarpTo(new Vector2(0x77, 0x50), recordSafe: false);
        _player.Face(Vector2I.Right);
        BombEffect mouthBomb = _entities.Spawn<BombEffect>(
            new BombSpawn(
                _player,
                new BombDatabase().Data,
                4,
                static _ => { }));
        mouthBomb.Throw(
            _player,
            Vector2I.Zero,
            Vector2I.Zero,
            speedZ: 0,
            speedRaw: 0);
        Step();
        FailIf(
            head.State != HeadThwompState.BombPause ||
            mouthBomb.State != BombState.Finished ||
            _entities.Entities<BombEffect>().Count != 0,
            "Head Thwomp did not consume the live Bomb in its $50/$78 " +
            "twelve-pixel mouth window.");
        for (int frame = 0;
             frame < 1400 && head.State != HeadThwompState.Red;
             frame++)
        {
            Step();
        }
        FailIf(
            head.State != HeadThwompState.Red ||
            head.Health != 3,
            "Head Thwomp's bomb spin did not settle on red and remove exactly " +
            "one of four health points.");
        Step();
        FailIf(
            _entities.Entities<ItemDropEffect>().All(
                drop => drop.SubId != ItemDropDatabase.Heart),
            "Head Thwomp's nonlethal red face did not drop its source heart.");

        // Run Swoop through shutter closure, the imported TX_2f00 lease, its
        // bounce, and the three-flap miniboss handoff.
        PrepareRoom(0x34);
        SwoopBoss swoop = _entities.Entities<SwoopBoss>().Single();
        for (int frame = 0;
             frame < 240 && swoop.State == SwoopState.WaitingForDoors;
             frame++)
        {
            Step();
        }
        for (int frame = 0;
             frame < 360 && swoop.State != SwoopState.IntroDialogue;
             frame++)
        {
            Step();
        }
        FailIf(
            swoop.State != SwoopState.IntroDialogue ||
            !_dialogue.IsOpen ||
            !_entities.LinkCollisionsAndMenuDisabled,
            "Swoop did not close its shutters, land/bounce, lock Link, and " +
            "open imported TX_2f00.");
        _dialogue.Close();
        Step();
        Step(144);
        FailIf(
            swoop.State != SwoopState.Flying ||
            _entities.LinkCollisionsAndMenuDisabled ||
            _entities.PlayerMenusDisabled,
            "Swoop did not begin the miniboss and restore Link after its " +
            "three 48-update flaps.");

        RestoreFlags();
        _dialogue.Close();
        _player.EndCutsceneControl();
        _player.EndGetItemTwoHandPose();
        _player.Visible = true;
        LoadValidationRoom(0, 0x00);

        GD.Print(
            "Validated complete Wing Dungeon dungeon02 coverage: all 34 rooms, " +
            "six chests, Roc's Feather top-down arc, ordered Sparks/Whisps/" +
            "Thwomps/Peahats/Shrouded Stalfos/color Gels, toggle/color/cube " +
            "puzzles, side platforms, circular platforms, persistent " +
            "minecarts and gates, Head Thwomp, Swoop/TX_2f00, Heart Container, " +
            "Ancient Wood, and the second-Essence exit route.");

        static float Fraction(float value) =>
            value - Mathf.Floor(value);
    }
}
