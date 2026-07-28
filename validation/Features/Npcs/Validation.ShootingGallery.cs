using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2e9ShootingGallery()
    {
        const int group = 2;
        const int roomId = 0xe9;

        var database = new ShootingGalleryEventDatabase();
        ShootingGalleryEventRecord record = database.Record;
        FailIf(
            record is not
            {
                Group: group,
                Room: roomId,
                InteractionId: 0x30,
                SubId: 0,
                Rounds: 10,
                InitialDelay: 0x78,
                PitchDelay: 0x28,
                PuffDelay: 0x0a,
                LayoutDelay: 0x5a,
                BetweenRoundDelay: 0x14
            } ||
            database.MainCommands.Count != 48 ||
            database.CleanupCommands.Count != 55 ||
            database.TargetCount != 10 ||
            database.Result(0) is not { ScoreDelta: 30, TextId: 0x0807 } ||
            database.Result(9) is not { ScoreDelta: 200, TextId: 0x080b } ||
            database.Result(21) is not { ScoreDelta: -50, TextId: 0x081c } ||
            !Enumerable.Range(0, 16).Select(database.Ring).SequenceEqual(
                new[]
                {
                    0x18,
                    0x1d, 0x1d, 0x1d, 0x1d,
                    0x1f, 0x1f, 0x1f, 0x1f, 0x1f,
                    0x2a, 0x2a, 0x2a, 0x2a, 0x2a, 0x2a
                }),
            "Room 2:e9 did not retain its imported script/table closure.");

        var inventorySnapshot = new byte[0x39];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);
        OracleRandomState randomSnapshot = _random.CaptureState();
        MethodInfo reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "Could not reload shooting-gallery validation inventory.");

        try
        {
            if (_inventory.Rupees < record.Cost)
                _inventory.AddRupees(record.Cost - _inventory.Rupees);
            _inventory.SetScriptedEquippedItems(
                InventoryState.ItemShield,
                InventoryState.ItemShovel);
            int originalB = _inventory.EquippedB;
            int originalA = _inventory.EquippedA;

            LoadValidationRoom(group, roomId);
            ShootingGalleryEvent gallery = _roomEvents.ShootingGallery;
            ShootingGalleryCharacter keeper =
                _entities.Entities<ShootingGalleryCharacter>().Single();
            FailIf(
                !gallery.HasState ||
                gallery.BlocksGameplay ||
                gallery.MenusDisabled ||
                keeper.Record is not { Id: 0x30, SubId: 0x00 } ||
                keeper.Position != new Vector2(0x88, 0x68) ||
                gallery.CurrentCommandIndex != 1,
                "Room 2:e9 did not instantiate its specialized $30:$00 " +
                "attendant and source-entry script update.");

            StepRoomEventFrames(1);
            // The human attendant is reached from the player side of the
            // counter. The source script's $06,$16 collision radii put Link's
            // center 28 pixels to his left at the exact blocking edge; the
            // A-button point is then 18 pixels away and must use the widened
            // X radius rather than the generic $06 NPC radius.
            _player.WarpTo(keeper.Position + Vector2.Left * 28.0f);
            _player.Face(Vector2I.Right);
            FailIf(
                !_interactions.TryInteract(_player),
                "The room 2:e9 attendant was not reachable across the " +
                "counter through the script's $06,$16 A-button geometry.");
            StepRoomEventFrames(1);
            ExpectGalleryDialogue(
                "Need some target",
                choice: true,
                "initial 10-Rupee prompt");
            FailIf(
                !gallery.BlocksGameplay ||
                !gallery.MenusDisabled ||
                !_player.CutsceneControlled,
                "The initial gallery prompt did not disable Link and menus.");

            _dialogue.SubmitChoiceForValidation(0);
            StepRoomEventFrames(31);
            ExpectGalleryDialogue(
                "Do you need an",
                choice: true,
                "explanation prompt");
            FailIf(_inventory.Rupees < 0, "Shooting-gallery rupee debit underflowed.");

            _dialogue.SubmitChoiceForValidation(0);
            StepRoomEventFrames(31);
            ExpectGalleryDialogue(
                "Then let's",
                choice: false,
                "game start prompt");
            _dialogue.Close();

            for (int frame = 0; frame < 400 && gallery.Session is null; frame++)
                StepRoomEventFrames(1);
            ShootingGallerySession session = gallery.Session ??
                throw new InvalidOperationException(
                    "The gallery main script did not spawn $30:$03.");
            ShootingGalleryGameController controller = gallery.Controller ??
                throw new InvalidOperationException(
                    "The gallery event lost its native controller.");
            FailIf(
                gallery.BlocksGameplay ||
                !gallery.MenusDisabled ||
                _player.CutsceneControlled ||
                _player.Position != new Vector2(0x50, 0x60) ||
                _player.FacingVector != Vector2I.Up ||
                _inventory.EquippedB != TreasureDatabase.TreasureSword ||
                _inventory.EquippedA != InventoryState.ItemNone ||
                TileAt(0x74) != record.EntranceClosedTile0 ||
                TileAt(0x75) != record.EntranceClosedTile1 ||
                controller.State != 1 ||
                controller.Counter != record.InitialDelay - 1,
                "The gallery setup did not preserve its fade, sword equip, " +
                "Link placement, entrance closure, and input/menu split.");

            Vector2 signReadingPosition =
                PointFor(0x52) + Vector2.Down * 15.0f;
            _player.WarpTo(signReadingPosition);
            _player.Face(Vector2I.Up);
            int pausedInitialCounter = controller.Counter;
            int pausedCommand = gallery.CurrentCommandIndex;
            int pausedKeeperAnimation = keeper.CurrentAnimationFrame;
            FailIf(
                !_interactions.TryInteract(_player),
                "The room 2:e9 prize sign was not readable while " +
                "wMenuDisabled remained set for the active pitch.");
            ExpectGalleryDialogue(
                "Hit targets",
                choice: false,
                "in-game prize sign");
            RoomEntityManagerState initialPauseClock =
                _entities.CaptureDebugState();
            StepRoomEventFrames(8);
            FailIf(
                controller.Counter != pausedInitialCounter ||
                gallery.CurrentCommandIndex != pausedCommand ||
                keeper.CurrentAnimationFrame != pausedKeeperAnimation,
                "The prize-sign textbox did not freeze the gallery's " +
                "$30 interaction state and NPC animation.");
            _entities.RestoreDebugStateAfterRoomParse(initialPauseClock);
            _dialogue.Close();

            ICutsceneCommandHost galleryHost = gallery;
            (string Handler, int Threshold)[] rewardThresholds =
            [
                ("CheckScore0", record.RingScore),
                ("CheckScore1", record.GashaScore),
                ("CheckScore2", record.RupeeScore),
                ("CheckScore3", record.HeartScore)
            ];
            foreach ((string handler, int threshold) in rewardThresholds)
            {
                session.Score = threshold - 1;
                galleryHost.RunNativeHandler(handler);
                FailIf(
                    galleryHost.MemoryEquals("Condition", 1),
                    $"{handler} accepted score {threshold - 1}.");
                session.Score = threshold;
                galleryHost.RunNativeHandler(handler);
                FailIf(
                    !galleryHost.MemoryEquals("Condition", 1),
                    $"{handler} rejected its exact {threshold}-point boundary.");
            }
            session.Score = 0;

            var strikeSession = new ShootingGallerySession();
            strikeSession.BeginBall();
            int strikeSound = -1;
            var strikeBall = new ShootingGalleryBall();
            strikeBall.Initialize(
                database,
                strikeSession,
                _rooms.CurrentRoom,
                new OracleRandom(),
                PointFor(record.EntrancePosition0),
                sound => strikeSound = sound,
                () => 0);
            var strikeSpawns = new List<RoomEntitySpawn>();
            strikeBall.UpdateFrame(strikeSpawns);
            strikeBall.UpdateFrame(strikeSpawns);
            FailIf(
                !strikeBall.Finished ||
                !strikeSession.IsStrike ||
                strikeSound != record.StrikeSound,
                "Incoming PART_BALL $38 did not detect the current closed " +
                "entrance tile as a strike before applying speed.");
            strikeBall.Free();

            int randomCallsBeforeLayout = _entities.RandomCalls;
            StepRoomEventFrames(record.InitialDelay - 2);
            FailIf(
                controller.State != 1 || controller.Counter != 1,
                "$30:$03 initial delay did not retain its 120-update boundary.");
            StepRoomEventFrames(1);
            FailIf(
                controller.State != 2 ||
                controller.Counter != record.PitchDelay,
                "$30:$03 did not enter its 40-update pitch warning.");
            StepRoomEventFrames(record.PitchDelay);
            FailIf(
                controller.State != 3 ||
                controller.Counter != record.PuffDelay ||
                _entities.Entities<PuzzlePuffEffect>().Count != 10,
                "The pitch warning did not spawn all ten source-order puffs.");
            StepRoomEventFrames(record.PuffDelay);
            FailIf(
                controller.State != 4 ||
                controller.Counter != record.LayoutDelay ||
                controller.RemainingLayouts != 9 ||
                _entities.RandomCalls != randomCallsBeforeLayout + 1,
                "The first target layout did not consume one shared RNG " +
                "call and deplete exactly one of ten layouts.");
            StepRoomEventFrames(record.LayoutDelay);
            ShootingGalleryBall ball =
                _entities.Entities<ShootingGalleryBall>().Single();
            FailIf(
                session.Round != 1 ||
                controller.State != 5 ||
                ball.State != ShootingGalleryBallState.Incoming ||
                ball.Speed is not (0x64 or 0x3c) ||
                _entities.RandomCalls != randomCallsBeforeLayout + 2,
                "The first pitch did not spawn PART_BALL $38 with one " +
                "additional shared-RNG speed decision.");

            Vector2 launcherPosition = ball.Position;
            TerrainInfo launcherTerrain =
                _rooms.CurrentRoom.GetTerrainInfo(launcherPosition);
            FailIf(
                launcherTerrain.Collision != 0x0a ||
                !_rooms.CurrentRoom.IsSolid(launcherPosition),
                "PART_BALL $38 did not begin in the launcher's source " +
                "partial-collision $0a quarter.");
            StepRoomEventFrames(1);
            FailIf(
                ball.Finished ||
                session.BallFinished ||
                ball.Position.Y <= launcherPosition.Y,
                "PART_BALL $38 treated the launcher's partial collision " +
                "$0a as a strike instead of exiting the hole.");

            _player.WarpTo(signReadingPosition);
            _player.Face(Vector2I.Up);
            Vector2 pausedBallPosition = ball.Position;
            int pausedBallUpdates = ball.ElapsedUpdates;
            FailIf(
                !_interactions.TryInteract(_player),
                "The room 2:e9 prize sign stopped accepting A after " +
                "PART_BALL $38 spawned.");
            RoomEntityManagerState ballPauseClock =
                _entities.CaptureDebugState();
            StepRoomEventFrames(8);
            FailIf(
                ball.Position != pausedBallPosition ||
                ball.ElapsedUpdates != pausedBallUpdates,
                "PART_BALL $38 continued moving behind the prize-sign " +
                "textbox instead of following wTextIsActive.");
            _entities.RestoreDebugStateAfterRoomParse(ballPauseClock);
            _dialogue.Close();

            int negativeTarget = Enumerable.Range(0, database.TargetCount)
                .First(index =>
                {
                    int tile = TileAt(database.Target(index).PackedPosition);
                    return tile == record.TargetRed || tile == record.TargetImp;
                });
            int negativePacked = database.Target(negativeTarget).PackedPosition;
            Vector2 negativePoint = PointFor(negativePacked);
            ball.Position = negativePoint;
            Vector2 deflectionSource =
                negativePoint + new Vector2(-8.0f, 8.0f);
            FailIf(
                !ball.Deflect(
                new Rect2(negativePoint - Vector2.One * 4, Vector2.One * 8),
                deflectionSource),
                "PART_BALL $38 rejected an overlapping sword deflection.");
            StepRoomEventFrames(2);
            FailIf(
                ball.State != ShootingGalleryBallState.Reflected ||
                ball.Angle != 0x04 ||
                ball.Position.X <= negativePoint.X ||
                ball.Position.Y >= negativePoint.Y ||
                session.HitCount != 1 ||
                TileAt(negativePacked) != record.FloorTile ||
                ball.CollisionCounter != 3 ||
                _entities.Entities<ShootingGalleryTargetDebris>().Count !=
                    database.Debris.Count ||
                _entities.Entities<ShootingGalleryTargetDebris>().Any(
                    debris =>
                        debris.Counter != database.Debris.Lifetime ||
                        debris.Palette != database.Debris.RedPalette),
                "Reflected PART_BALL $38 did not retain its diagonal " +
                "32-step knockback, replace its red/imp target, record " +
                "the hit, install the 3-update cooldown, and emit four " +
                "$92:$05 debris objects.");

            Vector2 reflectedPausePosition = ball.Position;
            int reflectedPauseUpdates = ball.ElapsedUpdates;
            _player.WarpTo(signReadingPosition);
            _player.Face(Vector2I.Up);
            FailIf(
                !_interactions.TryInteract(_player),
                "The room 2:e9 prize sign stopped accepting A after a " +
                "target hit.");
            StepRoomEventFrames(2);
            FailIf(
                ball.Position != reflectedPausePosition ||
                ball.ElapsedUpdates != reflectedPauseUpdates ||
                _entities.Entities<ShootingGalleryTargetDebris>().Any(
                    debris => debris.Counter != database.Debris.Lifetime - 2),
                "The textbox dispatcher did not freeze PART_BALL while " +
                "continuing enabled-bit-7 INTERAC_FALLING_ROCK debris.");
            _dialogue.Close();

            // Finish the remaining pitch travel outside the screen. This
            // exercises every native delay/layout depletion/result script and
            // the final cleanup/retry path without synthesized player input.
            for (int round = 1; round <= record.Rounds; round++)
            {
                if (round > 1)
                {
                    for (int frame = 0;
                        frame < 400 &&
                        (session.Round < round ||
                            _entities.Entities<ShootingGalleryBall>().Count == 0);
                        frame++)
                    {
                        CloseGalleryDialogueIfOpen();
                        StepRoomEventFrames(1);
                    }
                    ball = _entities.Entities<ShootingGalleryBall>().Single();
                }

                ball.Position = new Vector2(-1, ball.Position.Y);
                StepRoomEventFrames(1);
                for (int frame = 0; frame < 180; frame++)
                {
                    CloseGalleryDialogueIfOpen();
                    StepRoomEventFrames(1);
                    bool roundDone = round < record.Rounds
                        ? controller.State == 1 &&
                            session.Round == round &&
                            !gallery.BlocksGameplay
                        : gallery.ScriptKind ==
                            ShootingGalleryScriptKind.Cleanup;
                    if (roundDone)
                        break;
                }

                FailIf(
                    round < record.Rounds &&
                    (controller.State != 1 ||
                        controller.Counter != record.BetweenRoundDelay ||
                        session.Score != 0 ||
                        session.PendingResult != -1),
                    $"Gallery miss result {round} did not return to the " +
                    "exact 20-update between-round delay.");
            }

            FailIf(
                session.Round != record.Rounds ||
                session.Score != 0 ||
                !controller.Finished ||
                gallery.ScriptKind != ShootingGalleryScriptKind.Cleanup ||
                !gallery.BlocksGameplay ||
                !gallery.MenusDisabled,
                "The tenth pitch did not select the final-total and cleanup scripts.");

            for (int frame = 0;
                frame < 500 &&
                (!_dialogue.IsOpen ||
                    !_dialogue.CurrentMessage.StartsWith(
                        "Try again", StringComparison.Ordinal));
                frame++)
            {
                CloseGalleryDialogueIfOpen();
                StepRoomEventFrames(1);
            }
            ExpectGalleryDialogue(
                "Try again",
                choice: true,
                "retry prompt");
            FailIf(
                _player.Position != new Vector2(0x68, 0x68) ||
                _player.FacingVector != Vector2I.Right ||
                _inventory.EquippedB != originalB ||
                _inventory.EquippedA != originalA ||
                TileAt(0x74) != record.EntranceOpenTile0 ||
                TileAt(0x75) != record.EntranceOpenTile1 ||
                Enumerable.Range(0, database.TargetCount).Any(
                    index => TileAt(
                        database.Target(index).PackedPosition) !=
                        record.FloorTile),
                "Gallery cleanup did not restore Link, equips, entrance, " +
                "and all ten target tiles before retry.");

            _dialogue.SubmitChoiceForValidation(1);
            StepRoomEventFrames(31);
            ExpectGalleryDialogue(
                "Suit yourself",
                choice: false,
                "retry rejection");
            _dialogue.Close();
            StepRoomEventFrames(31);
            StepRoomEventFrames(1);
            FailIf(
                gallery.BlocksGameplay ||
                gallery.MenusDisabled ||
                _player.CutsceneControlled ||
                gallery.CurrentCommandIndex != 2,
                "Rejecting retry did not restore normal input and the " +
                "attendant's persistent A-button loop " +
                $"(blocks={gallery.BlocksGameplay}, " +
                $"menus={gallery.MenusDisabled}, " +
                $"controlled={_player.CutsceneControlled}, " +
                $"command={gallery.CurrentCommandIndex}).");

            _inventory.SetScriptedEquippedItems(
                InventoryState.ItemShield,
                TreasureDatabase.TreasureSword);
            galleryHost.RunNativeHandler("EquipSword");
            FailIf(
                _inventory.EquippedB != InventoryState.ItemNone ||
                _inventory.EquippedA != TreasureDatabase.TreasureSword,
                "The gallery did not clear B while retaining a Sword " +
                "already equipped on A.");
            galleryHost.RunNativeHandler("RestoreEquips");
            FailIf(
                _inventory.EquippedB != InventoryState.ItemShield ||
                _inventory.EquippedA != TreasureDatabase.TreasureSword,
                "The gallery did not restore its alternate A-Sword loadout.");

            GD.Print(
                "Validated room 2:e9 INTERAC_SHOOTING_GALLERY $30:$00, " +
                "imported prompts/retry/rewards, white fades, sword/entrance " +
                "restore, exact B/A clearing, Link-vs-menu input split, ten " +
                "readable-sign textbox freeze, " +
                "source-order targets, 120/40/10/90/20 counters, one layout " +
                "RNG and one ball RNG call per pitch, launcher partial-" +
                "collision exit, 32-step sword reflection/target replacement, " +
                "negative-hit/miss/final result scripts, and cleanup.");
        }
        finally
        {
            _saveData.WriteWramBytes(0xc688, inventorySnapshot);
            _saveData.CommitInventoryChange();
            reloadInventory.Invoke(_inventory, null);
            _random.RestoreState(randomSnapshot);
            if (_dialogue.IsOpen)
                _dialogue.Close();
        }

        int TileAt(int packed)
        {
            Vector2 point = new(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8);
            return _rooms.CurrentRoom.GetMetatile(point);
        }

        static Vector2 PointFor(int packed) => new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
    }

    private void ExpectGalleryDialogue(
        string prefix,
        bool choice,
        string phase)
    {
        FailIf(
            !_dialogue.IsOpen ||
            !_dialogue.CurrentMessage.StartsWith(prefix, StringComparison.Ordinal) ||
            _dialogue.ChoiceActive != choice,
            $"Room 2:e9 {phase} did not preserve its imported textbox.");
    }

    private void CloseGalleryDialogueIfOpen()
    {
        if (_dialogue.IsOpen)
            _dialogue.Close();
    }
}
