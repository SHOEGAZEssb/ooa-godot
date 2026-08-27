using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom449EchoingHowl()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
            {
                _entities.Update(update, _player);
                _roomEvents.Update(update);
            }
        }

        var data = new MoonlitGrottoDatabase();
        DungeonInteractionVisual visual =
            new DungeonInteractionVisualDatabase().Visual("echoing-howl");
        IReadOnlyList<DungeonObjectRecord> records =
            data.GetRoomRecords(4, 0x49);
        FailIf(
            records is not
                [{
                    Order: 0,
                    Kind: DungeonObjectKind.Essence,
                    Id: 0x7f,
                    SubId: 0x00,
                    Y: 0x28,
                    X: 0x78,
                    Predicate: DungeonObjectCondition.Always,
                    Source: "mainData.s:group4Map49ObjectData"
                }] ||
            visual is not
                {
                    TileBase: 0x06,
                    Palette: 0x03,
                    Animations.Length: 1
                } ||
            !data.EssenceMessage.Contains(
                "Echoing Howl", StringComparison.Ordinal),
            "Room 4:49 lost INTERAC_ESSENCE $7f:$00, the third " +
            "@essenceOamData row, or TX_0010.");

        _saveData.SetRoomFlag(
            4, 0x49, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(4, 0x49);
        DungeonEssence essence =
            _entities.Entities<DungeonEssence>().Single();
        FailIf(
            _rooms.CurrentDungeonIndex != 3 ||
            essence.Position != new Vector2(0x78, 0x28) ||
            essence.EssenceIndex != 2 || essence.Collected ||
            essence.ExitWarp is not
                {
                    SourceGroup: 4,
                    SourceRoom: 0x49,
                    DestinationGroup: 0,
                    DestinationRoom: 0xba,
                    DestinationPosition: 0x55,
                    DestinationParameter: 0,
                    DestinationTransition: 1
                } ||
            _currentRoom.GetTerrainInfo(
                new Vector2(0x78, 0x28)).Collision != 0x0f ||
            !essence.BlocksLink(new Vector2(0x78, 0x28)),
            "Room 4:49 did not create D3's Echoing Howl and its always-present " +
            "subid-$01 pedestal with the source exit route.");

        _player.WarpTo(new Vector2(0x78, 0x3a), recordSafe: false);
        _player.Face(Vector2I.Up);
        _sound.ClearPlayRequestAudit();
        for (int frame = 0; frame < 180 && !_dialogue.IsOpen; frame++)
            Step();
        FailIf(
            !_dialogue.IsOpen || !essence.ReadyForDialogue ||
            !_player.IsHoldingItemTwoHands ||
            !_saveData.HasRoomFlag(
                4, 0x49, OracleSaveData.RoomFlagItem) ||
            (_inventory.Essences & 0x04) == 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDropEssence) != 1 ||
            _sound.PlayRequestsFor(
                OracleSoundEngine.SndCtrlSlowFadeOut) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusGetEssence) != 1,
            "Room 4:49's Echoing Howl did not approach, fall, enter the " +
            "two-hand pose, show TX_0010, set ROOMFLAG_ITEM/D3's Essence bit, " +
            "and start the source sounds.");

        _dialogue.Close();
        Step();
        FailIf(
            !essence.SwirlActive ||
            _roomEvents.DungeonEssence.Counter != 360 ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusEssence) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndEnergyThing) != 1,
            "Echoing Howl did not begin the common 360-update inward-energy " +
            "swirl on the post-dialogue update.");
        for (int frame = 0;
             frame < 520 && !_transitions.IsTransitioning;
             frame++)
        {
            Step();
        }
        FailIf(
            !_transitions.IsTransitioning || essence.SwirlActive ||
            _roomEvents.DungeonEssence.TracksEssence ||
            !_player.IsHoldingItemTwoHands ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFadeOut) != 4 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1,
            "Room 4:49 did not finish the common 360/20/20/40/30 Essence " +
            "cadence and begin its delayed white exit warp.");

        for (int frame = 0;
             frame < 180 && _transitions.IsTransitioning;
             frame++)
        {
            _transitions.Update(update);
        }
        FailIf(
            _transitions.IsTransitioning ||
            _rooms.ActiveGroup != 0 || _rooms.CurrentRoom.Id != 0xba ||
            _currentRoom.Id != 0xba ||
            _player.Position != new Vector2(0x58, 0x58) ||
            _player.IsHoldingItemTwoHands ||
            _roomView.BackgroundFadeAlpha != 0.0f,
            "D3's Essence did not finish its source warp to 0:ba/$55 with " +
            "the held pose and white fade cleaned up.");

        LoadValidationRoom(4, 0x49);
        DungeonEssence collected =
            _entities.Entities<DungeonEssence>().Single();
        FailIf(
            !collected.Collected ||
            _entities.Entities<DungeonEssence>().Count != 1 ||
            _currentRoom.GetTerrainInfo(
                new Vector2(0x78, 0x28)).Collision != 0x0f ||
            !collected.BlocksLink(new Vector2(0x78, 0x28)),
            "Collected room 4:49 re-entry did not delete only Echoing Howl's " +
            "essence/glow while retaining the subid-$01 pedestal and collision.");

        GD.Print(
            "Validated room 4:49 Echoing Howl: imported placement/OAM/text, " +
            "pedestal, collection, Essence bit, common timing/sounds, exit " +
            "warp, and persistent re-entry.");
    }

    private void ValidateRoom44aShadowHagBoss()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static Vector2 Point(int packedPosition) => new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }
        void FaceBoss(ShadowHagBoss target)
        {
            Vector2 delta = target.Position - _player.Position;
            _player.Face(Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                ? delta.X >= 0 ? Vector2I.Right : Vector2I.Left
                : delta.Y >= 0 ? Vector2I.Down : Vector2I.Up);
        }

        var data = new MoonlitGrottoDatabase();
        var visuals = new DungeonInteractionVisualDatabase();
        var bosses = new DungeonBossDatabase();
        var mechanics = new DungeonMechanicDatabase();
        IReadOnlyList<DungeonObjectRecord> records =
            data.GetRoomRecords(4, 0x4a);
        DungeonInteractionVisual shadowVisual =
            visuals.Visual("shadow-hag-shadow");
        FailIf(
            records.Select(record =>
                (record.Order, record.Kind, record.Id, record.SubId,
                 record.Y, record.X)).ToArray() is not
                [
                    (2, DungeonObjectKind.BossReward, 0x20, 0x01,
                        0x58, 0x78),
                    (3, DungeonObjectKind.ShadowHag, 0x7a, 0x00,
                        0x58, 0xd8)
                ] ||
            bosses.Enemy(0x7a) is not
                {
                    Health: 12, DamageQuarters: 3,
                    RadiusY: 9, RadiusX: 9,
                    Palette: 3, Sprites.Length: 2,
                    Animations.Length: 7
                } ||
            bosses.Enemy(0x42) is not
                {
                    Health: 2, DamageQuarters: 1,
                    RadiusY: 6, RadiusX: 6,
                    TileBase: 0x14, Palette: 2,
                    Animations.Length: 1
                } ||
            shadowVisual is not
                {
                    TileBase: 0, Palette: 0,
                    Sprites: ["spr_projectiles_3"],
                    Animations.Length: 5
                } ||
            string.IsNullOrWhiteSpace(data.ShadowHagMessage),
            "Room 4:4a lost its source-ordered reward/boss, imported $7a/$42 " +
            "definitions, PART $41 visual, or TX_2f2b text.");

        _saveData.SetRoomFlag(
            4, 0x4a, OracleSaveData.RoomFlag80, value: false);
        _saveData.SetRoomFlag(
            4, 0x4a, OracleSaveData.RoomFlagItem, value: false);
        _sound.ClearPlayRequestAudit();
        OracleRoomData transitionSource = _world.LoadRoom(4, 0x49);
        OracleRoomData transitionDestination = _world.LoadRoom(4, 0x4a);
        _entities.LoadRoom(4, transitionSource);
        Vector2 incomingOffset = Vector2.Left * transitionDestination.Width;
        _entities.BeginScreenTransition(
            4, transitionDestination, incomingOffset);
        ShadowHagBoss preloadedBoss =
            _entities.Entities<ShadowHagBoss>().Single();
        FailIf(
            preloadedBoss.Visible ||
            preloadedBoss.State != ShadowHagState.IntroWaitingForDoors ||
            preloadedBoss.TransitionDrawOffset != incomingOffset ||
            _sound.PlayRequestsFor(
                OracleSoundEngine.SndCtrlStopMusic) != 1,
            "Incoming room 4:4a did not resolve Shadow Hag's hidden state 0 " +
            "before exposing the scrolling destination.");
        _entities.Update(1.0, _player);
        FailIf(
            preloadedBoss.State != ShadowHagState.IntroWaitingForDoors,
            "Incoming room 4:4a advanced Shadow Hag while destination " +
            "entities were frozen during scrolling.");
        _entities.FinishScreenTransition();

        SeedShooterRecord shooter = SeedShooterRecord.Load();
        SeedRecord ember = new SeedSatchelDatabase().Ember;
        Vector2 wallApproach = new(40, 88);
        var wallSeed = new EmberSeedEffect();
        wallSeed.Initialize(
            ember,
            transitionDestination,
            new BreakableTileDatabase(),
            wallApproach - shooter.Offsets[6],
            Vector2I.Left,
            _ => { },
            (_, _) => { },
            () => { },
            () => 0,
            _ => null,
            _saveData,
            4,
            launchKind: SeedLaunchKind.Shooter,
            angle: 6);
        var wallSeedSpawns = new List<RoomEntitySpawn>();
        wallSeed.UpdateFrame(1, wallSeedSpawns);
        for (int frame = 2;
             frame < 12 &&
             !transitionDestination.IsSolid(wallSeed.PrecisePosition);
             frame++)
        {
            wallSeed.UpdateFrame(frame, wallSeedSpawns);
        }
        Vector2 wallCollisionPosition = wallSeed.PrecisePosition;
        FailIf(
            !transitionDestination.IsSolid(wallCollisionPosition) ||
            wallSeed.Angle != 6 || wallSeed.BouncesRemaining != 3,
            "Room 4:4a's left arena wall did not receive the cardinal " +
            $"shooter seed at the source collision position " +
            $"(position={wallCollisionPosition}, angle={wallSeed.Angle}, " +
            $"bounces={wallSeed.BouncesRemaining}).");
        wallSeed.UpdateFrame(wallSeed.ElapsedFrames + 1, wallSeedSpawns);
        FailIf(
            wallSeed.State != EmberState.Flying || wallSeed.Angle != 2 ||
            wallSeed.BouncesRemaining != 2 ||
            wallSeed.PrecisePosition !=
                wallCollisionPosition + new Vector2(3, 0),
            "A cardinal shooter seed did not reflect right and run the " +
            "same-update objectApplySpeed after hitting room 4:4a's left " +
            $"wall (state={wallSeed.State}, angle={wallSeed.Angle}, " +
            $"bounces={wallSeed.BouncesRemaining}, " +
            $"position={wallSeed.PrecisePosition}).");
        wallSeed.UpdateFrame(wallSeed.ElapsedFrames + 1, wallSeedSpawns);
        FailIf(
            wallSeed.State != EmberState.Flying || wallSeed.Angle != 2 ||
            wallSeed.BouncesRemaining != 2 ||
            wallSeed.PrecisePosition !=
                wallCollisionPosition + new Vector2(6, 0),
            "Room 4:4a's reflected shooter seed remained embedded in the " +
            "wall and consumed another bounce instead of crossing the arena.");
        wallSeed.Free();

        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(4, 0x4a);
        _player.WarpTo(new Vector2(0x78, 0x58), recordSafe: false);
        ShadowHagBoss boss = _entities.Entities<ShadowHagBoss>().Single();
        DungeonRewardRoomEntity reward =
            _entities.Entities<DungeonRewardRoomEntity>().Single();
        FailIf(
            boss.Position != new Vector2(0xd8, 0x58) ||
            _entities.RoomEnemyCount != 1 || reward.Finished ||
            _entities.Entities<DungeonDoorRoomEntity>().Select(door =>
                (door.SubId, door.PackedPosition,
                 door.EnemyCompletionSupported)).ToArray() is not
                    [(0x0b, 0x50, true), (0x09, 0x5e, true)] ||
            _currentRoom.GetMetatile(Point(0x50)) != 0x7b ||
            _currentRoom.GetMetatile(Point(0x5e)) != 0x79 ||
            !_currentRoom.IsSolid(Point(0x50)) ||
            !_currentRoom.IsSolid(Point(0x5e)),
            "Room 4:4a did not create its counted ENEMY_SHADOW_HAG and " +
            "heart-container script with both supported enemy shutters in " +
            "source order.");

        for (int frame = 0;
             frame < 600 && boss.State != ShadowHagState.IntroDialogue;
             frame++)
        {
            Step();
        }
        FailIf(
            boss.State != ShadowHagState.IntroDialogue ||
            !_dialogue.IsOpen || !boss.Visible ||
            !_entities.LinkCollisionsAndMenuDisabled ||
            boss.Position.X >= 0x78 ||
            _sound.PlayRequestsFor(
                OracleSoundEngine.SndCtrlStopMusic) != 1,
            "Shadow Hag did not close room 4:4a, slide left from Link's " +
            "position, emerge, and open TX_2f2b with Link locked.");
        _dialogue.Close();
        for (int frame = 0;
             frame < 16 && boss.State == ShadowHagState.IntroDialogue;
             frame++)
        {
            Step();
        }
        FailIf(
            boss.State != ShadowHagState.GroundEyes || boss.IntroActive ||
            _entities.LinkCollisionsAndMenuDisabled ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusBoss) != 1,
            "Shadow Hag did not honor the post-dialogue eight-update delay, " +
            "start boss music, and restore Link.");

        for (int frame = 0;
             frame < 150 &&
             boss.State != ShadowHagState.ShadowsChasing;
             frame++)
        {
            Step();
        }
        List<ShadowHagShadowEffect> shadows =
            _entities.Entities<ShadowHagShadowEffect>();
        FailIf(
            boss.State != ShadowHagState.ShadowsChasing ||
            boss.Counter1 != 150 || boss.Counter2 != 4 ||
            boss.Visible || shadows.Count != 4 ||
            shadows.Select(shadow => shadow.Angle).Order().ToArray() is not
                [0x04, 0x0c, 0x14, 0x1c],
            "Shadow Hag did not flicker for 90 updates and create four " +
            "PART_SHADOW_HAG_SHADOW children in source angle order " +
            $"(state={boss.State}, c1={boss.Counter1}, c2={boss.Counter2}, " +
            $"visible={boss.Visible}, shadows={shadows.Count}, " +
            $"angles={string.Join(',', shadows.Select(shadow => shadow.Angle))}).");

        int randomCallsBeforeConvergence = _entities.RandomCalls;
        for (int frame = 0;
             frame < 900 && boss.State != ShadowHagState.SpawningBugs;
             frame++)
        {
            Step();
        }
        FailIf(
            boss.State != ShadowHagState.SpawningBugs ||
            _entities.Entities<ShadowHagShadowEffect>().Count != 0 ||
            _entities.RandomCalls < randomCallsBeforeConvergence + 2,
            "The four Shadow Hag parts did not chase on alternate updates, " +
            "reconverge, and consume the shared RNG for target/cycle count.");

        for (int frame = 0;
             frame < 80 && boss.State == ShadowHagState.SpawningBugs;
             frame++)
        {
            Step();
        }
        FailIf(
            boss.BugsAlive != 4 ||
            _entities.Entities<ShadowHagBug>().Count != 4 ||
            _entities.Entities<ShadowHagBug>().Any(bug =>
                bug.Record.Id != 0x42),
            "Shadow Hag did not spawn four uncounted $42 bugs at each " +
            "16-update boundary while retaining one room enemy count.");

        Vector2 behindOffset = _player.FacingVector == Vector2I.Up
            ? new Vector2(0, 0x40)
            : _player.FacingVector == Vector2I.Right
                ? new Vector2(-0x40, 8)
                : _player.FacingVector == Vector2I.Down
                    ? new Vector2(0, -0x40)
                    : new Vector2(0x40, 8);
        Vector2 chargeAnchor = (
            from y in Enumerable.Range(2, _currentRoom.HeightInTiles - 4)
            from x in Enumerable.Range(2, _currentRoom.WidthInTiles - 4)
            let link = new Vector2(x * 16 + 8, y * 16 + 8)
            let spawn = link + behindOffset
            where spawn.Y >= 0x1c && spawn.Y < 0x9c &&
                spawn.X >= 0 && spawn.X < 0xf0 &&
                _currentRoom.GetTerrainInfo(link).Collision == 0 &&
                _currentRoom.GetTerrainInfo(spawn).Collision == 0
            select link).First();
        _player.WarpTo(chargeAnchor, recordSafe: false);

        for (int frame = 0;
             frame < 300 && boss.State != ShadowHagState.ChargeTell;
             frame++)
        {
            _player.WarpTo(chargeAnchor, recordSafe: false);
            Step();
        }
        FailIf(
            boss.State != ShadowHagState.ChargeTell || !boss.Vulnerable ||
            !boss.Visible || _entities.RoomEnemyCount != 1,
            "Shadow Hag did not choose the source offset behind Link, emerge, " +
            "and enter its seed-vulnerable 30-update charge tell " +
            $"(state={boss.State}, c1={boss.Counter1}, c2={boss.Counter2}, " +
            $"pos={boss.Position}, visible={boss.Visible}, " +
            $"bugs={boss.BugsAlive}).");
        int health = boss.Health;
        FailIf(
            _entities.ApplySwordHit(
                boss.CollisionBounds.Grow(1), boss.Position, damage: 2) ||
            boss.Health != health,
            "Shadow Hag accepted sword damage despite collision mode $4b.");
        FailIf(
            !boss.TakeSeedHit(2) || boss.Health != health - 2 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBossDamage) != 1,
            "Shadow Hag rejected source seed damage during state $11.");

        Vector2I spawnFacing = _player.FacingVector;
        bool sawSecondShadows = false;
        bool sawSecondConvergence = false;
        for (int frame = 0; frame < 4000; frame++)
        {
            _player.WarpTo(chargeAnchor, recordSafe: false);
            if (boss.State == ShadowHagState.WaitingBehindLink)
                _player.Face(spawnFacing);
            else if (boss.State is ShadowHagState.ChargeTell or
                     ShadowHagState.Charging)
                FaceBoss(boss);
            Step();
            sawSecondShadows |= boss.State == ShadowHagState.ShadowsChasing;
            sawSecondConvergence |=
                sawSecondShadows &&
                boss.State == ShadowHagState.ShadowsConverging;
            if (sawSecondConvergence &&
                boss.State == ShadowHagState.BugSpawnDelay)
            {
                break;
            }
        }
        string secondShadowSummary = string.Join(
            ';',
            _entities.Entities<ShadowHagShadowEffect>().Select(shadow =>
                $"{shadow.State}@{shadow.Position}/${shadow.Angle:x2}"));
        FailIf(
            !sawSecondShadows || !sawSecondConvergence ||
            boss.State != ShadowHagState.BugSpawnDelay ||
            boss.Counter1 != 30 || !boss.Visible ||
            _entities.Entities<ShadowHagShadowEffect>().Count != 0,
            "Shadow Hag did not complete her second split/reassembly cycle " +
            $"(state={boss.State}, c1={boss.Counter1}, c2={boss.Counter2}, " +
            $"visible={boss.Visible}, shadows={secondShadowSummary}).");

        for (int frame = 0;
             frame < 1200 && boss.State != ShadowHagState.ChargeTell;
             frame++)
        {
            _player.WarpTo(chargeAnchor, recordSafe: false);
            if (boss.State == ShadowHagState.WaitingBehindLink)
                _player.Face(spawnFacing);
            Step();
        }
        FailIf(
            boss.State != ShadowHagState.ChargeTell || !boss.Vulnerable,
            "Shadow Hag did not resume her attack after the second " +
            $"reassembly (state={boss.State}, c1={boss.Counter1}, " +
            $"c2={boss.Counter2}).");
        boss.InvincibilityCounter = 0;
        FailIf(!boss.TakeSeedHit(0x7f),
            "Shadow Hag rejected a lethal seed hit during its vulnerable phase.");

        Step();
        FailIf(
            _entities.Entities<ShadowHagBug>().Count != 0 ||
            boss.BugsAlive != 0,
            "Shadow Hag's no-health handler did not kill every live $42 child.");
        Step(119);
        BossDeathExplosionEffect explosion =
            _entities.Entities<BossDeathExplosionEffect>().Single();
        FailIf(
            _entities.Entities<ShadowHagBoss>().Count != 0 ||
            explosion.BossId != 0x7a ||
            _saveData.HasRoomFlag(
                4, 0x4a, OracleSaveData.RoomFlag80) ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            !_entities.LinkCollisionsAndMenuDisabled,
            "Shadow Hag did not hand off to the counted 120-update boss " +
            "death and finite PART_BOSS_DEATH_EXPLOSION sequence.");
        _sound.ClearPlayRequestAudit();
        Step(80);
        FailIf(
            !_saveData.HasRoomFlag(
                4, 0x4a, OracleSaveData.RoomFlag80) ||
            _entities.Entities<GroundTreasurePickup>() is not
                [{ Record: { TreasureObject:
                    "TREASURE_OBJECT_HEART_CONTAINER_00" } }] ||
            _entities.LinkCollisionsAndMenuDisabled ||
            _entities.RoomEnemyCount != 0 ||
            !_currentRoom.IsSolid(Point(0x50)) ||
            !_currentRoom.IsSolid(Point(0x5e)) ||
            _sound.PlayRequestsFor(mechanics.SolveSound) != 2,
            "Room 4:4a did not persist flag $80 and spawn its Heart " +
            "Container and begin both enemy-shutter solve delays when the " +
            "boss explosion released the enemy count.");
        Step(mechanics.SolveWait);
        FailIf(
            _currentRoom.GetMetatile(Point(0x50)) != 0x7b ||
            _currentRoom.GetMetatile(Point(0x5e)) != 0x79,
            "Room 4:4a changed either shutter before the post-solve ready " +
            "update.");
        Step();
        FailIf(
            _currentRoom.GetMetatile(Point(0x50)) != 0xa0 ||
            _currentRoom.GetMetatile(Point(0x5e)) != 0xa0 ||
            !_currentRoom.IsSolid(Point(0x50)) ||
            !_currentRoom.IsSolid(Point(0x5e)),
            "Room 4:4a did not begin both interleaved shutter openings " +
            "while retaining collision.");
        Step(mechanics.DoorFrameWait);
        FailIf(
            _currentRoom.IsSolid(Point(0x50)) ||
            _currentRoom.IsSolid(Point(0x5e)) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0,
            "Room 4:4a did not finish both enemy shutters on the exact " +
            "six-update interleaving boundary.");

        _saveData.SetRoomFlag(
            4, 0x4a, OracleSaveData.RoomFlagItem, value: true);
        LoadValidationRoom(4, 0x4a);
        FailIf(
            _entities.Entities<ShadowHagBoss>().Count != 0 ||
            _entities.Entities<DungeonRewardRoomEntity>().Count != 0,
            "Completed room 4:4a did not suppress its BeforeEvent boss and " +
            "$20:$01 reward after the item flag was persisted.");

        GD.Print(
            "Validated room 4:4a Shadow Hag: source order/visuals, transition " +
            "preload, arena-wall shooter ricochet, intro, four shadows, " +
            "shared RNG, bugs, behind-Link charge, repeated reassembly, seed " +
            "vulnerability, death, enemy-shutter opening, Heart Container, " +
            "and re-entry.");
    }

    private void ValidateRoom44dSubterrorMiniboss()
    {
        const double Update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        static bool IsGbcColor(Color color, int red, int green, int blue)
        {
            const float TextureTolerance = 1.5f / 255.0f;
            return Mathf.Abs(color.R - red / 31.0f) <= TextureTolerance &&
                Mathf.Abs(color.G - green / 31.0f) <= TextureTolerance &&
                Mathf.Abs(color.B - blue / 31.0f) <= TextureTolerance;
        }
        void Step(int count = 1)
        {
            for (int index = 0; index < count; index++)
                _entities.Update(Update, _player);
        }

        var data = new MoonlitGrottoDatabase();
        var visuals = new DungeonInteractionVisualDatabase();
        var bosses = new DungeonBossDatabase();
        DungeonInteractionVisual dirtVisual = visuals.Visual("subterror-dirt");
        AnimationDefinition dirtAnimation =
            OracleGraphicsCache.GetAnimationDefinition(
                dirtVisual.Animations.Single());
        FailIf(
            dirtAnimation.Frames.Select(frame =>
                (frame.Duration, frame.Parameter)).ToArray() is not
                [(3, 0x83), (6, 0x82), (6, 0x83), (6, 0x83), (1, 0)] ||
            bosses.SubterrorPalettes is not { Count: 1 } ||
            !bosses.SubterrorPalettes.ContainsKey(6) ||
            !IsGbcColor(
                bosses.SubterrorPalettes[6][0], 0x1f, 0x1f, 0x1f) ||
            !IsGbcColor(
                bosses.SubterrorPalettes[6][1], 0x1b, 0x14, 0x00) ||
            !IsGbcColor(
                bosses.SubterrorPalettes[6][2], 0x15, 0x0c, 0x00) ||
            !IsGbcColor(
                bosses.SubterrorPalettes[6][3], 0x0b, 0x06, 0x00),
            "PART_SUBTERROR_DIRT lost its full five-frame animation or " +
            "PALH_be paletteData4950 override for OBJ palette 6.");
        Texture2D expectedDirtTexture =
            NpcCharacter.BuildOamTextureWithPaletteOverrides(
                EnemyVisualSource.LoadComposite(dirtVisual.Sprites),
                dirtAnimation.Frames[0].EncodedOam,
                dirtVisual.TileBase,
                dirtVisual.Palette,
                bosses.SubterrorPalettes,
                dirtVisual.SourceGrayscaleInverted);
        ulong expectedDirtHash;
        using (Image expectedDirtImage = expectedDirtTexture.GetImage())
            expectedDirtHash = OracleGraphicsCache.PixelHash(expectedDirtImage);
        IReadOnlyList<DungeonObjectRecord> records =
            data.GetRoomRecords(4, 0x4d);
        FailIf(
            records.Select(record =>
                (record.Order, record.Kind, record.Id, record.SubId,
                 record.Y, record.X)).ToArray() is not
                [
                    (3, DungeonObjectKind.MinibossReward, 0x20, 0x00,
                        0x58, 0x78),
                    (4, DungeonObjectKind.Subterror, 0x72, 0x00,
                        0x18, 0x78)
                ] ||
            string.IsNullOrWhiteSpace(data.SubterrorMessage),
            "Room 4:4d lost its source-ordered $20:$00 reward, " +
            "$72:$00 BeforeEvent boss, or TX_2f03 text.");

        _saveData.SetRoomFlag(
            4, 0x4d, OracleSaveData.RoomFlag80, value: false);
        _sound.ClearPlayRequestAudit();
        OracleRoomData transitionSource = _world.LoadRoom(4, 0x4c);
        OracleRoomData transitionDestination = _world.LoadRoom(4, 0x4d);
        _entities.LoadRoom(4, transitionSource);
        Vector2 incomingOffset = Vector2.Left * transitionDestination.Width;
        _entities.BeginScreenTransition(
            4, transitionDestination, incomingOffset);
        SubterrorBoss preloadedBoss =
            _entities.Entities<SubterrorBoss>().Single();
        FailIf(
            preloadedBoss.Visible || preloadedBoss.Counter2 != 30 ||
            preloadedBoss.DirtCounter != 7 || preloadedBoss.Speed != 0x3c ||
            preloadedBoss.TransitionDrawOffset != incomingOffset ||
            _sound.PlayRequestsFor(
                OracleSoundEngine.SndCtrlStopMusic) != 1,
            "Incoming room 4:4d did not resolve Subterror's hidden source " +
            "state 0 before exposing the scrolling destination.");
        _entities.Update(1.0, _player);
        FailIf(
            preloadedBoss.Counter2 != 30 || preloadedBoss.DirtCounter != 7 ||
            preloadedBoss.State != SubterrorState.WaitingForDoors,
            "Incoming room 4:4d advanced Subterror while destination " +
            "entities were frozen during scrolling.");
        _entities.FinishScreenTransition();

        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(4, 0x4d);
        SubterrorBoss boss = _entities.Entities<SubterrorBoss>().Single();
        DungeonRewardRoomEntity reward =
            _entities.Entities<DungeonRewardRoomEntity>().Single();
        FailIf(
            boss.Record is not
                {
                    Id: 0x72, Health: 20, DamageQuarters: 2,
                    RadiusY: 6, RadiusX: 6, Palette: 1,
                    Sprites.Length: 3
                } ||
            boss.Position != new Vector2(0x78, 0x18) ||
            _entities.RoomEnemyCount != 1 || reward.Finished,
            "Room 4:4d did not create imported ENEMY_SUBTERROR $72 and " +
            "its counted miniboss-death controller.");

        ulong? observedDirtHash = null;
        for (int frame = 0;
             frame < 360 &&
             !(boss.State == SubterrorState.WaitingForDoors &&
               boss.Substate == 3);
             frame++)
        {
            Step();
            if (!observedDirtHash.HasValue &&
                _entities.Entities<SubterrorDirtEffect>().FirstOrDefault() is
                    { } dirt)
            {
                using Image image = dirt.CurrentAnimationTexture.GetImage();
                observedDirtHash = OracleGraphicsCache.PixelHash(image);
            }
        }
        FailIf(
            boss.State != SubterrorState.WaitingForDoors ||
            boss.Substate != 3 || boss.Position.Y != 0x58 + 0x80 / 256.0f ||
            !_dialogue.IsOpen ||
            !_entities.LinkCollisionsAndMenuDisabled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDig) == 0 ||
            observedDirtHash != expectedDirtHash ||
            _entities.Entities<SubterrorDirtEffect>().Count != 0,
            "Subterror did not close room 4:4d, trail finite brown " +
            "PART_SUBTERROR_DIRT effects, emerge at Y=$58, bounce, and open " +
            "TX_2f03 with Link locked.");

        _dialogue.Close();
        Step();
        FailIf(
            boss.State != SubterrorState.Digging || boss.IntroActive ||
            _entities.LinkCollisionsAndMenuDisabled ||
            _entities.PlayerMenusDisabled ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusMiniboss) != 1,
            "Subterror did not begin the fight and restore Link after TX_2f03.");

        for (int frame = 0;
             frame < 360 && boss.State != SubterrorState.Drilling;
             frame++)
        {
            _player.WarpTo(boss.Position, recordSafe: false);
            Step();
        }
        FailIf(
            boss.State != SubterrorState.Drilling || boss.Counter2 != 30 ||
            boss.Visible || boss.DrillingCollisionEnabled,
            "Subterror did not use its health-$14 120-update pursuit timer " +
            "before teleporting to Link for a hidden 30-update drill tell.");
        Step(29);
        FailIf(
            boss.Counter2 != 1 || boss.Visible ||
            boss.DrillingCollisionEnabled,
            "Subterror became visible or collidable before drill tell " +
            "counter2 reached zero.");
        int shockSounds = _sound.PlayRequestsFor(OracleSoundEngine.SndShock);
        Step();
        FailIf(
            boss.Counter2 != 0 || !boss.Visible ||
            !boss.DrillingCollisionEnabled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndShock) !=
                shockSounds + 1,
            "Subterror did not expose its damaging drill and request " +
            "SND_SHOCK on the exact counter2-zero update.");

        for (int frame = 0;
             frame < 240 && !boss.ShovelCollisionEnabled;
             frame++)
        {
            Step();
        }
        FailIf(
            !boss.ShovelCollisionEnabled || boss.Visible ||
            boss.State != SubterrorState.UndergroundMoving,
            "Subterror did not return to its invisible shovel-only " +
            "underground collision mode after drilling.");
        Vector2 shovelPosition = boss.Position;
        FailIf(
            !_entities.ApplyShovelHit(
                boss.CollisionBounds.Grow(1), shovelPosition) ||
            boss.State != SubterrorState.AboveGround ||
            boss.Substate != 0 || boss.ZFixed != -0x100 ||
            boss.Speed != 0x28 || !boss.Visible,
            "ITEM_SHOVEL did not launch Subterror into state $0c with " +
            "-$0100 Z speed and SPEED_100 movement away from the hit.");

        for (int frame = 0;
             frame < 240 && !boss.Vulnerable;
             frame++)
        {
            Step();
        }
        FailIf(
            !boss.Vulnerable || boss.Substate != 1 ||
            boss.Counter1 != 180,
            "Subterror did not complete its source two-bounce emergence " +
            "and enter the 180-update vulnerable wait.");
        int health = boss.Health;
        FailIf(
            !_entities.ApplySwordHit(
                boss.CollisionBounds.Grow(1), boss.Position, damage: 2) ||
            boss.Health != health - 2 || boss.Substate != 1,
            "Subterror rejected an ordinary sword hit during its vulnerable " +
            "above-ground state.");
        Step();
        FailIf(
            boss.Substate != 2 ||
            boss.Counter1 is not (60 or 90 or 120 or 180),
            "A successful hit did not immediately advance Subterror to its " +
            "two-call shared-RNG movement phase.");

        boss.InvincibilityCounter = 0;
        FailIf(!boss.TakeSwordHit(boss.Position, damage: 0x7f),
            "Subterror rejected a lethal vulnerable validation hit.");
        Step(120);
        BossDeathExplosionEffect explosion =
            _entities.Entities<BossDeathExplosionEffect>().Single();
        FailIf(
            _entities.Entities<SubterrorBoss>().Count != 0 ||
            explosion.BossId != 0x72 ||
            !_entities.LinkCollisionsAndMenuDisabled ||
            _saveData.HasRoomFlag(
                4, 0x4d, OracleSaveData.RoomFlag80),
            "Subterror did not hand off to the counted 120-update boss " +
            "death / PART_BOSS_DEATH_EXPLOSION sequence.");
        Step(79);
        Step();
        FailIf(
            !_saveData.HasRoomFlag(
                4, 0x4d, OracleSaveData.RoomFlag80) ||
            _entities.Entities<MinibossPortal>().Count != 0,
            "Room 4:4d did not persist flag $80 and begin the standard " +
            "20-update miniboss portal wait after the explosion released " +
            "its enemy count.");
        Step(19);
        FailIf(_entities.Entities<MinibossPortal>().Count != 0,
            "Room 4:4d created its miniboss portal before update 20.");
        Step();
        FailIf(
            _entities.Entities<MinibossPortal>().Count != 1 ||
            _entities.LinkCollisionsAndMenuDisabled,
            "Room 4:4d did not create its paired portal and restore Link on " +
            "the exact reward-wait boundary.");

        LoadValidationRoom(4, 0x4d);
        FailIf(
            _entities.Entities<SubterrorBoss>().Count != 0 ||
            _entities.Entities<DungeonRewardRoomEntity>().Count != 0,
            "Completed room 4:4d did not suppress its BeforeEvent boss and " +
            "$20:$00 reward controller on re-entry.");

        GD.Print(
            "Validated room 4:4d Subterror: imported source order/visuals, " +
            "intro, dirt tiles, 120/30 drill timing, shovel emergence, " +
            "two-bounce vulnerability, shared RNG, death, portal, and re-entry.");
    }

    private void ValidateRoom44bMoonlitGrottoInteractions()
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
            data.GetRoomRecords(4, 0x4b);
        RoomTileChangeWatcherDatabaseRecord watcherRecord =
            new RoomTileChangeWatcherDatabase().GetRoomRecords(4, 0x4b).Single();
        FailIf(
            records.Select(record =>
                (record.Order, record.Id, record.SubId,
                 record.PackedPosition)).ToArray() is not
                [(0, 0x13, 0x01, 0x6b), (1, 0x12, 0x01, 0x58)] ||
            watcherRecord is not { Order: 2, Position: 0x54, RoomFlag: 0x80 },
            "Room 4:4b lost its source-ordered $13:$01 push trigger, " +
            "$12:$01 falling key, or $dc:$08 tile-$54/flag-$80 watcher.");

        _saveData.SetRoomFlag(
            4, 0x4b, OracleSaveData.RoomFlagItem, value: false);
        _saveData.SetRoomFlag(
            4, 0x4b, OracleSaveData.RoomFlag80, value: false);
        LoadValidationRoom(4, 0x4b);
        OracleRoomData room = _currentRoom;
        PushBlockTriggerRoomEntity trigger =
            _entities.Entities<PushBlockTriggerRoomEntity>().Single();
        DungeonRewardRoomEntity keyController =
            _entities.Entities<DungeonRewardRoomEntity>().Single();
        bool WatcherPresent() => _entities.Entities<Node2D>().Any(
            node => node.Name == "TileChangeWatcher_2");
        List<MoldormCharacter> moldorms =
            _entities.Entities<MoldormCharacter>();
        FailIf(
            room.ActiveCollisions != 2 || room.Width != 240 || room.Height != 176 ||
            trigger.PackedPosition != 0x6b || trigger.CountsAsEnemy ||
            moldorms.Count != 2 || _entities.RoomEnemyCount != 2 ||
            room.GetMetatile(Point(0x6b)) != 0x1b ||
            room.GetMetatile(Point(0x54)) != 0x19 ||
            room.GetMetatile(Point(0x55)) != 0xa0 ||
            !WatcherPresent() ||
            _entities.Entities<ItemDropProducer>().Select(value => value.Position)
                .ToArray() is not
                [{ X: 0x18, Y: 0x18 }, { X: 0x28, Y: 0x18 },
                 { X: 0x18, Y: 0x28 }, { X: 0x28, Y: 0x28 }] ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room 4:4b did not load its two counted Moldorms, four uncounted " +
            "drop producers, directional blocks, and three placed interactions.");

        Step();
        FailIf(
            room.GetMetatile(Point(0x6b)) != data.PushableBlock ||
            !trigger.CountsAsEnemy || _entities.RoomEnemyCount != 3 ||
            _saveData.HasRoomFlag(4, 0x4b, OracleSaveData.RoomFlag80) ||
            keyController.Finished,
            "$13:$01 did not temporarily replace room 4:4b/$6b with $1d " +
            "and add itself to wNumEnemies before $12:$01 checked the count.");

        foreach (MoldormCharacter moldorm in moldorms)
        {
            FailIf(!moldorm.TakeSwordHit(moldorm.Position, damage: 0x7f),
                "A room 4:4b Moldorm rejected a lethal validation hit.");
        }
        Step();
        FailIf(
            _entities.Entities<MoldormCharacter>().Count != 0 ||
            _entities.RoomEnemyCount != 1 ||
            room.GetMetatile(Point(0x6b)) != 0x1b ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "$13:$01 did not restore the source left-only block when only its " +
            "synthetic enemy count remained, or $12:$01 spawned early.");

        void PushBlock(int sourcePacked, Vector2I direction, int goalPacked)
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
                !_pushBlocks.Active || room.GetMetatile(source) != 0xa0,
                $"Room 4:4b block ${sourcePacked:x2} did not begin its " +
                $"source-direction push toward ${goalPacked:x2}.");
            for (int frame = 0;
                 frame < PushBlockController.MoveFrames - 1;
                 frame++)
            {
                _pushBlocks.Advance(Update);
            }
            FailIf(
                !_pushBlocks.Active ||
                room.GetMetatile(Point(goalPacked)) != 0xa0,
                $"Room 4:4b block ${sourcePacked:x2} completed before " +
                "SPEED_80 update 32.");
            _pushBlocks.Advance(Update);
            FailIf(
                _pushBlocks.Active ||
                room.GetMetatile(Point(goalPacked)) != data.PushableBlock,
                $"Room 4:4b block ${sourcePacked:x2} did not write $1d at " +
                $"goal ${goalPacked:x2} on movement update 32.");
        }

        PushBlock(0x6b, Vector2I.Left, 0x6a);
        Step();
        Step(data.PushDelay - 1);
        FailIf(
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1 ||
            _entities.RoomEnemyCount != 1 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room 4:4b released the push trigger or falling key before the " +
            "source $1e counter's 30th decrement.");
        Step();
        GroundTreasurePickup fallingKey =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 0 ||
            _entities.Entities<DungeonRewardRoomEntity>().Count != 0 ||
            _entities.RoomEnemyCount != 0 ||
            fallingKey.Position != new Vector2(0x88, 0x58) ||
            fallingKey.Record.TreasureObject != "TREASURE_OBJECT_SMALL_KEY_01" ||
            fallingKey.Record.SpawnMode != 2 || fallingKey.Record.GrabMode != 2 ||
            fallingKey.Record.SpawnDelayFrames != 40 ||
            fallingKey.Record.BounceCount != 2 || fallingKey.Record.Gravity != 0x10 ||
            fallingKey.Record.BounceSpeed != -0xaa ||
            !fallingKey.Record.InitialZAboveScreen,
            "$13:$01 did not clear the synthetic count on update 30 so the " +
            "following $12:$01 slot could create its exact falling small key.");

        for (int frame = 0;
             frame < 300 && fallingKey.State != PickupState.Waiting;
             frame++)
        {
            Step();
        }
        int dungeon = _rooms.CurrentDungeonIndex;
        int keysBeforePickup = _inventory.GetDungeonSmallKeys(dungeon);
        _player.WarpTo(fallingKey.Position, recordSafe: false);
        Step();
        FailIf(
            fallingKey.State != PickupState.Collected ||
            _inventory.GetDungeonSmallKeys(dungeon) != keysBeforePickup + 1 ||
            !_saveData.HasRoomFlag(
                4, 0x4b, OracleSaveData.RoomFlagItem),
            "Room 4:4b's falling key did not grant one dungeon-$03 small key " +
            "and persist ROOMFLAG_ITEM.");

        PushBlock(0x54, Vector2I.Right, 0x55);
        FailIf(_saveData.HasRoomFlag(
                4, 0x4b, OracleSaveData.RoomFlag80),
            "$dc:$08 wrote room 4:4b flag $80 outside its interaction update.");
        Step();
        FailIf(
            !_saveData.HasRoomFlag(
                4, 0x4b, OracleSaveData.RoomFlag80) ||
            WatcherPresent(),
            "$dc:$08 did not persist the changed tile-$54 block with room " +
            "flag $80 on its next source update.");

        LoadValidationRoom(4, 0x4b);
        room = _currentRoom;
        FailIf(
            room.GetMetatile(Point(0x54)) != 0xa0 ||
            room.GetMetatile(Point(0x55)) != data.PushableBlock ||
            _entities.Entities<DungeonRewardRoomEntity>().Count != 0 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            !WatcherPresent(),
            "Room 4:4b re-entry did not apply the flag-$80 single-tile " +
            "changes or suppress the collected $12:$01 key controller.");
        Step();
        FailIf(
            WatcherPresent() ||
            room.GetMetatile(Point(0x6b)) != data.PushableBlock,
            "Room 4:4b's completed $dc:$08 watcher did not delete before " +
            "$13:$01 re-armed its enemy-gated block on re-entry.");

        GD.Print(
            "Validated full room 4:4b interactions: source-ordered $13:$01 " +
            "synthetic enemy count and block restore, $1e release wait, " +
            "$12:$01 falling small key/item flag, $dc:$08 tile watcher, " +
            "flag-$80 single-tile persistence, and completed re-entry.");
    }

    private void ValidateRoom44eMoonlitGrotto()
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
            data.GetRoomRecords(4, 0x4e);
        FailIf(
            records.Count != 8 ||
            records.Select(record => (record.Order, record.Id, record.SubId,
                record.PackedPosition, record.Parameter)).ToArray() is not
            [
                (0, 0x23, 0x01, 0x39, 0x02),
                (1, 0x23, 0x01, 0x42, 0x03),
                (2, 0x23, 0x01, 0x4c, 0x04),
                (3, 0x03, 0x02, 0x31, 0x00),
                (4, 0x03, 0x03, 0x3d, 0x00),
                (5, 0x05, 0x02, 0x68, 0x00),
                (6, 0x33, 0x0a, 0x18, 0x0c),
                (7, 0xc7, 0x04, 0x0f, 0x16)
            ],
            "Room 4:4e lost its eight source-ordered bridge/orb/switch/" +
            "seed-bouncer/respawnable-bush objects.");

        _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
        _runtimeState.SetWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(4, 0x4e);
        _sound.ClearPlayRequestAudit();
        OracleRoomData room = _currentRoom;
        List<ExtendableBridgeRoomEntity> bridges =
            _entities.Entities<ExtendableBridgeRoomEntity>();
        List<DungeonOrbRoomEntity> orbs =
            _entities.Entities<DungeonOrbRoomEntity>();
        DungeonSwitchRoomEntity roomSwitch =
            _entities.Entities<DungeonSwitchRoomEntity>().Single();
        RotatableSeedThingRoomEntity bouncer =
            _entities.Entities<RotatableSeedThingRoomEntity>().Single();
        byte[] bouncerBackground =
        [
            room.GetBackgroundSubtileForValidation(16, 2),
            room.GetBackgroundSubtileForValidation(17, 2),
            room.GetBackgroundSubtileForValidation(16, 3),
            room.GetBackgroundSubtileForValidation(17, 3)
        ];
        byte[] switchBackground =
        [
            room.GetBackgroundSubtileForValidation(16, 12),
            room.GetBackgroundSubtileForValidation(17, 12),
            room.GetBackgroundSubtileForValidation(16, 13),
            room.GetBackgroundSubtileForValidation(17, 13)
        ];
        FailIf(
            bridges.Select(bridge =>
                (bridge.PackedPosition, bridge.PatternVariant)).ToArray() is not
                [(0x39, 2), (0x42, 3), (0x4c, 4)] ||
            orbs.Select(orb => (room.GetPackedPosition(orb.Position),
                orb.ToggleMask)).ToArray() is not [(0x31, 0x04), (0x3d, 0x08)] ||
            roomSwitch is not { PackedPosition: 0x68, SwitchMask: 0x02 } ||
            bouncer.Position != Point(0x18) || bouncer.ToggleMask != 0x0c ||
            bouncer.Orientation != 2 ||
            room.GetMetatile(Point(0x18)) != data.SeedBouncerBackgroundTile ||
            bouncerBackground.SequenceEqual(switchBackground) ||
            data.SeedBouncerChildY != 12 ||
            data.SeedBouncerChildX != 0 ||
            data.SeedBouncerChildZ != -14 ||
            !room.IsSolid(Point(0x18) + Vector2.Left * 4) ||
            !room.IsSolid(Point(0x18) + Vector2.Right * 4) ||
            _entities.Entities<RespawnableBushScannerRoomEntity>().Count != 1 ||
            _entities.Entities<RespawnableBushRoomEntity>().Count != 0,
            "Room 4:4e did not instantiate the placed mechanisms before the " +
            "`$c7:$04 scanner's first interaction update, or " +
            "PART_ROTATABLE_SEED_THING redrew logical tile `$0a as the " +
            "visible switch graphic beneath itself.");

        Step();
        List<RespawnableBushRoomEntity> bushes =
            _entities.Entities<RespawnableBushRoomEntity>();
        FailIf(
            _entities.Entities<RespawnableBushScannerRoomEntity>().Count != 0 ||
            bushes.Select(bush =>
                (bush.PackedPosition, bush.DropSubId)).ToArray() is not
                [(0x11, 0x06), (0x12, 0x06), (0x8a, 0x06), (0x9a, 0x06)] ||
            bridges.Select(bridge => bridge.BridgePresent).ToArray() is not
                [false, true, true],
            "Room 4:4e's scanner did not create four initialized Scent Seed " +
            "bushes in layout order or the bridges misread their source tiles.");

        SeedShooterRecord shooter = SeedShooterRecord.Load();
        SeedRecord ember = new SeedSatchelDatabase().Ember;
        EmberSeedEffect SpawnShooterSeed(Vector2 position, int angle = 1) =>
            _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                position - shooter.Offsets[angle],
                Vector2I.Up,
                ember,
                4,
                SeedLaunchKind.Shooter,
                angle));

        // checkObjectsCollidedFromVariables accepts item - part == -sum but
        // rejects +sum. At orientation 2, the left-edge center is ten pixels
        // from the parent ($04 seed radius + $06 part radius). Missing that
        // exact edge lets seedItemCheckDiagonalCollision see the solid tile
        // and produce wall angle 5 instead of reflector angle 1.
        Vector2 leftEdge = bouncer.Position + Vector2.Left * 10;
        EmberSeedEffect lenientSeed = SpawnShooterSeed(leftEdge, angle: 3);
        Step();
        FailIf(
            lenientSeed.State != EmberState.Flying ||
            lenientSeed.PrecisePosition != leftEdge ||
            !bouncer.IntersectsSeed(lenientSeed.CollisionBounds) ||
            bouncer.IntersectsSeed(new Rect2(
                bouncer.Position + Vector2.Right * 10 - Vector2.One * 4,
                Vector2.One * 8)),
            "PART_ROTATABLE_SEED_THING $33:$0a lost the source asymmetric " +
            "[-sum,+sum) collision boundary for a shooter seed in room 4:4e.");
        Step();
        FailIf(
            lenientSeed.State != EmberState.Flying ||
            lenientSeed.Angle != 1 ||
            lenientSeed.BouncesRemaining != 2 ||
            lenientSeed.PrecisePosition == leftEdge,
            "Room 4:4e's valid left-edge bouncer hit fell through to the " +
            "nearby solid-tile bounce (expected reflector angle 1, not wall " +
            $"angle 5; actual={lenientSeed.Angle}).");
        lenientSeed.OnCollision(SeedHitResult.Consume);
        Step();

        EmberSeedEffect downwardSeed = SpawnShooterSeed(
            bouncer.Position + Vector2.Left * 4,
            angle: 0);
        Step();
        Step();
        FailIf(
            downwardSeed.Angle != 4 ||
            downwardSeed.BouncesRemaining != 2 ||
            downwardSeed.PrecisePosition !=
                bouncer.Position + new Vector2(-4, 3),
            "PART_ROTATABLE_SEED_THING $33:$0a did not reflect the room " +
            "4:4e seed downward before its source item-passable background " +
            "tile `$0a check.");
        Step();
        FailIf(
            downwardSeed.State != EmberState.Flying ||
            downwardSeed.Angle != 4 ||
            downwardSeed.BouncesRemaining != 2 ||
            downwardSeed.PrecisePosition !=
                bouncer.Position + new Vector2(-4, 6) ||
            !shooter.CanPassSolidTile(room, downwardSeed.PrecisePosition),
            "Room 4:4e's valid downward seed reflection bounced off the " +
            "bouncer's background instead of passing through source tile " +
            "`$0a with collision `$0f.");
        downwardSeed.OnCollision(SeedHitResult.Consume);
        Step();

        Rect2 childOnlyBounds = new(
            bouncer.Position + new Vector2(-4, 8),
            Vector2.One * 8);
        var ignoredSpawns = new List<RoomEntitySpawn>();
        FailIf(
            bouncer.IntersectsSeed(childOnlyBounds) ||
            bouncer.ApplySeedHitAtHeight(
                childOnlyBounds,
                childOnlyBounds.GetCenter(),
                sourceZ: 0,
                ember.SeedItem,
                ignoredSpawns) != SeedHitResult.None ||
            bouncer.ApplySeedHitAtHeight(
                childOnlyBounds,
                childOnlyBounds.GetCenter(),
                sourceZ: -14,
                ember.SeedItem,
                ignoredSpawns) != SeedHitResult.Bounce,
            "PART_ROTATABLE_SEED_THING's `$03 child at Y+$0c/Z=$f2 " +
            "incorrectly extended the ground-level shooter collision below " +
            "the visible parent in room 4:4e.");

        EmberSeedEffect centeredBounceSeed = SpawnShooterSeed(
            bouncer.Position + Vector2.Down * 13,
            angle: 0);
        Step();
        Step();
        FailIf(
            centeredBounceSeed.Angle != 0 ||
            centeredBounceSeed.PrecisePosition !=
                bouncer.Position + Vector2.Down * 10,
            "A ground-level shooter seed reflected from the source `$03 " +
            "child instead of continuing toward the visible bouncer parent.");
        Step();
        FailIf(
            centeredBounceSeed.Angle != 0 ||
            centeredBounceSeed.PrecisePosition !=
                bouncer.Position + Vector2.Down * 7,
            "The shooter seed did not enter the visible parent collision " +
            "before the room 4:4e reflection.");
        Step();
        FailIf(
            centeredBounceSeed.Angle != 4 ||
            centeredBounceSeed.BouncesRemaining != 2 ||
            centeredBounceSeed.PrecisePosition !=
                bouncer.Position + Vector2.Down * 10,
            "Room 4:4e reflected a ground-level shooter seed away from the " +
            "invisible child rather than the visible bouncer parent.");
        centeredBounceSeed.OnCollision(SeedHitResult.Consume);
        Step();

        EmberSeedEffect firstOrbSeed = SpawnShooterSeed(
            orbs[0].Position + new Vector2(12, 0), angle: 6);
        Step();
        FailIf(
            firstOrbSeed.State != EmberState.Flying ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.ToggleBlocksStateAddress) != 0,
            "A newly allocated shooter seed collided with PART_ORB during " +
            "its setup-only update.");
        int firstOrbFlightUpdates = 0;
        while ((_runtimeState.ReadWramByte(
                   OracleRuntimeState.ToggleBlocksStateAddress) & 0x04) == 0 &&
               firstOrbFlightUpdates < 6)
        {
            Step();
            firstOrbFlightUpdates++;
        }
        FailIf(
            firstOrbFlightUpdates != 3 ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.ToggleBlocksStateAddress) != 0x04 ||
            orbs[0].Palette != 2 || orbs[0].HitLockout != 0 ||
            firstOrbSeed.State != EmberState.Burning ||
            firstOrbSeed.CollisionEnabled || bouncer.Orientation != 3 ||
            _sound.PlayRequestsFor(0x7e) != 1,
            "A shooter-fired Ember Seed did not reach PART_ORB before solid-" +
            "tile handling, XOR bit `$04, activate on collision, and rotate " +
            "the seed bouncer without sword-style invincibility " +
            $"(flight={firstOrbFlightUpdates}, toggle=${_runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress):x2}, " +
            $"palette={orbs[0].Palette}, lockout={orbs[0].HitLockout}, " +
            $"seed={firstOrbSeed.State}/{firstOrbSeed.PrecisePosition}, " +
            $"bouncer={bouncer.Orientation}, switchSounds={_sound.PlayRequestsFor(0x7e)}).");

        EmberSeedEffect secondOrbSeed = SpawnShooterSeed(
            orbs[1].Position - new Vector2(12, 0), angle: 2);
        Step();
        int secondOrbFlightUpdates = 0;
        while ((_runtimeState.ReadWramByte(
                   OracleRuntimeState.ToggleBlocksStateAddress) & 0x08) == 0 &&
               secondOrbFlightUpdates < 6)
        {
            Step();
            secondOrbFlightUpdates++;
        }
        FailIf(
            secondOrbFlightUpdates != 3 ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.ToggleBlocksStateAddress) != 0x0c ||
            orbs[1].Palette != 2 || orbs[1].HitLockout != 0 ||
            secondOrbSeed.State != EmberState.Burning ||
            secondOrbSeed.CollisionEnabled || bouncer.Orientation != 0 ||
            _sound.PlayRequestsFor(0x7e) != 2,
            "The second shooter seed did not toggle room 4:4e orb bit `$08, " +
            "rotate the seed bouncer, and play SND_SWITCH once.");

        Vector2 childBouncePoint = bouncer.Position + new Vector2(0, 12);
        EmberSeedEffect reflectedSeed = SpawnShooterSeed(bouncer.Position);
        Step();
        FailIf(
            reflectedSeed.State != EmberState.Flying ||
            reflectedSeed.PrecisePosition != bouncer.Position ||
            !bouncer.IntersectsSeed(reflectedSeed.CollisionBounds) ||
            bouncer.IntersectsSeed(new Rect2(
                childBouncePoint - Vector2.One,
                Vector2.One * 2)) ||
            bouncer.SeedBounceOrientation != 0 ||
            bouncer.CollisionRadii != new Vector2(4, 6),
            "The `$33:$0a parent did not expose its source `$06/$04 " +
            "orientation-0 collision rectangle, or its Z=$f2 child leaked " +
            "into the ground-level shooter target.");
        Step();
        FailIf(
            reflectedSeed.State != EmberState.Flying ||
            reflectedSeed.Angle != 7 ||
            reflectedSeed.BouncesRemaining != 2,
            "COLLISIONEFFECT_2a/func_50f4 did not reflect shooter angle 1 " +
            "to angle 7, consume one bounce, and bypass same-update terrain " +
            "collision from animation parameter 0.");
        reflectedSeed.OnCollision(SeedHitResult.Consume);
        Step();

        _sound.ClearPlayRequestAudit();
        EmberSeedEffect switchSeed = SpawnShooterSeed(
            roomSwitch.Position - new Vector2(0, 12), angle: 4);
        Step();
        int switchFlightUpdates = 0;
        while (_runtimeState.ReadWramByte(
                   OracleRuntimeState.SwitchStateAddress) == 0 &&
               switchFlightUpdates < 6)
        {
            Step();
            switchFlightUpdates++;
        }
        FailIf(
            switchFlightUpdates != 3 ||
            _runtimeState.ReadWramByte(
                OracleRuntimeState.SwitchStateAddress) != 0x02 ||
            room.GetMetatile(Point(0x68)) != data.SwitchOnTile ||
            roomSwitch.HitLockout != 0 ||
            switchSeed.State != EmberState.Burning ||
            switchSeed.CollisionEnabled ||
            bridges.Any(bridge =>
                !bridge.UpdatingTiles || bridge.Counter != 10),
            "A shooter-fired Ember Seed did not reach PART_SWITCH before " +
            "terrain handling, XOR wSwitchState bit 1, activate on contact, " +
            "and start all three bridge streams without sword invincibility.");
        Step(9);
        FailIf(
            room.GetMetatile(Point(0x39)) != 0xf7 ||
            room.GetMetatile(Point(0x62)) != 0x6a ||
            room.GetMetatile(Point(0x6c)) != 0x6a,
            "INTERAC_EXTENDABLE_BRIDGE changed a tile before its tenth update.");
        Step();
        FailIf(
            room.GetMetatile(Point(0x39)) != 0x6d ||
            room.GetMetatile(Point(0x62)) != 0xf4 ||
            room.GetMetatile(Point(0x6c)) != 0xf4 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 3,
            "The three room 4:4e bridge streams did not apply their first " +
            "source tile together with SND_DOORCLOSE on update ten.");
        Step(30);
        FailIf(
            room.GetMetatile(Point(0x39)) != 0x6d ||
            room.GetMetatile(Point(0x38)) != 0x6d ||
            room.GetMetatile(Point(0x37)) != 0x6d ||
            room.GetMetatile(Point(0x36)) != 0x6d ||
            room.GetMetatile(Point(0x42)) != 0xf4 ||
            room.GetMetatile(Point(0x52)) != 0xf4 ||
            room.GetMetatile(Point(0x62)) != 0xf4 ||
            room.GetMetatile(Point(0x4c)) != 0xf4 ||
            room.GetMetatile(Point(0x5c)) != 0xf4 ||
            room.GetMetatile(Point(0x6c)) != 0xf4 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 10,
            "Room 4:4e did not finish the source-ordered horizontal creation " +
            "and paired vertical removals at ten-update intervals.");
        Step(10);
        FailIf(bridges.Any(bridge => bridge.UpdatingTiles),
            "The horizontal bridge did not consume its terminal `$ff ten " +
            "updates after placing the fourth tile.");

        RespawnableBushRoomEntity bush = bushes[0];
        int rngCalls = _random.Calls;
        int dropsBefore = _entities.Entities<ItemDropEffect>().Count;
        FailIf(!_entities.ApplySwordHit(bush.CollisionBounds, bush.Position),
            "The room 4:4e respawnable bush rejected an ordinary sword hit.");
        bool expectedDrop = (_random.LastResult.Value & 1) != 0;
        FailIf(
            _random.Calls != rngCalls + 1 ||
            bush.State != RespawnableBushState.CutDelay ||
            bush.Counter != 0xf0 || room.GetMetatile(bush.Position) != 0x02 ||
            _entities.Entities<GrassDebrisEffect>().Count != 1 ||
            _entities.Entities<ItemDropEffect>().Count !=
                dropsBefore + (expectedDrop ? 1 : 0) ||
            expectedDrop && _entities.Entities<ItemDropEffect>()[^1].SubId != 0x06,
            "Cutting PART_RESPAWNABLE_BUSH did not consume one RNG value, " +
            "apply the 50% fixed Scent Seed drop, create grass debris, and " +
            "enter tile `$02/$f0 delay.");

        int delayUpdates = 0;
        while (bush.State == RespawnableBushState.CutDelay && delayUpdates < 482)
        {
            Step();
            delayUpdates++;
        }
        FailIf(
            delayUpdates is < 479 or > 480 ||
            bush.State != RespawnableBushState.Regenerating ||
            bush.Counter != 0x0c || room.GetMetatile(bush.Position) != 0x03,
            "PART_RESPAWNABLE_BUSH did not decrement `$f0 on alternating " +
            "global frames before entering its `$0c tile-$03 regeneration.");
        Step(11);
        FailIf(bush.State != RespawnableBushState.Regenerating || bush.Counter != 1,
            "The respawnable bush ended its `$0c regeneration wait early.");
        Step();
        FailIf(
            bush.State != RespawnableBushState.Arming || bush.Counter != 8 ||
            room.GetMetatile(bush.Position) != 0x04 || bush.CollisionEnabled,
            "The respawnable bush did not restore tile `$04 before its final " +
            "eight-update collision delay.");
        Step(7);
        FailIf(bush.CollisionEnabled,
            "The restored respawnable bush enabled collision before update eight.");
        Step();
        FailIf(!bush.CollisionEnabled || bush.State != RespawnableBushState.Ready,
            "The restored respawnable bush did not re-arm on update eight.");

        GD.Print(
            "Validated full room 4:4e: eight source-ordered objects, three " +
            "bit-1 extendable bridges and ten-update tile streams, two toggle " +
            "orbs and switch hit by moving shooter seeds before terrain, one " +
            "visible rotating bouncer with its collision-only child, four " +
            "`$c7-created Scent Seed bushes, one-call 50% drops, debris, and " +
            "exact respawn timing.");
    }

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
        DungeonOrbRoomEntity preloadedOrb =
            _entities.Entities<DungeonOrbRoomEntity>().Single();
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
            _entities.Entities<DungeonOrbRoomEntity>().Count != 0 ||
            _entities.Entities<ItemDropProducer>().Select(value => value.Position)
                .ToArray() is not [{ X: 0x98, Y: 0x18 }, { X: 0x98, Y: 0x28 }] ||
            _currentRoom.GetMetatile(Point(0x44)) != 0x26 ||
            _currentRoom.GetMetatile(Point(0x69)) == data.ChestTile,
            "Room 4:56 did not retain its event-first stream, two fixed drop " +
            "producers, hidden $26 statue, and unspawned Compass chest.");

        Step();
        DungeonOrbRoomEntity orb =
            _entities.Entities<DungeonOrbRoomEntity>().Single();
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
            _entities.Entities<DungeonOrbRoomEntity>().Count != 1 ||
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
