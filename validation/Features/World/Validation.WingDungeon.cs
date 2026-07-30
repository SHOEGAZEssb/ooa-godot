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
        static bool IsGbcColor(Color color, int red, int green, int blue)
        {
            const float textureTolerance = 1.5f / 255.0f;
            return Mathf.Abs(color.R - red / 31.0f) <= textureTolerance &&
                Mathf.Abs(color.G - green / 31.0f) <= textureTolerance &&
                Mathf.Abs(color.B - blue / 31.0f) <= textureTolerance;
        }
        int[] rooms = Enumerable.Range(0x27, 0x22).ToArray();
        byte[] originalFlags = rooms
            .Select(room => _saveData.GetRoomFlags(4, room))
            .ToArray();
        var data = new WingDungeonDatabase();
        var interactions = new DungeonInteractionDatabase();

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
        MinecartRuntimeState.Reset(
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

        List<DungeonObjectRecord> native = rooms
            .SelectMany(room => data.GetRoomRecords(4, room))
            .ToList();
        var kindCounts = native
            .GroupBy(record => record.Kind)
            .ToDictionary(group => group.Key, group => group.Count());
        FailIf(
            native.Count != 40 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.ToggleFloor) != 5 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.SidePlatform) != 4 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.CircularSidePlatform) != 3 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.EnemyChest) != 3 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.ColoredCube) != 2 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.HeadThwomp) != 1 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.Swoop) != 1 ||
            kindCounts.GetValueOrDefault(DungeonObjectKind.Essence) != 1 ||
            data.Pattern(DungeonObjectKind.FloorPatternKey, 0)
                .SequenceEqual(new byte[] { 0x67, 0x77 }) == false ||
            data.Pattern(DungeonObjectKind.ColoredBlockKey, 2)
                .SequenceEqual(new byte[] { 0x4a, 0x59, 0x5b, 0x6a }) == false ||
            interactions.SwitchTiles(0x13) != (0x5c, 0x5a) ||
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
                        _entities.Entities<MovingSideScrollPlatformRoomEntity>().Count != 2,
                        "Room 4:29 did not create both imported side platforms.");
                    break;
                case 0x2a:
                    FailIf(
                        _entities.Entities<MovingSideScrollPlatformRoomEntity>().Count != 2 ||
                        _entities.Entities<ThwompCharacter>().Count != 1,
                        "Room 4:2a did not create its two side platforms and Thwomp.");
                    break;
                case 0x2b:
                    FailIf(
                        _entities.Entities<CircularSideScrollPlatformRoomEntity>().Count != 3 ||
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
                        _entities.Entities<DungeonPatternKeyRoomEntity>().Count != 1 ||
                        _entities.Entities<ToggleFloorRoomEntity>().Count != 1,
                        "Room 4:2e did not create its Peahats and floor-pattern key puzzle.");
                    break;
                case 0x30:
                    FailIf(
                        _entities.Entities<SwordEnemyCharacter>().Count != 2 ||
                        _entities.Entities<EnemyClearChestRoomEntity>().Count != 1,
                        "Room 4:30 did not create its Sword Stalfos and kill chest.");
                    break;
                case 0x33:
                case 0x35:
                case 0x40:
                    FailIf(
                        _entities.Entities<MinecartRoomEntity>().Count != 1,
                        $"Room 4:{room:x2} did not restore its static minecart.");
                    break;
                case 0x34:
                    FailIf(
                        _entities.Entities<SwoopBoss>().Count != 1 ||
                        _entities.Entities<DungeonRewardRoomEntity>().Count != 1,
                        "Room 4:34 did not create Swoop and its miniboss reward.");
                    break;
                case 0x36:
                    FailIf(
                        _entities.Entities<SparkCharacter>().Count != 2,
                        "Room 4:36 did not create both Sparks.");
                    break;
                case 0x38:
                    FailIf(
                        _entities.Entities<DungeonEssence>() is not
                            [{ EssenceIndex: 1 }],
                        "Room 4:38 did not create the second Essence.");
                    break;
                case 0x3e:
                    FailIf(
                        _entities.Entities<ColorChangingGelCharacter>().Count != 7 ||
                        _entities.Entities<FloorColorChangerRoomEntity>().Count != 1 ||
                        _entities.Entities<EnemyClearChestRoomEntity>().Count != 1,
                        "Room 4:3e did not create seven color Gels, the exact " +
                        "floor changer, and its Boss Key chest.");
                    break;
                case 0x43:
                    FailIf(
                        _entities.Entities<ColoredCubeRoomEntity>().Count != 1 ||
                        _entities.Entities<ColoredCubeFlameRoomEntity>().Count != 1 ||
                        _entities.Entities<ColoredCubeSensorRoomEntity>().Count != 1,
                        "Room 4:43 did not create its color-cube/flame/sensor puzzle.");
                    break;
            }
        }

        // INTERAC_MINECART waits for four centered push updates, then gives
        // Link the source SPEED_80/-$01c0 jump. SPECIALOBJECT_MINECART owns
        // Link's screen-transition coordinate, persists as one object across
        // rooms, animates at 6/6 updates, and gives Link the matching exit
        // jump before restoring the stationary blocker.
        var dungeonVisuals = new DungeonInteractionVisualDatabase();
        DungeonInteractionVisual minecartVisual =
            dungeonVisuals.Visual("minecart");
        AnimationDefinition verticalRideAnimation =
            OracleGraphicsCache.GetAnimationDefinition(
                minecartVisual.Animations[2]);
        AnimationDefinition horizontalRideAnimation =
            OracleGraphicsCache.GetAnimationDefinition(
                minecartVisual.Animations[3]);
        FailIf(
            minecartVisual.Animations.Length != 4 ||
            verticalRideAnimation.Frames is not
            [
                { Duration: 6, Parameter: 0 },
                { Duration: 6, Parameter: 4 }
            ] ||
            horizontalRideAnimation.Frames is not
            [
                { Duration: 6, Parameter: 0 },
                { Duration: 6, Parameter: 4 }
            ],
            "SPECIALOBJECT_MINECART lost its imported vertical/horizontal " +
            "6/6-update animation and $00/$04 Link-offset parameters.");

        PrepareRoom(0x33);
        MinecartRoomEntity minecart =
            _entities.Entities<MinecartRoomEntity>().Single();
        _player.WarpTo(
            minecart.Position + new Vector2(-8, 8),
            recordSafe: false);
        _player.Face(Vector2I.Right);
        _player.UpdatePushingState(Vector2I.Right);
        Step();
        FailIf(
            minecart.PushCounter != 4 || minecart.Mounting,
            "INTERAC_MINECART accepted a diagonal push without Link being " +
            "within the source's four-pixel X-or-Y centering threshold.");

        _player.WarpTo(
            minecart.Position + Vector2.Left * 12,
            recordSafe: false);
        _player.Face(Vector2I.Right);
        _player.UpdatePushingState(Vector2I.Right);
        _sound.ClearPlayRequestAudit();
        Step(3);
        FailIf(
            minecart.Mounting ||
            minecart.PushCounter != 1 ||
            _player.MinecartJumpActive,
            "INTERAC_MINECART did not retain its four-update centered push.");
        Step();
        FailIf(
            !minecart.Mounting ||
            !_player.MinecartJumpActive ||
            _player.MinecartJumpAngle != 0x08 ||
            _player.FacingVector != Vector2I.Right ||
            _player.TopDownAirSpeedZ != -0x01c0,
            "INTERAC_MINECART did not begin Link's source angle-$08 " +
            "rightward boarding jump independently of its upward rail.");

        int mountJumpUpdates = 0;
        while (!minecart.Riding && mountJumpUpdates < 60)
        {
            _player.AdvanceMinecartJumpUpdateForValidation();
            Step();
            mountJumpUpdates++;
        }
        FailIf(
            !minecart.Riding ||
            mountJumpUpdates != 26 ||
            _player.MinecartJumpActive ||
            minecart.CurrentAnimationIndex != 2 + (minecart.Direction & 1) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndJump) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndLand) != 1,
            "Minecart boarding lost the exact falling-Z handoff, moving " +
            "animation selection, or jump/land sounds.");

        int equippedA = _inventory.EquippedA;
        int equippedB = _inventory.EquippedB;
        Vector2 rideInputStart = _player.PrecisePosition;
        int rideStartTile = _currentRoom.GetMetatile(minecart.Position);
        int rideStartDirection = minecart.Direction;
        _inventory.EquipA(InventoryState.ItemSword);
        _player.Face(Vector2I.Left);
        _player.StartSwordAttackForValidation(Vector2.Zero);
        _player.UpdateMinecartRideDirection(Vector2.Right);
        try
        {
            _player._PhysicsProcess(update);
        }
        finally
        {
            _inventory.EquipA(equippedA);
            _inventory.EquipB(equippedB);
        }
        FailIf(
            !_player.MinecartRideActive ||
            _player.FacingVector != Vector2I.Right ||
            !_player.IsAttacking ||
            _player.PrecisePosition != rideInputStart ||
            _entities.PlayerSwordDisabled ||
            _entities.PlayerItemUsageDisabled ||
            _entities.PlayerMenusDisabled,
            "linkState01 did not accept direction and ITEM_SWORD input while " +
            "SPECIALOBJECT_MINECART retained sole movement ownership " +
            $"(ride={_player.MinecartRideActive}, " +
            $"facing={_player.FacingVector}, attacking={_player.IsAttacking}, " +
            $"start={rideInputStart}, position={_player.PrecisePosition}, " +
            $"swordDisabled={_entities.PlayerSwordDisabled}, " +
            $"itemsDisabled={_entities.PlayerItemUsageDisabled}, " +
            $"menusDisabled={_entities.PlayerMenusDisabled}).");
        _player.AdvanceSwordForValidation(32, buttonHeld: false);
        _player._PhysicsProcess(update);

        int firstRideFrame = minecart.CurrentAnimationFrame;
        Step(5);
        FailIf(
            minecart.CurrentAnimationFrame != firstRideFrame,
            "SPECIALOBJECT_MINECART advanced before its six-update frame boundary.");
        Step();
        FailIf(
            minecart.CurrentAnimationFrame == firstRideFrame,
            "SPECIALOBJECT_MINECART did not advance on its sixth update.");

        int minecartRoomTransitions = 0;
        int rideUpdates = 6;
        int minecartRideRoom = _currentRoom.Id;
        Vector2 dismountJumpStart = Vector2.Zero;
        int dismountTravelAngle = 0xff;
        var minecartTrackTrace = new List<string>();
        while (!minecart.Dismounting && rideUpdates < 2400)
        {
            if ((Mathf.FloorToInt(minecart.Position.Y) & 0x0f) == 8 &&
                (Mathf.FloorToInt(minecart.Position.X) & 0x0f) == 8)
            {
                minecartTrackTrace.Add(
                    $"4:{minecartRideRoom:x2}@{minecart.Position}" +
                    $"/${_currentRoom.GetMetatile(minecart.Position):x2}" +
                    $"/d{minecart.Direction}/a${minecart.Angle:x2}");
            }
            _player._PhysicsProcess(update);
            Vector2 linkPositionBeforeCartUpdate = _player.PrecisePosition;
            int cartAngleBeforeUpdate = minecart.Angle;
            Step();
            UpdatePostObjectPlayerState();
            rideUpdates++;
            if (minecart.Dismounting)
            {
                dismountJumpStart =
                    linkPositionBeforeCartUpdate + Vector2.Down * 6;
                dismountTravelAngle = cartAngleBeforeUpdate;
            }
            if (!_transitions.ScrollActive)
                continue;

            int sourceRoom = minecartRideRoom;
            CompleteTransition();
            minecartRoomTransitions++;
            minecartRideRoom = _currentRoom.Id;
            FailIf(
                _currentRoom.Id == sourceRoom ||
                minecartRoomTransitions == 1 &&
                    (sourceRoom != 0x33 || _currentRoom.Id != 0x2f) ||
                minecartRoomTransitions == 2 &&
                    (sourceRoom != 0x2f || _currentRoom.Id != 0x33) ||
                _entities.Entities<MinecartRoomEntity>() is not
                    [var retainedMinecart] ||
                !ReferenceEquals(retainedMinecart, minecart) ||
                !minecart.Riding,
                "SPECIALOBJECT_MINECART did not persist as the single active " +
                "cart across its dungeon room scroll " +
                $"(source=4:{sourceRoom:x2}, destination=4:{_currentRoom.Id:x2}, " +
                $"carts={_entities.Entities<MinecartRoomEntity>().Count}, " +
                $"same={_entities.Entities<MinecartRoomEntity>().Contains(minecart)}, " +
                $"riding={minecart.Riding}).");
        }
        FailIf(
            !minecart.Dismounting ||
            !_player.MinecartJumpActive ||
            minecartRoomTransitions != 2 ||
            _currentRoom.Id != 0x33 ||
            _player.PrecisePosition != dismountJumpStart ||
            _player.MinecartJumpAngle != dismountTravelAngle ||
            _player.TopDownAirZ != -6 ||
            _player.TopDownAirSpeedZ != -0x01c0,
            "The Wing Dungeon minecart did not begin its source platform " +
            "dismount jump " +
            $"(updates={rideUpdates}, transitions={minecartRoomTransitions}, " +
            $"room=4:{_currentRoom.Id:x2}, position={minecart.Position}, " +
            $"riding={minecart.Riding}, dismounting={minecart.Dismounting}, " +
            $"direction={minecart.Direction}, startTile=${rideStartTile:x2}, " +
            $"startDirection={rideStartDirection}, " +
            $"jump={_player.MinecartJumpActive}, " +
            $"z={_player.TopDownAirZ}, speedZ={_player.TopDownAirSpeedZ}, " +
            $"track={string.Join(",", minecartTrackTrace)}).");

        int dismountJumpUpdates = 0;
        while ((_player.MinecartJumpActive || minecart.Dismounting) &&
            dismountJumpUpdates < 60)
        {
            _player.AdvanceMinecartJumpUpdateForValidation();
            Step();
            dismountJumpUpdates++;
        }
        Vector2 dismountDirection =
            OracleObjectMovement.Shared.Direction(dismountTravelAngle);
        Vector2 escapedMovement = _collision.ResolveMovement(
            _player.PrecisePosition,
            dismountDirection,
            allowWallSlide: true);
        FailIf(
            dismountJumpUpdates != 32 ||
            _player.MinecartJumpActive ||
            minecart.Dismounting ||
            minecart.Riding ||
            escapedMovement.Dot(dismountDirection) <= 0,
            "SPECIALOBJECT_MINECART did not finish Link's 32-update exit " +
            "jump with movement away from the restored cart blocker.");

        MinecartRuntimeState.Reset(
            _entities.RuntimeState, data.Minecarts);

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
        ThwompCharacter incomingThwomp =
            _entities.Entities<ThwompCharacter>().Single();
        Vector2 preloadedThwompPosition = incomingThwomp.Position;
        int preloadedThwompAnimation = incomingThwomp.AnimationIndex;
        Step(8);
        FailIf(
            !incomingThwomp.Visible ||
            incomingThwomp.State != ThwompState.Waiting ||
            incomingThwomp.Position != preloadedThwompPosition ||
            incomingThwomp.AnimationIndex != preloadedThwompAnimation ||
            preloadedThwompAnimation != 4,
            "Room 6:2a's Thwomp did not become visible in source state $08 " +
            "with animation $04 while remaining frozen in the incoming " +
            "scrolling room.");
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

        // updateEnemies dispatches state 0 even while wScrollMode is active.
        // Whisp state 0 consumes one RNG value per object, installs its angle,
        // and becomes visible; state-8 bouncing/movement remains frozen.
        PrepareRoom(0x2d);
        OracleRoomData whispRoom = _world.LoadRoom(4, 0x2c);
        _entities.BeginScreenTransition(
            4, whispRoom, Vector2.Left * whispRoom.Width);
        List<WhispCharacter> incomingWhisps =
            _entities.Entities<WhispCharacter>();
        Vector2[] preloadedWhispPositions =
            incomingWhisps.Select(whisp => whisp.Position).ToArray();
        int[] preloadedWhispAngles =
            incomingWhisps.Select(whisp => whisp.Angle).ToArray();
        Step(8);
        FailIf(
            incomingWhisps.Count != 2 ||
            incomingWhisps.Any(whisp =>
                !whisp.Visible ||
                !whisp.Initialized ||
                whisp.Angle is not (0x04 or 0x0c or 0x14 or 0x1c)) ||
            !incomingWhisps.Select(whisp => whisp.Position)
                .SequenceEqual(preloadedWhispPositions) ||
            !incomingWhisps.Select(whisp => whisp.Angle)
                .SequenceEqual(preloadedWhispAngles),
            "Room 4:2c Whisps did not consume source state-0 initialization " +
            "and become visible while remaining frozen in the incoming room.");
        _entities.FinishScreenTransition();
        Step(8);
        FailIf(
            incomingWhisps.Select(whisp => whisp.Position)
                .SequenceEqual(preloadedWhispPositions),
            "Room 4:2c Whisps did not begin moving after scrolling finished.");

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
        MovingSideScrollPlatformRoomEntity movingPlatform =
            _entities.Entities<MovingSideScrollPlatformRoomEntity>()[1];
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
        AnimationFrameDefinition ordinaryThwompFrame =
            OracleGraphicsCache.GetAnimationDefinition(
                ridingThwomp.Record.Animations[4]).Frames[0];
        using Texture2D expectedOrdinaryThwomp =
            NpcCharacter.BuildOamTextureUncachedForValidation(
                EnemyVisualSource.LoadComposite(ridingThwomp.Record.Sprites),
                ordinaryThwompFrame.EncodedOam,
                ridingThwomp.Record.TileBase,
                ridingThwomp.Record.Palette,
                sourceGrayscaleInverted:
                    ridingThwomp.Record.SourceGrayscaleInverted);
        using (Image ordinaryThwompImage =
            ridingThwomp.CurrentAnimationTexture.GetImage())
        using (Image expectedOrdinaryThwompImage =
            expectedOrdinaryThwomp.GetImage())
        {
            FailIf(
                ridingThwomp.AnimationIndex != 4 ||
                ridingThwomp.Record.SourceGrayscaleInverted ||
                ridingThwomp.Animation.CurrentOffset != new Vector2(-16, -16) ||
                ordinaryThwompImage.GetWidth() != 32 ||
                ordinaryThwompImage.GetHeight() != 32 ||
                OracleGraphicsCache.PixelHash(ordinaryThwompImage) !=
                    OracleGraphicsCache.PixelHash(expectedOrdinaryThwompImage) ||
                !IsGbcColor(
                    ordinaryThwompImage.GetPixel(2, 1),
                    0x00, 0x00, 0x1f) ||
                !IsGbcColor(
                    ordinaryThwompImage.GetPixel(16, 1),
                    0x0e, 0x15, 0x1f) ||
                !IsGbcColor(
                    ordinaryThwompImage.GetPixel(16, 28),
                    0x00, 0x00, 0x00),
                "ENEMY_THWOMP did not retain spr_thwomps.properties' " +
                "invert=false shade order and complete disassembly " +
                "32-by-32 animation-$04 OAM sprite.");
        }
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

        // ENEMYCOLLISION_THWOMP maps the sword rows to effects $15-$17.
        // Those preserve health, install ENEMYDMG_$34's negative
        // invincibility, allocate one clink, and write recoil to ITEM_SWORD.
        Vector2 thwompSwordPosition =
            ridingThwomp.Position + Vector2.Left * 4;
        var thwompSwordHitbox = new Rect2(
            thwompSwordPosition - Vector2.One,
            Vector2.One * 2);
        int thwompHealth = ridingThwomp.Health;
        SwordAttackerKnockback? thwompRecoil = null;
        _sound.ClearPlayRequestAudit();
        FailIf(
            ridingThwomp.ArmoredAttackerKnockbackFrames(
                EnemyKnockbackStrength.Low) != 11 ||
            ridingThwomp.ArmoredAttackerKnockbackFrames(
                EnemyKnockbackStrength.Normal) != 19 ||
            ridingThwomp.ArmoredAttackerKnockbackFrames(
                EnemyKnockbackStrength.High) != 25 ||
            !_entities.ApplySwordHit(
                thwompSwordHitbox,
                ridingThwomp.Position + Vector2.Left * 16,
                damage: 99,
                knockbackStrength: EnemyKnockbackStrength.Low,
                attackerKnockback: response => thwompRecoil = response) ||
            ridingThwomp.Health != thwompHealth ||
            ridingThwomp.InvincibilityCounter != -28 ||
            ridingThwomp.KnockbackCounter != 0 ||
            thwompRecoil is not
            {
                SourcePosition: var thwompRecoilSource,
                Frames: 11
            } ||
            thwompRecoilSource != ridingThwomp.Position ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBombLand) != 1 ||
            _entities.Entities<ClinkEffect>() is not
            [
                {
                    Position: var thwompClinkPosition,
                    Flickers: false
                }
            ] ||
            thwompClinkPosition !=
                ridingThwomp.Position + Vector2.Left * 2,
            "The ordinary Thwomp did not apply its source armored sword " +
            "clink, ITEM_SWORD recoil, and ENEMYDMG_$34 repeat suppression.");
        FailIf(
            _entities.ApplySwordHit(
                thwompSwordHitbox,
                ridingThwomp.Position + Vector2.Left * 16,
                damage: 99,
                knockbackStrength: EnemyKnockbackStrength.Low,
                attackerKnockback: _ => { }) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBombLand) != 1 ||
            _entities.Entities<ClinkEffect>().Count != 1,
            "Thwomp armor accepted a repeated sword collision before its " +
            "negative invincibility counter expired.");

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
        var bosses = new DungeonBossDatabase();
        AnimationFrameDefinition headFrame =
            OracleGraphicsCache.GetAnimationDefinition(
                head.Record.Animations[0]).Frames[0];
        (Texture2D expectedHeadTexture, Vector2 expectedHeadOffset) =
            NpcCharacter
                .BuildPositionedOamTextureWithPaletteOverridesUncachedForValidation(
                    EnemyVisualSource.LoadComposite(head.Record.Sprites),
                    headFrame.EncodedOam,
                    head.Record.TileBase,
                    head.Record.Palette,
                    bosses.HeadThwompPalettes,
                    head.Record.SourceGrayscaleInverted);
        using (expectedHeadTexture)
        using (Image headImage = head.CurrentAnimationTexture.GetImage())
        using (Image expectedHeadImage = expectedHeadTexture.GetImage())
        {
            FailIf(
                head.Animation.CurrentOffset != new Vector2(-20, -27) ||
                expectedHeadOffset != new Vector2(-20, -27) ||
                headImage.GetWidth() != 40 ||
                headImage.GetHeight() != 48 ||
                OracleGraphicsCache.PixelHash(headImage) !=
                    OracleGraphicsCache.PixelHash(expectedHeadImage),
                "Head Thwomp's 13-cell face was clipped or displaced from " +
                "its disassembly 40-by-48 positioned OAM bounds.");
        }
        FailIf(
            bosses.HeadThwompPalettes is not { Count: 1 } ||
            !bosses.HeadThwompPalettes.ContainsKey(6) ||
            !IsGbcColor(
                bosses.HeadThwompPalettes[6][0], 0x1f, 0x1f, 0x1f) ||
            !IsGbcColor(
                bosses.HeadThwompPalettes[6][1], 0x00, 0x00, 0x00) ||
            !IsGbcColor(
                bosses.HeadThwompPalettes[6][2], 0x14, 0x01, 0x1b) ||
            !IsGbcColor(
                bosses.HeadThwompPalettes[6][3], 0x16, 0x0f, 0x1f),
            "Head Thwomp did not import PALH $81's paletteData4958 into " +
            "OBJ palette 6.");
        Vector2 headSwordPosition =
            head.Position + Vector2.Left * 4;
        var headSwordHitbox = new Rect2(
            headSwordPosition - Vector2.One,
            Vector2.One * 2);
        int headHealthBeforeSword = head.Health;
        _sound.ClearPlayRequestAudit();
        FailIf(
            !_entities.ApplySwordHit(
                headSwordHitbox,
                head.Position + Vector2.Left * 16,
                damage: 99,
                knockbackStrength: EnemyKnockbackStrength.High) ||
            head.Health != headHealthBeforeSword ||
            head.InvincibilityCounter != -20 ||
            head.KnockbackCounter != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBombLand) != 0 ||
            _entities.Entities<ClinkEffect>() is not
            [
                {
                    Position: var headClinkPosition,
                    Flickers: false
                }
            ] ||
            headClinkPosition != head.Position + Vector2.Left * 2,
            "Head Thwomp did not apply COLLISIONEFFECT_$1b's single " +
            "midpoint clink and ENEMYDMG_$28 repeat suppression.");
        FailIf(
            _entities.ApplySwordHit(
                headSwordHitbox,
                head.Position + Vector2.Left * 16,
                damage: 99,
                knockbackStrength: EnemyKnockbackStrength.High) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndClink) != 1 ||
            _entities.Entities<ClinkEffect>().Count != 1,
            "Head Thwomp accepted a repeated sword collision before its " +
            "negative invincibility counter expired.");
        bool sawPurplePalette = false;
        for (int frame = 0;
             frame < 600 &&
             (head.State != HeadThwompState.Spinning ||
              head.Direction != 6);
             frame++)
        {
            Step();
            if (head.Direction == 4 && head.AnimationIndex == 4)
            {
                using Image purpleImage =
                    head.CurrentAnimationTexture.GetImage();
                bool hasDarkPurple = false;
                bool hasLightPurple = false;
                for (int y = 0; y < purpleImage.GetHeight(); y++)
                for (int x = 0; x < purpleImage.GetWidth(); x++)
                {
                    Color pixel = purpleImage.GetPixel(x, y);
                    hasDarkPurple |=
                        IsGbcColor(pixel, 0x14, 0x01, 0x1b);
                    hasLightPurple |=
                        IsGbcColor(pixel, 0x16, 0x0f, 0x1f);
                }
                sawPurplePalette |= hasDarkPurple && hasLightPurple;
            }
        }
        FailIf(
            head.State != HeadThwompState.Spinning ||
            head.Direction != 6 ||
            !sawPurplePalette,
            "Head Thwomp did not rotate through PALH $81's purple sprite " +
            "face to its red face in source timing.");
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
