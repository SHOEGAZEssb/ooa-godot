using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePushBlocks()
    {
        ComparePushBlockDataRom();
        CompareRoomShopFlagsRom();
        CompareTileInfoContactRom();
        CompareTileAButtonGatesRom();
        ComparePushBlockGameplayRom();
        ComparePushBlockBraceletHintRom();
        ComparePushBlockCubeGateRom();
        ComparePushBlockHazardsRom();
        ComparePushBlockSplashRom();
        ComparePushBlockSplashScrollRom();
        ComparePushBlockContactRom();
        GD.Print("Validated native push tile data, wall/contact gates, movement, hole allocation/" +
            "animation, hidden-grave locks and completion, cues/RNG, repeated gameplay " +
            "and independent staircase bounds.");
    }

    private void ValidateSpiritsGraveEntranceInteractions()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        var data = new DungeonEntranceInteractionDatabase();
        IReadOnlyList<PlacementRecord> placements =
            data.GetRoomRecords(4, 0x24);
        FailIf(
            data.PlacementCount != 42 || placements.Count != 3 ||
            placements[0] is not
                { Order: 0, Kind: DungeonEntranceInteractionDatabaseObjectKind.Entry } ||
            placements[1] is not
                { Order: 1, Kind: DungeonEntranceInteractionDatabaseObjectKind.EyeSpawner } ||
            placements[2] is not
                { Order: 2, Kind: DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal } ||
            data.PortalPairFor(1) is not { MinibossRoom: 0x18, EntranceRoom: 0x24 },
            "Room 4:24's shared dungeon interactions were not imported in source order.");

        var root = new Node { Name = "SpiritsGraveEntranceValidation" };
        AddChild(root);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var runtime = new OracleRuntimeState();
        using var managerFixture = RoomEntityValidationFixture.ForRoot(
            root, new() { SaveData = save, RuntimeState = runtime });
        RoomEntityManager manager = managerFixture.Manager;
        OracleRoomData room = _world.LoadRoom(4, 0x24);
        _player.EndCutsceneControl();
        _player.WarpTo(new Vector2(0x48, 0x88));

        // Destination preloading must complete only declared source state-0
        // presentation work before freezing the incoming entity set. The eye
        // spawner recursively creates six UpdateThisFrame children, while the
        // enabled portal resolves its visibility predicate without beginning
        // contact or animation behavior.
        save.SetRoomFlag(
            4, 0x18, OracleSaveData.RoomFlag80);
        OracleRoomData transitionSource = _world.LoadRoom(4, 0x14);
        manager.LoadRoom(4, transitionSource);
        Vector2 incomingOffset = Vector2.Down * room.Height;
        manager.BeginScreenTransition(4, room, incomingOffset);
        List<StatueEyeball> preloadedEyes =
            manager.Entities<StatueEyeball>();
        MinibossPortal preloadedPortal =
            manager.Entities<MinibossPortal>().SingleOrDefault() ??
            throw new InvalidOperationException(
                "Room 4:24 did not preload its enabled miniboss portal.");
        FailIf(
            preloadedEyes.Count != 6 ||
            preloadedEyes.Any(eye =>
                !eye.Visible || !eye.Initialized ||
                eye.TransitionDrawOffset != incomingOffset) ||
            !preloadedPortal.Visible ||
            preloadedPortal.TransitionDrawOffset != incomingOffset,
            "Room 4:24 did not recursively complete its declared eye/portal " +
            "presentation preload before the incoming scroll became visible.");
        Vector2[] frozenEyePositions =
            preloadedEyes.Select(eye => eye.Position).ToArray();
        int frozenPortalFrame = preloadedPortal.AnimationFrame;
        manager.Update(1.0, _player);
        FailIf(
            !preloadedEyes.Select(eye => eye.Position)
                .SequenceEqual(frozenEyePositions) ||
            preloadedPortal.AnimationFrame != frozenPortalFrame ||
            !preloadedPortal.Visible,
            "Room 4:24 advanced preloaded eye tracking or portal animation " +
            "while ordinary destination entities were frozen.");
        manager.FinishScreenTransition();
        manager.Clear();
        save.SetRoomFlag(
            4, 0x18, OracleSaveData.RoomFlag80, value: false);

        // A direct/debug room load retains all three source slots until their
        // first updates. Dungeon-stuff then rejects the non-whiteout entry,
        // the eye spawner creates children in descending packed-position
        // order, and the undefeated miniboss suppresses the portal.
        manager.LoadRoom(4, room);
        FailIf(
            manager.Entities<Node2D>().Count != 3,
            "Room 4:24 did not initially retain its three placed source interactions.");
        manager.Update(update, _player);
        List<StatueEyeball> eyes = manager.Entities<StatueEyeball>();
        Vector2[] initialEyePositions =
        [
            new(0x98, 0x76), new(0x58, 0x76),
            new(0x98, 0x46), new(0x58, 0x46),
            new(0x98, 0x16), new(0x58, 0x16)
        ];
        FailIf(
            eyes.Count != initialEyePositions.Length ||
            manager.Entities<MinibossPortal>().Count != 0,
            "Room 4:24 did not create exactly six eyes or suppress its uncleared portal.");
        for (int index = 0; index < eyes.Count; index++)
        {
            FailIf(
                eyes[index].Position != initialEyePositions[index] ||
                eyes[index].Visible != (index != 0) || eyes[index].Initialized != (index != 0) ||
                manager.InteractionSlot(eyes[index]) != (index == 0 ? 2 : index + 4) ||
                eyes[index].Direction != 4 || eyes[index].AnimationIndex != 4 ||
                eyes[index].PixelHash == 0,
                $"Statue eye {index} did not preserve descending spawn order, " +
                "native slot initialization timing, default direction, or imported OAM.");
        }
        // Entry slot $d2 was freed before scanner $d3 ran. Its first child
        // reuses $d2 and waits; later children in $d5-$d9 run immediately.
        manager.Update(update, _player);
        FailIf(!eyes[0].Visible || !eyes[0].Initialized || eyes[0].Position != initialEyePositions[0],
            "The lower-slot statue eye must initialize on the next pass without starting its aiming update.");
        ulong fixedEyePixelHash = eyes[0].PixelHash;
        Vector2 firstEyeTileCenter = new(0x98, 0x78);
        for (int direction = 0; direction < 8; direction++)
        {
            _player.WarpTo(
                firstEyeTileCenter +
                OracleObjectMovement.Shared.Direction(direction * 4) * 32);
            manager.Update(update, _player);
            DungeonEntranceInteractionDatabaseVisualRecord visual =
                data.EyeVisuals[direction];
            Vector2 expectedPosition = new(0x90 + visual.LowX, 0x70 + visual.LowY);
            FailIf(
                eyes[0].Direction != direction || eyes[0].AnimationIndex != 4 ||
                eyes[0].PixelHash != fixedEyePixelHash ||
                eyes[0].Position != expectedPosition ||
                visual.Animation != data.EyeVisuals[4].Animation,
                $"Room 4:24's upper statue eye did not preserve fixed " +
                $"animation-$04 OAM at aiming direction {direction}.");
        }

        // The whiteout-only handler clears the three dungeon session fields,
        // applies D1's spinner byte, then shows TX_0201 at strict collision.
        int entryText = 0;
        string entryMessage = string.Empty;
        manager.DungeonEntranceTriggered += (textId, message) =>
        {
            entryText = textId;
            entryMessage = message;
        };
        runtime.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0x55);
        runtime.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0xaa);
        runtime.SetWramByte(OracleRuntimeState.SpinnerStateAddress, 0xff);
        _player.WarpTo(new Vector2(0x78, 0x88));
        manager.LoadRoom(
            4, room, EnemyPlacementContext.FromWarpDestination(0xff));
        manager.Update(update, _player);
        FailIf(
            entryText != 0x0201 || entryMessage != data.Entry(1).Message ||
            runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != 0 ||
            runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0 ||
            runtime.ReadWramByte(OracleRuntimeState.SpinnerStateAddress) != 0,
            "Room 4:24 dungeon-stuff did not initialize D1 state and show TX_0201.");

        // Bit 7 belongs to the miniboss room ($18), not the entrance. Starting
        // on an enabled portal must wait for Link to leave; a fresh contact
        // then pins/spins Link for exactly $30 updates and requests the shared
        // fadeout warp back to $18 at packed position $57.
        save.SetRoomFlag(4, 0x18, OracleSaveData.RoomFlag80);
        var sounds = new List<int>();
        Warp? requestedWarp = null;
        manager.SoundRequested += sounds.Add;
        manager.RoomWarpRequested += warp => requestedWarp = warp;
        Vector2 portalPosition = new(0x78, 0x58);
        _player.WarpTo(portalPosition);
        manager.LoadRoom(4, room);
        manager.Update(update, _player);
        FailIf(
            manager.Entities<MinibossPortal>() is not [{ Visible: true }] ||
            sounds.Count != 0 || _player.CutsceneControlled,
            "Enabled room 4:24 portal did not enter its initial-overlap wait state.");
        manager.Update(update, _player);
        FailIf(sounds.Count != 0, "Room 4:24 portal retriggered while Link remained on its destination.");
        _player.WarpTo(new Vector2(0x78, 0x70));
        manager.Update(update, _player);
        _player.WarpTo(portalPosition);
        manager.Update(update, _player);
        FailIf(
            !_player.CutsceneControlled || sounds is not [SoundId.SndTeleport] ||
            requestedWarp.HasValue,
            "Room 4:24 portal did not start its fresh-contact teleport state and sound.");
        for (int frame = 0; frame < data.PortalSpinUpdates - 1; frame++)
            manager.Update(update, _player);
        FailIf(
            requestedWarp.HasValue || _player.Position != portalPosition,
            "Room 4:24 portal warped early or failed to pin Link during its $30 counter.");
        manager.Update(update, _player);
        FailIf(
            requestedWarp is not
            {
                SourceGroup: 4,
                SourceRoom: 0x24,
                SourcePosition: 0x57,
                SourceTransition: WarpSourceTransition.FadeOut,
                DirectFadeOut: true,
                DestinationGroup: 4,
                DestinationRoom: 0x18,
                DestinationPosition: 0x57,
                DestinationParameter: 0,
                DestinationTransition: WarpDestinationTransition.Basic
            },
            "Room 4:24 portal did not request the exact D1 miniboss-room fadeout warp.");

        _player.EndCutsceneControl();
        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print("Validated room 4:24 dungeon-entry TX_0201/session state, six " +
            "source-ordered eyes with fixed animation-$04 OAM and all eight position " +
            "offsets, recursive transition-presentation preload plus ordinary-state " +
            "freeze, miniboss-room flag gate, destination-overlap guard, teleport " +
            "sound, $30 Link spin, and bidirectional portal metadata.");
    }

    private void ValidateDungeonMechanics()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        var database = new DungeonMechanicDatabase();
        int switchRecordCount = 0;
        int buttonRecordCount = 0;
        int triggerDoorRecordCount = 0;
        int enemyFallingKeyCount = 0;
        int enemyClearChestCount = 0;
        int permanentTriggerChestCount = 0;
        int retractableTriggerChestCount = 0;
        int moonlitCrystalEventCount = 0;
        int moonlitArmosEventCount = 0;
        int moonlitCrystalCount = 0;
        int moonlitFallingKeyCount = 0;
        int tilePatternFallingKeyCount = 0;
        int torchTranslatorCount = 0;
        int torchScannerCount = 0;
        int orbCount = 0;
        int extendableBridgeCount = 0;
        int rotatableSeedThingCount = 0;
        int respawnableBushScannerCount = 0;
        for (int group = 0; group < 8; group++)
        for (int roomId = 0; roomId < 0x100; roomId++)
        {
            foreach (DungeonMechanicDatabaseRecord record in
                database.GetRoomRecords(group, roomId))
            {
                switchRecordCount += record.Id == 0x05 ? 1 : 0;
                buttonRecordCount += record.Id == 0x09 ? 1 : 0;
                triggerDoorRecordCount += record is
                    { Id: InteractionId.DoorController, SubId: >= 0x04 and <= 0x07 } ? 1 : 0;
                enemyFallingKeyCount += record is
                    { Id: InteractionId.DungeonStuff, SubId: 0x01 } ? 1 : 0;
                enemyClearChestCount += record is
                    { Id: InteractionId.DungeonStuff, SubId: 0x02 } ? 1 : 0;
                permanentTriggerChestCount += record is
                    { Id: InteractionId.DungeonScript, SubId: 0x00 } ? 1 : 0;
                retractableTriggerChestCount += record is
                    { Id: InteractionId.DungeonEvents, SubId: 0x17 } ? 1 : 0;
                moonlitCrystalEventCount += record is
                    { Id: InteractionId.DungeonEvents, SubId: 0x0d } ? 1 : 0;
                moonlitArmosEventCount += record is
                    { Id: InteractionId.DungeonEvents, SubId: 0x0a or 0x0c } ? 1 : 0;
                moonlitFallingKeyCount += record is
                    { Id: InteractionId.DungeonEvents, SubId: 0x0e } ? 1 : 0;
                tilePatternFallingKeyCount += record is
                    { Id: InteractionId.DungeonEvents, SubId: 0x09 } ? 1 : 0;
                moonlitCrystalCount += record is
                    { Id: InteractionId.TriggerTranslator, SubId: 0x10 or 0x20 or 0x40 or 0x80 } ? 1 : 0;
                torchTranslatorCount += record is
                    { Id: InteractionId.TriggerTranslator, SubId: 0x02 } ? 1 : 0;
                torchScannerCount += record is
                    { Id: InteractionId.CreateObjectAtEachTileIndex, SubId: 0x08 } ? 1 : 0;
                orbCount += record.Id == 0x03 ? 1 : 0;
                extendableBridgeCount += record.Id == 0x23 ? 1 : 0;
                rotatableSeedThingCount += record is
                    { Id: InteractionId.SmogBoss, SubId: 0x0a or 0x08 or 0x88 } ? 1 : 0;
                respawnableBushScannerCount += record is
                    { Id: InteractionId.CreateObjectAtEachTileIndex, SubId: 0x04 } ? 1 : 0;
            }
        }
        FailIf(
            database.RecordCount != 239 || switchRecordCount != 7 ||
            buttonRecordCount != 49 ||
            triggerDoorRecordCount != 20 ||
            enemyFallingKeyCount != 2 ||
            enemyClearChestCount != 12 ||
            permanentTriggerChestCount != 7 ||
            retractableTriggerChestCount != 6 ||
            moonlitArmosEventCount != 2 ||
            moonlitCrystalEventCount != 4 || moonlitCrystalCount != 4 ||
            moonlitFallingKeyCount != 1 ||
            tilePatternFallingKeyCount != 1 ||
            torchTranslatorCount != 2 || torchScannerCount != 8 ||
            orbCount != 17 || extendableBridgeCount != 7 ||
            rotatableSeedThingCount != 4 || respawnableBushScannerCount != 3 ||
            database.GetRoomRecords(4, 0x0c).Select(record => record.Order)
                .ToArray() is not [0, 1],
            "Expected two shared enemy-clear falling keys, 12 enemy-clear chests, " +
            "seven switches, 49 buttons, 20 " +
            "trigger shutters, seven delayed and six retractable trigger " +
            "chests, two Moonlit orb/button Armos events, four Moonlit crystal " +
            "handlers/parts, one direct-layout and one tile-pattern falling key, " +
            "two torch-count translators, eight tile-$08 torch scanners, " +
            "17 orbs, seven extendable bridges, two rotating seed bouncers, " +
            "three respawnable-bush scanners, " +
            "and 73 ordered $13:$01/enemy-shutter dungeon placements.");

        void Step() => _entities.Update(update, _player);
        _entities.WorldToScreen = static position => position;

        // Room 4:08 uses the dungeon-$0d table's $20:$00 script: exact
        // wActiveTriggers == $01, solve cue plus INTERAC_PUFF, wait 15, then
        // TILEINDEX_CHEST at packed $57. updateParts publishes the button
        // before updateInteractions, regardless of placement order.
        _saveData.SetRoomFlag(
            4, 0x08, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(4, 0x08);
        OracleRoomData room = _currentRoom;
        Vector2 room408Button = new(0x78, 0x18);
        Vector2 room408Chest = new(0x78, 0x58);
        _player.EndNewGameSlowFall();
        _player.WarpTo(new Vector2(0x48, 0x78));
        _sound.ClearPlayRequestAudit();
        FailIf(
            _entities.Entities<TriggerChestRoomEntity>() is not
            [{
                Id: 0x20,
                SubId: 0x00,
                PackedPosition: 0x57,
                TriggerParameter: 0x01,
                Predicate: TriggerPredicate.Exact
            }] ||
            _entities.Entities<GroundButtonRoomEntity>() is not
            [{ SubId: 0x00, PackedPosition: 0x17, Reusable: false }] ||
            room.GetMetatile(room408Button) != 0x0c ||
            room.GetMetatile(room408Chest) != 0xa3,
            "Room 4:08 did not instantiate its ordered exact-trigger chest script and button.");
        Step();
        _player.WarpTo(room408Button);
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x01 ||
            room.GetMetatile(room408Button) != 0x0d ||
            room.GetMetatile(room408Chest) != 0xa3 ||
            _entities.Entities<GroundButtonRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1,
            "Room 4:08's button and $20:$00 script must react in the same update.");
        FailIf(
            _entities.Entities<TriggerChestRoomEntity>() is not [{ Counter: 15 }] ||
            _entities.Entities<PuzzlePuffEffect>() is not
                [{ ElapsedUpdates: 1, AnimationFrame: 0 }] ||
            room.GetMetatile(room408Chest) != 0xa3 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(SoundId.SndPoof) != 1,
            "Room 4:08 did not request solve/poof and begin the exact 15-update chest wait.");
        for (int frame = 0; frame < database.ChestWait - 1; frame++)
            Step();
        FailIf(
            room.GetMetatile(room408Chest) != 0xa3 ||
            _entities.Entities<TriggerChestRoomEntity>() is not [{ Counter: 1 }] ||
            _entities.Entities<PuzzlePuffEffect>() is not
                [{ ElapsedUpdates: 15, AnimationFrame: 2 }],
            "Room 4:08 installed its chest before wait 15 reached zero.");
        Step();
        FailIf(
            room.GetMetatile(room408Chest) != 0xf1 ||
            _entities.Entities<TriggerChestRoomEntity>().Count != 0 ||
            _entities.Entities<PuzzlePuffEffect>() is not
                [{ ElapsedUpdates: 16, AnimationFrame: 2 }],
            "Room 4:08 did not install chest tile $f1 on the wait-15 zero update.");
        Step();
        Step();
        Step();
        FailIf(
            _entities.Entities<PuzzlePuffEffect>() is not
            [{ ElapsedUpdates: 19, CurrentParameter: 0xff }],
            "INTERAC_PUFF did not reach its imported terminal parameter after 6/8/4 updates.");
        Step();
        FailIf(
            _entities.Entities<PuzzlePuffEffect>().Count != 0,
            "INTERAC_PUFF did not delete one update after terminal parameter $ff.");

        TreasureObjectVisualRecord smallKeyVisual =
            _treasures.GetObjectVisual(0x42);
        FailIf(
            smallKeyVisual.Sprite != "spr_map_compass_keys_bookofseals" ||
            smallKeyVisual.TileBase != 0x0c || smallKeyVisual.Palette != 5 ||
            smallKeyVisual.DefaultAnimation != 0,
            "TREASURE_OBJECT_SMALL_KEY_03 did not import INTERAC_TREASURE graphic $42 exactly.");
        int dungeon = _rooms.CurrentDungeonIndex;
        int keysBefore = _inventory.GetDungeonSmallKeys(dungeon);
        _player.WarpTo(room408Chest + Vector2.Down * 12.0f);
        _player.Face(Vector2I.Up);
        bool interacted = TryInteract(_player);
        ChestTreasureEffect keyReward = _interactions.ChestReward!;
        FailIf(
            !interacted || !_interactions.ChestRewardActive ||
            keyReward is not { VisualGraphic: 0x42 } ||
            room.GetMetatile(room408Chest) != 0xf0 ||
            _sound.PlayRequestsFor(SoundId.SndOpenChest) != 1 ||
            _sound.PlayRequestsFor(SoundId.SndGetSeed) != 0 ||
            _sound.PlayRequestsFor(SoundId.SndGetItem) != 0,
            "Room 4:08's revealed chest did not open as graphic $42 with SND_OPENCHEST.");
        var rupeeReward = new ChestTreasureEffect();
        rupeeReward.Initialize(Vector2.Zero, _treasures.GetObjectVisual(0x2b));
        FailIf(
            OracleGraphicsCache.PixelHash(keyReward.RewardTexture.GetImage()) ==
            OracleGraphicsCache.PixelHash(rupeeReward.RewardTexture.GetImage()),
            "Room 4:08's small-key reward still rendered as the rupee graphic $2b.");
        rupeeReward.Free();
        _interactions.Update(31.0 / 60.0, _player);
        FailIf(
            _inventory.GetDungeonSmallKeys(dungeon) != keysBefore ||
            _sound.PlayRequestsFor(SoundId.SndGetSeed) != 0 ||
            _sound.PlayRequestsFor(SoundId.SndGetItem) != 0,
            "Room 4:08 granted its key or SND_GETITEM before the 32-frame rise ended.");
        _interactions.Update(1.0 / 60.0, _player);
        FailIf(
            _inventory.GetDungeonSmallKeys(dungeon) != keysBefore + 1 ||
            _sound.PlayRequestsFor(SoundId.SndGetSeed) != 1 ||
            _sound.PlayRequestsFor(SoundId.SndGetItem) != 1 ||
            !_dialogue.IsOpen,
            "Room 4:08 did not grant its key with SND_GETSEED then SND_GETITEM after 32 frames.");
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        LoadValidationRoom(4, 0x08);
        room = _currentRoom;
        _player.WarpTo(new Vector2(0x48, 0x78));
        Step();
        FailIf(
            _entities.Entities<TriggerChestRoomEntity>().Count != 0 ||
            room.GetMetatile(room408Chest) != 0xf0,
            "Room 4:08 did not retire $20:$00 and load its opened chest " +
            $"for ROOMFLAG_ITEM: entities={_entities.Entities<TriggerChestRoomEntity>().Count}, " +
            $"tile=${room.GetMetatile(room408Chest):x2}, " +
            $"flag={_saveData.HasRoomFlag(4, 0x08, OracleSaveData.RoomFlagItem)}.");
        _saveData.SetRoomFlag(
            4, 0x08, OracleSaveData.RoomFlagItem, value: false);

        // $21:$17 is the reusable companion consumer: exact trigger equality
        // creates its chest immediately and pressure release restores the
        // original room-buffer tile with another puff but no solve cue.
        _saveData.SetRoomFlag(
            4, 0x7a, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(4, 0x7a);
        room = _currentRoom;
        Vector2 retractableChest = new(0x98, 0x38);
        Vector2 retractableButton = new(0x48, 0x18);
        byte retractableOriginal = room.GetOriginalMetatile(retractableChest);
        _player.WarpTo(new Vector2(0x78, 0x78));
        _sound.ClearPlayRequestAudit();
        Step();
        _player.WarpTo(retractableButton);
        Step();
        Step();
        FailIf(
            room.GetMetatile(retractableChest) != 0xf1 ||
            _entities.Entities<RetractableTriggerChestRoomEntity>().Count != 1 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(SoundId.SndPoof) != 1,
            "Room 4:7a's $21:$17 did not create its exact-$01 reusable chest.");
        _player.WarpTo(new Vector2(0x78, 0x78));
        Step();
        Step();
        FailIf(
            _entities.ActiveTriggers != 0 ||
            room.GetMetatile(retractableChest) != retractableOriginal ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(SoundId.SndPoof) != 2 ||
            _entities.Entities<RetractableTriggerChestRoomEntity>().Count != 1,
            "Room 4:7a's $21:$17 did not retract to the source tile without another solve cue.");

        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(4, 0x0c);
        room = _currentRoom;
        Vector2 door = new(0x78, 0x08);
        // The ROM-backed trigger fixture owns approach/countdown/script
        // timing. Keep the distinct Godot mapping-pixel check with a declared
        // completed producer: only the original shutter remains, count zero.
        _entities.Clear();
        var graphicDoor = new DungeonDoorRoomEntity(
            database.GetRoomRecords(4,0x0c).Single(row => row.Id == 0x1e),room,database,
            () => _entities.RoomEnemyCount,_entities.TriggerIsActive,_entities.ToScreen,
            () => (long)_animationTicks,_sound.PlaySound,default,true,_rooms.TrySetTile);
        _entities.AddEntity(graphicDoor);
        for (int frame = 0; SomariaPrivate<DoorState>(graphicDoor,"_state") != DoorState.ReadyToOpen && frame < 24; frame++)
            Step();
        FailIf(SomariaPrivate<DoorState>(graphicDoor,"_state") != DoorState.ReadyToOpen,
            "The declared solved shutter must reach its first mapping-interleave update.");

        // OracleWorldData caches mutable room instances; use an isolated world
        // so preparing the open mapping cannot alter the active door state.
        OracleRoomData reference = new OracleWorldData().LoadRoom(4, 0x0c);
        Vector2 topLeftSample = door + new Vector2(-4, -4);
        Vector2 bottomLeftSample = door + new Vector2(-4, 4);
        Color expectedClosedBottom = reference.GetRenderedPixelForValidation(
            new Vector2I((int)bottomLeftSample.X, (int)bottomLeftSample.Y));
        FailIf(
            !reference.ReplaceMetatile(door, 0x78, 0xa0, (long)_animationTicks),
            "Could not prepare the 4:0c open-door reference tile.");
        Color expectedOpenBottom = reference.GetRenderedPixelForValidation(
            new Vector2I((int)bottomLeftSample.X, (int)bottomLeftSample.Y));

        Step();
        Color actualInterleavedTop = room.GetRenderedPixelForValidation(
            new Vector2I((int)topLeftSample.X, (int)topLeftSample.Y));
        Color actualInterleavedBottom = room.GetRenderedPixelForValidation(
            new Vector2I((int)bottomLeftSample.X, (int)bottomLeftSample.Y));
        FailIf(
            room.GetMetatile(door) != 0xa0 || !room.IsSolid(door) ||
            !actualInterleavedTop.IsEqualApprox(expectedClosedBottom) ||
            !actualInterleavedBottom.IsEqualApprox(expectedOpenBottom) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
            "Room 4:0c did not install the type-0 mapping-interleaved, still-solid " +
            $"door frame: tile=${room.GetMetatile(door):x2}, solid={room.IsSolid(door)}, " +
            $"top={actualInterleavedTop}/{expectedClosedBottom}, " +
            $"bottom={actualInterleavedBottom}/{expectedOpenBottom}, " +
            $"SND_DOORCLOSE={_sound.PlayRequestsFor(SoundId.SndDoorClose)}.");

        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        FailIf(
            !room.IsSolid(door) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 1,
            "Room 4:0c finalized the shutter before six interleaved updates elapsed.");
        Step();
        FailIf(
            room.GetMetatile(door) != 0xa0 || room.IsSolid(door) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2,
            "Room 4:0c did not finalize open tile $a0 and SND_DOORCLOSE on update 6.");

        // replaceShutterForLinkEntering temporarily opens only the shutter at
        // Link's incoming packed position. Destination objects remain frozen
        // throughout the scroll; afterward $1e:$0b waits until Link no longer
        // overlaps its $08/$0a radii before beginning the six-update close.
        _player.WarpTo(new Vector2(0xe8, 0x58));
        _sound.ClearPlayRequestAudit();
        _transitions.BeginScroll(_player, Vector2I.Right, 0x0b);
        OracleRoomData scrollingRoom40b = _world.LoadRoom(4, 0x0b);
        Vector2 scrollingUpDoor = new(0x78, 0x08);
        Vector2 scrollingLeftDoor = new(0x08, 0x58);
        FailIf(
            scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            scrollingRoom40b.GetMetatile(scrollingUpDoor) != 0x78 ||
            !scrollingRoom40b.IsSolid(scrollingUpDoor),
            "Room 4:0b scrolling preload did not open only Link's left-entry shutter.");
        Step();
        FailIf(
            scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
            "Room 4:0b's incoming left shutter advanced while destination entities were frozen.");
        _transitions.UpdateScroll(1.0);
        FailIf(
            scrollingRoom40b.GetPackedPosition(_player.Position) != 0x50,
            $"Room 4:0b's left scroll ended at packed position " +
            $"${scrollingRoom40b.GetPackedPosition(_player.Position):x2} instead of $50.");

        Step();
        Step();
        _player.WarpTo(new Vector2(0x17, 0x58), recordSafe: false);
        Step(); // Enemy branch and callscript each yield before contact.
        Step();
        Step();
        FailIf(
            scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
            "Room 4:0b closed its left shutter while Link still overlapped the strict 16-pixel boundary.");
        _player.WarpTo(new Vector2(0x18, 0x58), recordSafe: false);
        Step();
        Step(); // Successful contact check yields before the respawn helper.
        FailIf(
            scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0 ||
            _player.LocalRespawnPosition != new Vector2(0x18, 0x58),
            "Room 4:0b did not move Link's local respawn inward while deferring the close animation.");
        Step(); // jumpifnoenemies
        Step(); // setstate3
        Step();
        FailIf(
            scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
            "Room 4:0b did not begin its non-solid interleaved close after Link cleared the left doorway.");
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        FailIf(
            scrollingRoom40b.IsSolid(scrollingLeftDoor),
            "Room 4:0b made the incoming shutter solid before six close updates elapsed.");
        Step();
        FailIf(
            scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0x7b ||
            !scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2,
            "Room 4:0b did not finalize closed left tile $7b and collision on update 6.");

        // Room 4:0b proves the same controller handles multiple orientations
        // and ordinary live combat enemies rather than depending on room 4:0c.
        IReadOnlyList<RoomObjectRecord> room40bObjects =
            new EnemyDatabase().GetRoomObjects(4, 0x0b);
        FailIf(
            room40bObjects is not
            [{
                Kind: RoomObjectKind.RandomEnemy,
                Id: 0x43,
                SubId: 0x00,
                Flags: 0x60,
                Count: 3,
                ConditionMask: 0xff
            }],
            "Room 4:0b's Gel stream or always-active predicate diverged from obj_RandomEnemy $60 $43 $00.");
        LoadValidationRoom(4, 0x0b);
        room = _currentRoom;
        _sound.ClearPlayRequestAudit();
        FailIf(
            _entities.Entities<DungeonDoorRoomEntity>().Select(value => value.SubId)
                .ToArray() is not [0x08, 0x0b] ||
            _entities.Entities<GelCharacter>().Count != 3,
            "Room 4:0b did not reuse the up/left enemy shutters with its three Gels.");
        Step();
        FailIf(
            room.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
            room.GetMetatile(new Vector2(0x08, 0x58)) != 0x7b,
            "Room 4:0b shutters opened while live Gel enemies remained.");
        GelCharacter[] room40bGels = _entities.Entities<GelCharacter>().ToArray();
        for (int index = 0; index < room40bGels.Length; index++)
        {
            GelCharacter gel = room40bGels[index];
            FailIf(
                !_entities.ApplySwordHit(gel.CollisionBounds, gel.Position) ||
                _entities.Entities<GelCharacter>().Count != room40bGels.Length - index - 1,
                $"Room 4:0b Gel {index + 1} did not die through the shared sword/combat path.");
            Step();
            FailIf(
                _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0 ||
                room.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
                room.GetMetatile(new Vector2(0x08, 0x58)) != 0x7b,
                "Room 4:0b shutters released before every counted Gel " +
                "death puff decremented wNumEnemies.");
        }
        for (int frame = 0;
             frame < 20 &&
             _entities.Entities<EnemyDeathPuffEffect>().Count != 0;
             frame++)
        {
            Step();
        }
        FailIf(
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0 ||
            _entities.RoomEnemyCount != 0 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "Room 4:0b's two shutters must observe the final PART_ENEMY_DESTROYED count release in the same update's later interaction pass.");
        Step();
        FailIf(_sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 2,
            "Room4:0b must play both solve commands after the count-check update.");
        Step(); // Load wait8.
        for (int frame = 0; frame < database.SolveWait; frame++)
            Step();
        Step();
        FailIf(
            room.GetMetatile(new Vector2(0x78, 0x08)) != 0xa0 ||
            room.GetMetatile(new Vector2(0x08, 0x58)) != 0xa0 ||
            !room.IsSolid(new Vector2(0x78, 0x08)) ||
            !room.IsSolid(new Vector2(0x08, 0x58)) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2,
            "Room 4:0b did not begin both directional interleaved door frames together.");
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        FailIf(
            room.IsSolid(new Vector2(0x78, 0x08)) ||
            room.IsSolid(new Vector2(0x08, 0x58)) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 4,
            "Room 4:0b did not finish both reusable enemy shutters after six updates.");

        // wEnemiesKilledList retains each source object's one-based index for
        // the last eight visited room IDs. A short re-entry therefore omits
        // all three direct Gels; the source layout is reloaded closed, then
        // both zero-count shutters open without SND_SOLVEPUZZLE.
        _entities.LoadRoom(4, _world.LoadRoom(4, 0x0c));
        _sound.ClearPlayRequestAudit();
        _entities.LoadRoom(4, room);
        FailIf(
            _entities.Entities<GelCharacter>().Count != 0 ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 2 ||
            room.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
            room.GetMetatile(new Vector2(0x08, 0x58)) != 0x7b,
            "Room 4:0b short re-entry did not suppress its defeated Gel indices and restore both source shutters.");
        Step();
        FailIf(
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "Room 4:0b replayed SND_SOLVEPUZZLE for a zero-count re-entry.");
        Step(); // setangle
        Step(); // jumpifnoenemies
        Step(); // setstate2
        Step();
        FailIf(
            !room.IsSolid(new Vector2(0x78, 0x08)) ||
            !room.IsSolid(new Vector2(0x08, 0x58)) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2,
            "Room 4:0b did not begin its re-entry door animation on update5.");
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        FailIf(
            room.IsSolid(new Vector2(0x78, 0x08)) ||
            room.IsSolid(new Vector2(0x08, 0x58)) ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0 ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 4,
            "Room 4:0b did not finish its no-solve-cue re-entry shutters after six updates.");

        var recentDefeats = new RecentEnemyDefeats();
        recentDefeats.BeginRoom(0x0b);
        recentDefeats.MarkKilled(1);
        for (int roomId = 0x20; roomId < 0x27; roomId++)
            recentDefeats.BeginRoom(roomId);
        recentDefeats.BeginRoom(0x0b);
        FailIf(
            !recentDefeats.WasKilled(1),
            "wEnemiesKilledList did not retain room 4:0b across seven other visited room IDs.");
        recentDefeats.BeginRoom(0x27);
        recentDefeats.BeginRoom(0x0b);
        FailIf(
            recentDefeats.WasKilled(1),
            "wEnemiesKilledList did not evict room 4:0b at its original eight-entry ring boundary.");

        // The six room 5:93 Keese carry enemy-object flag $02. The parser
        // immediately subtracts them from wNumEnemies, so they must not hold
        // its right shutter closed even while their combat entities are live.
        LoadValidationRoom(5, 0x93);
        room = _currentRoom;
        _sound.ClearPlayRequestAudit();
        Vector2 countExemptDoor = new(0xe8, 0x88);
        FailIf(
            _entities.Entities<KeeseCharacter>().Count != 6 ||
            _entities.Entities<DungeonDoorRoomEntity>() is not [{ SubId: 0x09 }] ||
            room.GetMetatile(countExemptDoor) != 0x79,
            "Room 5:93 did not load six count-exempt Keese and right shutter $1e:$09.");
        Step();
        Step();
        Step();
        Step();
        Step();
        FailIf(
            room.GetMetatile(countExemptDoor) != 0xa0 ||
            !room.IsSolid(countExemptDoor) ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "Enemy flag $02 incorrectly held room 5:93's shutter in wNumEnemies.");

        // Room 4:06 combines two ordinary Stalfos with a $13:$01 push trigger.
        // Entering upward through its down shutter preloads only the crossed
        // door as floor, freezes all destination objects during the scroll,
        // and closes after Link passes the inclusive negative16-pixel edge.
        _entities.ClearRecentEnemyDefeats();
        LoadValidationRoom(4, 0x09);
        _player.WarpTo(new Vector2(0x78, 0x08));
        _sound.ClearPlayRequestAudit();
        _entities.WorldToScreen = _transitions.WorldToGameplayScreen;
        _transitions.BeginScroll(_player, Vector2I.Up, 0x06);
        OracleRoomData scrollingRoom406 = _world.LoadRoom(4, 0x06);
        Vector2 room406DownDoor = new(0x78, 0xa8);
        Vector2 room406RightDoor = new(0xe8, 0x88);
        FailIf(
            scrollingRoom406.GetMetatile(room406DownDoor) != 0xa0 ||
            scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0x79 ||
            !scrollingRoom406.IsSolid(room406RightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>() is not
            [
                {
                    SubId: 0x0a,
                    EnteredThroughThisDoor: true,
                    EnemyCompletionSupported: true
                },
                {
                    SubId: 0x09,
                    EnteredThroughThisDoor: false,
                    EnemyCompletionSupported: true
                }
            ] ||
            _entities.Entities<PushBlockTriggerRoomEntity>() is not
                [{ PackedPosition: 0x7a }] ||
            _entities.Entities<StalfosCharacter>().Count != 2,
            "Room 4:06 did not preload only its crossed down shutter with two Stalfos and its push trigger active.");
        int frozenStalfosRandomCalls = _entities.RandomCalls;
        Step();
        FailIf(
            scrollingRoom406.GetMetatile(room406DownDoor) != 0xa0 ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0 ||
            _entities.RandomCalls != frozenStalfosRandomCalls ||
            _entities.Entities<StalfosCharacter>().Any(enemy =>
                enemy.State != StalfosState.Uninitialized),
            "Room 4:06 advanced its incoming door or Stalfos during destination preload.");
        _transitions.UpdateScroll(1.0);
        FailIf(
            scrollingRoom406.GetPackedPosition(_player.Position) != 0xa7,
            $"Room 4:06's upward scroll ended at packed position " +
            $"${scrollingRoom406.GetPackedPosition(_player.Position):x2} instead of $a7.");
        Step();
        Step();
        _player.WarpTo(new Vector2(0x78, 0x98), recordSafe: false);
        Step();
        Step();
        Step();
        FailIf(
            scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
            "Room 4:06 closed its down shutter before Link stepped fully inside.");
        _player.WarpTo(new Vector2(0x78, 0x97), recordSafe: false);
        Step();
        Step();
        FailIf(
            scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0 ||
            _player.LocalRespawnPosition != new Vector2(0x78, 0x98),
            "Room 4:06 did not accept Link beyond the inclusive negative16-pixel edge.");
        Step();
        Step();
        Step();
        FailIf(
            scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
            "Room 4:06 did not begin its non-solid interleaved down-door close after Link cleared it.");
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        FailIf(
            scrollingRoom406.IsSolid(room406DownDoor),
            "Room 4:06 made its incoming down shutter solid before six close updates elapsed.");
        Step();
        FailIf(
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.GetMetatile(room406DownDoor) != 0x7a ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2,
            "Room 4:06 did not finish the delayed down-shutter close after six updates.");

        // Both Stalfos die to one level-1 sword hit. Their deaths leave the
        // synthetic push-trigger enemy alive, so neither shutter can open
        // until the all-direction source block is moved and its 30-update delay ends.
        _sound.ClearPlayRequestAudit();
        StalfosCharacter[] room406Stalfos =
            _entities.Entities<StalfosCharacter>().ToArray();
        for (int index = 0; index < room406Stalfos.Length; index++)
        {
            StalfosCharacter enemy = room406Stalfos[index];
            FailIf(
                !_entities.ApplySwordHit(
                    enemy.CollisionBounds.Grow(1.0f),
                    enemy.Position + Vector2.Down * 16.0f) ||
                !enemy.PendingKnockbackDeath ||
                _entities.Entities<StalfosCharacter>().Count !=
                    room406Stalfos.Length - index ||
                _sound.PlayRequestsFor(SoundId.SndKillEnemy) != index,
                $"Room 4:06 Stalfos {index + 1} did not begin lethal sword recoil.");
            for (int frame = 0;
                frame < 0x08 && enemy.KnockbackCounter > 0;
                frame++)
            {
                enemy.UpdateFrame(_player.Position);
            }
            enemy.UpdateFrame(_player.Position);
            _entities.Update(0.0, _player);
            FailIf(
                _entities.Entities<StalfosCharacter>().Count !=
                    room406Stalfos.Length - index - 1 ||
                _sound.PlayRequestsFor(SoundId.SndKillEnemy) != index + 1,
                $"Room 4:06 Stalfos {index + 1} did not die through the " +
                "shared post-recoil death-puff path.");
            Step();
        }
        Vector2 room406Block = new(0xa8, 0x78);
        FailIf(
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 2 ||
            _entities.RoomEnemyCount != 3 ||
            scrollingRoom406.GetMetatile(room406Block) != 0x1d ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0 ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            !scrollingRoom406.IsSolid(room406RightDoor),
            "Room 4:06 did not retain its disabled block and closed " +
            "shutters while two counted Stalfos death puffs remained: " +
            $"triggers={_entities.Entities<PushBlockTriggerRoomEntity>().Count}, " +
            $"Stalfos={_entities.Entities<StalfosCharacter>().Count}, " +
            $"puffs={_entities.Entities<EnemyDeathPuffEffect>().Count}, " +
            $"roomCount={_entities.RoomEnemyCount}, " +
            $"block=${scrollingRoom406.GetMetatile(room406Block):x2}, " +
            $"solve={_sound.PlayRequestsFor(SoundId.SndSolvePuzzle)}, " +
            $"downSolid={scrollingRoom406.IsSolid(room406DownDoor)}, " +
            $"rightSolid={scrollingRoom406.IsSolid(room406RightDoor)}.");
        for (int frame = 0;
             frame < 30 &&
             _entities.Entities<EnemyDeathPuffEffect>().Count != 0;
             frame++)
        {
            Step();
        }
        FailIf(
            _entities.Entities<EnemyDeathPuffEffect>().Count != 0 ||
            _entities.RoomEnemyCount != 1 ||
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1 ||
            scrollingRoom406.GetMetatile(room406Block) != 0x1c ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "Room 4:06 must retain its push-block sentinel count and restore the source all-direction block in the interaction pass after both terminal PART updates.");

        var pushableTiles = new PushableTileDatabase();
        FailIf(
            !pushableTiles.TryGet(
                scrollingRoom406.ActiveCollisions,
                scrollingRoom406.GetMetatile(room406Block),
                out PushableTileRecord room406BlockRecord) ||
            !room406BlockRecord.AllowsEveryDirection,
            "Room 4:06's restored source block was not the original all-direction pushable tile `$1c: " +
            $"mode={scrollingRoom406.ActiveCollisions}, " +
            $"tile=${scrollingRoom406.GetMetatile(room406Block):x2}, " +
            $"parameter=${room406BlockRecord.InteractionParameter:x2}, " +
            $"all={room406BlockRecord.AllowsEveryDirection}, " +
            $"direction={room406BlockRecord.RequiredDirection}.");
        Vector2 linkBelowBlock = room406Block + Vector2.Down * 10.0f;
        _pushBlocks.UpdatePushAttempt(linkBelowBlock,Vector2I.Up,Vector2.Zero);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _pushBlocks.UpdatePushAttempt(linkBelowBlock, Vector2I.Up, Vector2.Up);
        _pushBlocks.Advance(1.0 / 60.0);
        FailIf(
            !_pushBlocks.Active ||
            _sound.PlayRequestsFor(SoundId.SndMoveBlock) != 1,
            "Room 4:06's upward test push did not start the shared block movement.");

        Step();
        for (int frame = 0; frame < database.PushDelay - 1; frame++)
            Step();
        FailIf(
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "Room 4:06 released its synthetic enemy before the 30-update trigger delay.");
        Step();
        FailIf(
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "Room 4:06's source-ordered doors observed the push trigger before it finished update 30.");
        Step(); // checknoenemies yields before playsound.
        Step();
        FailIf(
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 2,
            "Room 4:06's two shutters did not observe the completed Stalfos/block enemy count.");
        Step(); // wait8 loads its counter on a separate update.
        for (int frame = 0; frame < database.SolveWait; frame++)
            Step();
        Step();
        FailIf(
            scrollingRoom406.GetMetatile(room406DownDoor) != 0xa0 ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0xa0 ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            !scrollingRoom406.IsSolid(room406RightDoor),
            "Room 4:06 did not begin both interleaved openings after its eight-update solve wait.");
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        FailIf(
            scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.IsSolid(room406RightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0,
            "Room 4:06 did not finish opening both solved shutters after six updates.");

        // Exercise the route from 4:07 independently: a leftward scroll must
        // substitute and then close 4:06's right shutter, never the down one.
        _entities.ClearRecentEnemyDefeats();
        LoadValidationRoom(4, 0x07);
        _player.WarpTo(new Vector2(0x08, 0x88));
        _sound.ClearPlayRequestAudit();
        _transitions.BeginScroll(_player, Vector2I.Left, 0x06);
        scrollingRoom406 = _world.LoadRoom(4, 0x06);
        FailIf(
            scrollingRoom406.GetMetatile(room406DownDoor) != 0x7a ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0xa0 ||
            scrollingRoom406.IsSolid(room406RightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>() is not
            [
                {
                    SubId: 0x0a,
                    EnteredThroughThisDoor: false,
                    EnemyCompletionSupported: true
                },
                {
                    SubId: 0x09,
                    EnteredThroughThisDoor: true,
                    EnemyCompletionSupported: true
                }
            ] ||
            _entities.Entities<StalfosCharacter>().Count != 2 ||
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1,
            "Room 4:06 did not preload only its crossed right shutter from room 4:07.");
        Step();
        _transitions.UpdateScroll(1.0);
        FailIf(
            scrollingRoom406.GetPackedPosition(_player.Position) != 0x8e,
            $"Room 4:06's leftward scroll ended at packed position " +
            $"${scrollingRoom406.GetPackedPosition(_player.Position):x2} instead of $8e.");
        Step();
        Step();
        _player.WarpTo(new Vector2(0xd8, 0x88), recordSafe: false);
        Step();
        Step();
        Step();
        FailIf(
            scrollingRoom406.IsSolid(room406RightDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
            "Room 4:06 closed its right shutter while Link still overlapped it.");
        _player.WarpTo(new Vector2(0xd7, 0x88), recordSafe: false);
        Step();
        Step();
        FailIf(
            scrollingRoom406.IsSolid(room406RightDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
            "Room 4:06 did not defer its right-entry close by one update at the strict boundary.");
        Step();
        Step();
        Step();
        FailIf(
            scrollingRoom406.IsSolid(room406RightDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
            "Room 4:06 did not begin its non-solid interleaved right-door close.");
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        FailIf(
            scrollingRoom406.IsSolid(room406RightDoor),
            "Room 4:06 made its incoming right shutter solid before six close updates elapsed.");
        Step();
        FailIf(
            !scrollingRoom406.IsSolid(room406RightDoor) ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0x79 ||
            scrollingRoom406.GetMetatile(room406DownDoor) != 0x7a ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "Room 4:06 did not finish the delayed right-shutter close from room 4:07.");
        _entities.ClearRecentEnemyDefeats();
        _entities.WorldToScreen = static position => position;

        LoadValidationRoom(4, 0x13);
        FailIf(
            _entities.Entities<DungeonDoorRoomEntity>() is not
            [
                { EnemyCompletionSupported: true },
                { EnemyCompletionSupported: true }
            ] ||
            _entities.Entities<PumpkinHeadBoss>().Count != 1 ||
            _currentRoom.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
            _currentRoom.GetMetatile(new Vector2(0x08, 0x78)) != 0x7b,
            "Room 4:13 did not bind both enemy shutters to Pumpkin Head.");
        // Room 4:09 is the canonical one-shot button: PART_BUTTON $09:$00 at
        // $14 sets trigger bit 0 after its state-0 initialization update. Both
        // $1e:$04/$05 doors reach their trigger check after setup/contact
        // script yields, request separate solve cues, and then enter their
        // six-update animation. Releasing Link keeps a one-shot trigger set.
        LoadValidationRoom(4, 0x09);
        room = _currentRoom;
        Vector2 oneShotButton = new(0x48, 0x18);
        Vector2 triggerUpDoor = new(0x78, 0x08);
        Vector2 triggerRightDoor = new(0xe8, 0x58);
        _player.WarpTo(new Vector2(0x78, 0x78));
        _sound.ClearPlayRequestAudit();
        FailIf(
            _entities.Entities<DungeonDoorRoomEntity>().Select(value => value.SubId)
                .ToArray() is not [0x04, 0x05] ||
            _entities.Entities<PushBlockTriggerRoomEntity>() is not
                [{ PackedPosition: 0x2a }] ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ SubId: 0x00, PackedPosition: 0x14, TriggerBit: 0, Reusable: false }] ||
            room.GetMetatile(oneShotButton) != 0x0c ||
            room.GetMetatile(triggerUpDoor) != 0x78 ||
            room.GetMetatile(triggerRightDoor) != 0x79,
            "Room 4:09 did not instantiate its ordered bit-0 button, push trigger, and up/right shutters.");
        Step();
        _player.WarpTo(oneShotButton);
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x01 ||
            _entities.Entities<GroundButtonRoomEntity>().Count != 0 ||
            room.GetMetatile(oneShotButton) != 0x0d ||
            _sound.PlayRequestsFor(SoundId.SndSplash) != 1 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0 ||
            room.GetMetatile(triggerUpDoor) != 0x78 ||
            room.GetMetatile(triggerRightDoor) != 0x79,
            "Room 4:09's one-shot button must latch tile $0d/trigger bit 0 before the interaction pass.");
        _player.WarpTo(new Vector2(0x78, 0x78));
        for (int frame = 0; frame < 5; frame++) Step(); // Script updates3..7.
        FailIf(
            _entities.ActiveTriggers != 0x01 ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 2 ||
            room.GetMetatile(triggerUpDoor) != 0x78 ||
            room.GetMetatile(triggerRightDoor) != 0x79,
            "Room 4:09's two trigger scripts must request solve cues on update7.");
        Step(); // setstate2
        Step();
        FailIf(
            room.GetMetatile(triggerUpDoor) != 0xa0 ||
            room.GetMetatile(triggerRightDoor) != 0xa0 ||
            !room.IsSolid(triggerUpDoor) || !room.IsSolid(triggerRightDoor) ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
            "Room 4:09 did not begin both interleaved openings while retaining collision.");
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        FailIf(
            room.IsSolid(triggerUpDoor) || room.IsSolid(triggerRightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 2 ||
            _entities.ActiveTriggers != 0x01 ||
            _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2,
            "Room 4:09 did not retain its latched trigger and reusable door controllers after opening.");

        // Room 4:22 uses reusable button $80. Its first pressure check rejects
        // the falling Link; grounded contact presses it. The summed radius8
        // excludes exactly +8 pixels, and the right shutter closes again.
        LoadValidationRoom(4, 0x22);
        room = _currentRoom;
        Vector2 reusableButton = new(0xb8, 0x58);
        Vector2 reusableDoor = new(0xe8, 0x58);
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(reusableButton);
        _player.BeginNewGameSlowFall(1);
        Step();
        Step();
        FailIf(
            _entities.ActiveTriggers != 0 ||
            room.GetMetatile(reusableButton) != 0x0c ||
            _sound.PlayRequestsFor(SoundId.SndSplash) != 0,
            "Room 4:22's reusable button accepted airborne Link pressure.");
        _player.EndNewGameSlowFall();
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x01 ||
            room.GetMetatile(reusableButton) != 0x0d ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ SubId: 0x80, TriggerBit: 0, Reusable: true, Pressed: true }] ||
            _sound.PlayRequestsFor(SoundId.SndSplash) != 1,
            "Room 4:22's grounded Link did not press reusable bit-0 button $80.");
        for (int frame = 0; frame < 4; frame++) Step(); // Remaining setup/solve script yields.
        Step();
        Step();
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        FailIf(room.IsSolid(reusableDoor), "Room 4:22's reusable button did not open its right shutter.");
        _player.WarpTo(reusableButton + Vector2.Right * 7.0f);
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x01 || room.GetMetatile(reusableButton) != 0x0d,
            "Room 4:22 released its button inside the strict eight-pixel contact radius.");
        _player.WarpTo(reusableButton + Vector2.Right * 8.0f);
        Step();
        FailIf(
            _entities.ActiveTriggers != 0 || room.GetMetatile(reusableButton) != 0x0c ||
            _sound.PlayRequestsFor(SoundId.SndSplash) != 2,
            "Room 4:22 did not release at the strict eight-pixel boundary with SND_SPLASH.");
        FailIf(
            room.IsSolid(reusableDoor),
            "Room 4:22 closed its shutter in the update that observed trigger release.");
        Step(); // setstate3 yields before interleave starts.
        Step();
        int healthBeforeDoorRespawn = _player.HealthQuarters;
        Vector2 expectedDoorRespawn = _player.LocalRespawnPosition;
        _player.WarpTo(reusableDoor, recordSafe: false);
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        FailIf(
            room.IsSolid(reusableDoor),
            "Room 4:22 applied closed collision before six interleaved updates.");
        Step();
        FailIf(
            room.GetMetatile(reusableDoor) != 0x79 || !room.IsSolid(reusableDoor) ||
            !_player.IsFloorDoorRespawning || _player.FloorDoorRespawnCounter != 0 ||
            !_player.Visible || _player.Position != reusableDoor ||
            _player.HealthQuarters != healthBeforeDoorRespawn,
            "Room 4:22 must close tile $79 and request parameter-2 respawn without moving or hiding Link yet.");
        _player._PhysicsProcess(update); // Consume state02 request.
        FailIf(!_player.Visible || _player.Position != reusableDoor || _player.NativeNormalStateForInteraction,
            "Consuming the shutter request must change state without initializing respawn.");
        _player._PhysicsProcess(update); // Initialize state02.
        FailIf(_player.Visible || _player.Position != expectedDoorRespawn || _player.FloorDoorRespawnCounter != 2,
            "State02 initialization must copy the local anchor and start the two-update wait.");
        _player._PhysicsProcess(update);
        _player._PhysicsProcess(update);
        FailIf(
            !_player.Visible || _player.HealthQuarters != healthBeforeDoorRespawn - 2,
            "Floor-door respawn must reappear after two wait updates with source raw $fc damage.");
        _player.Heal(2);
        _player.WarpTo(new Vector2(0x78, 0x78));

        // An object above reusable button $80 starts the original $1c release
        // counter. The pressed tile is revealed when that object leaves and
        // remains active through 27 clear updates, releasing on update 28.
        LoadValidationRoom(4, 0x22);
        room = _currentRoom;
        _player.WarpTo(new Vector2(0x78, 0x78));
        Step();
        _sound.ClearPlayRequestAudit();
        Vector2 pressureBlock = reusableButton + Vector2.Left * 16.0f;
        room.SetPositionTileAndCollision(
            pressureBlock, 0x1c, null, (long)_animationTicks);
        Vector2 pushFromLeft = pressureBlock + Vector2.Left * 10.0f;
        _pushBlocks.UpdatePushAttempt(pushFromLeft,Vector2I.Right,Vector2.Zero);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _pushBlocks.UpdatePushAttempt(pushFromLeft, Vector2I.Right, Vector2.Right);
        for (int frame = 0; frame < PushBlockController.MoveFrames; frame++)
            _pushBlocks.Advance(update);
        FailIf(
            _pushBlocks.Active || room.GetMetatile(reusableButton) != 0x1d,
            "The shared push-block controller did not place tile $1d over room 4:22's button.");
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x01 ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ Pressed: true, ReleaseCounter: 0x1c }] ||
            room.GetMetatile(reusableButton) != 0x1d ||
            room.GetUnderlyingMetatile(reusableButton) != 0x0d ||
            _sound.PlayRequestsFor(SoundId.SndSplash) != 1,
            "Room 4:22 did not preserve an object above its newly pressed reusable button.");
        // Destination tile $1d is intentionally no longer pushable. Restore
        // the underlying tile here to model a removable pot/Somaria block
        // leaving while preserving the real push-controller pressure path.
        room.SetPositionTileAndCollision(
            reusableButton, room.GetUnderlyingMetatile(reusableButton), null, (long)_animationTicks);
        for (int frame = 0; frame < database.ButtonObjectReleaseDelay - 1; frame++)
            Step();
        FailIf(
            _entities.ActiveTriggers != 0x01 ||
            room.GetMetatile(reusableButton) != 0x0d ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ Pressed: true, ReleaseCounter: 1 }] ||
            _sound.PlayRequestsFor(SoundId.SndSplash) != 1,
            "Reusable object pressure did not retain tile $0d through 27 release updates.");
        Step();
        FailIf(
            _entities.ActiveTriggers != 0 || room.GetMetatile(reusableButton) != 0x0c ||
            _sound.PlayRequestsFor(SoundId.SndSplash) != 2,
            "Reusable object pressure did not release on exact update $1c.");

        // Bits 0-2, not bit 7, choose wActiveTriggers. Room 4:16's second
        // one-shot button therefore sets only bit 1.
        LoadValidationRoom(4, 0x16);
        room = _currentRoom;
        _player.WarpTo(new Vector2(0x78, 0x78));
        _sound.ClearPlayRequestAudit();
        Step();
        GroundButtonRoomEntity bit1Button = _entities.Entities<GroundButtonRoomEntity>()
            .Single(value => value.SubId == 0x01);
        GroundButtonRoomEntity bit0Button = _entities.Entities<GroundButtonRoomEntity>()
            .Single(value => value.SubId == 0x00);
        TriggerChestRoomEntity bit0Chest = _entities.Entities<TriggerChestRoomEntity>().Single();
        byte bit0ChestOriginal = room.GetOriginalMetatile(bit0Chest.Position);
        _player.WarpTo(bit1Button.Position);
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x02 ||
            room.GetMetatile(bit1Button.Position) != 0x0d ||
            room.GetMetatile(bit0Chest.Position) != bit0ChestOriginal ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
            "PART_BUTTON $09:$01 did not select only wActiveTriggers bit 1 or incorrectly activated a bit-0 chest.");
        _player.WarpTo(bit0Button.Position);
        Step();
        FailIf(
            _entities.ActiveTriggers != 0x03 ||
            _entities.Entities<TriggerChestRoomEntity>() is not [{ Counter: 15 }] ||
            room.GetMetatile(bit0Chest.Position) != bit0ChestOriginal ||
            _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1,
            "Room 4:16's bit-0 predicate did not accept trigger state $03 " +
            $"while bit 1 remained set: triggers=${_entities.ActiveTriggers:x2}, " +
            $"controllers={_entities.Entities<TriggerChestRoomEntity>().Count}, " +
            $"counter={_entities.Entities<TriggerChestRoomEntity>().FirstOrDefault()?.Counter}, " +
            $"tile=${room.GetMetatile(bit0Chest.Position):x2}, " +
            $"solve={_sound.PlayRequestsFor(SoundId.SndSolvePuzzle)}.");
        for (int frame = 0; frame < 4; frame++) Step();
        FailIf(_sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 2,
            "Room4:16's shutter must add its solve sound on script update7, after the chest's immediate cue.");
        _entities.WorldToScreen = _transitions.WorldToGameplayScreen;

        GD.Print("Validated all 234 imported enemy-clear-key/chest/switch/button/" +
            "trigger-chest/$13:$01/$1e:$04-$0b " +
            "placements, seven switches, 49 buttons, seven delayed and six retractable trigger chests, " +
            "20 trigger-door records, room 4:08's exact-$01 solve/puff/15-update chest, " +
            "room 4:7a's reusable chest retraction, room 4:09's one-shot bit-0 " +
            "button and dual shutters, room 4:22's reusable strict-radius/airborne/" +
            "28-update object release, reopening/closing, and local door respawn, " +
            "exact/bit predicates, bit selection, room 4:0c's " +
            "ordered push-trigger enemy count and 30/8/6-update boundaries, mapping-level " +
            "half-door animation/collision, sounds, room 4:0b's delayed left-entry close, " +
            "shared up/left Gel shutters, sword path, predicates, transient eight-room " +
            "defeat/re-entry behavior, room 5:93's " +
            "enemy flag-$02 count exemption, and room 4:06's two delayed entry " +
            "closures plus its two-Stalfos/all-direction-block full solve.");
    }

    private void ValidateChests()
    {
        LoadChestValidationRoom();
        _sound.ClearPlayRequestAudit();
        const int chestPosition = 0x51;
        Vector2 chestPoint = new(24, 88);
        FailIf(
            _activeGroup != 0 || _currentRoom.Id != 0x49 ||
            _currentRoom.GetPackedPosition(chestPoint) != chestPosition ||
            _currentRoom.GetMetatile(chestPoint) != 0xf1,
            "The canonical 0:49/$51 30-rupee chest was not available for testing.");

        Image roomImage = _currentRoom.Texture.GetImage();
        int redChestPixels = 0;
        for (int y = 80; y < 96; y++)
        for (int x = 16; x < 32; x++)
        {
            Color pixel = roomImage.GetPixel(x, y);
            if (pixel.R > 0.5f && pixel.G < 0.2f && pixel.B < 0.25f)
                redChestPixels++;
        }
        FailIf(redChestPixels == 0, "Chest $51 did not render with PALH_0f background palette 0.");

        _player.WarpTo(new Vector2(24, 74));
        _player.Face(Vector2I.Down);
        FailIf(
            !TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "It won't open\nfrom this side!" ||
            _sound.PlayRequestsFor(SoundId.SndOpenChest) != 0,
            "Chest $51 did not use TX_510d from the wrong side.");
        _dialogue.Close();

        _player.WarpTo(new Vector2(24, 100));
        _player.Face(Vector2I.Up);
        int rupeesBefore = _player.Rupees;
        FailIf(
            !TryInteract(_player) || !_interactions.ChestRewardActive ||
            _interactions.ChestReward is not { VisualGraphic: 0x2b } ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0 ||
            _sound.PlayRequestsFor(SoundId.SndOpenChest) != 1 ||
            _sound.PlayRequestsFor(SoundId.SndGetItem) != 0,
            "Chest $51 did not open from below into tile $f0.");

        _interactions.Update(31.0 / 60.0, _player);
        FailIf(
            !_interactions.ChestRewardActive || _player.Rupees != rupeesBefore ||
            _sound.PlayRequestsFor(SoundId.SndGetItem) != 0,
            "The chest reward completed before its 32-frame rise.");
        _interactions.Update(1.0 / 60.0, _player);
        FailIf(
            !_interactions.ChestRewardActive || _player.Rupees != rupeesBefore + 30 ||
            !_dialogue.IsOpen || _dialogue.CurrentMessage != "You got\n30 Rupees!\nThat's nice." ||
            _sound.PlayRequestsFor(SoundId.SndGetItem) != 1,
            "TREASURE_OBJECT_RUPEES_04 did not remain visible while showing TX_0005.");

        _dialogue.Close();
        FailIf(!_interactions.ChestRewardActive, "The chest reward disappeared before its textbox closed.");
        _interactions.Update(0.0, _player);
        FailIf(_interactions.ChestRewardActive, "The chest reward remained after its textbox closed.");

        _currentRoom = _world.LoadRoom(0, 0x49);
        _roomView.SetRoom(_currentRoom.Texture);
        FailIf(
            _currentRoom.GetMetatile(chestPoint) != 0xf0 || TryInteract(_player),
            "The opened chest room flag did not persist for the session.");

        OracleRandomState randomBeforeMissingChest =
            _random.CaptureState();
        LoadValidationRoom(4, 0xce);
        const int missingChestPosition = 0x67;
        var chests = new ChestDatabase();
        FailIf(
            chests.TryGet(
            4, 0xce, missingChestPosition, out _),
            "The missing-chest fixture unexpectedly exists in chestData.s.");
        _interactions.ResetChestForTesting(
            4, 0xce, missingChestPosition);
        Vector2 missingChestPoint = new(
            7 * OracleRoomData.MetatileSize + 8,
            6 * OracleRoomData.MetatileSize + 8);
        _player.WarpTo(
            missingChestPoint + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        int missingRupeesBefore = _player.Rupees;
        int missingOpenSounds =
            _sound.PlayRequestsFor(SoundId.SndOpenChest);
        FailIf(
            !TryInteract(_player) ||
            _interactions.ChestReward is not { VisualGraphic: 0x28 } ||
            _currentRoom.GetMetatile(missingChestPoint) != 0xf0 ||
            _sound.PlayRequestsFor(SoundId.SndOpenChest) !=
                missingOpenSounds + 1,
            "getChestData's missing-row `$2800 default did not open as " +
            "TREASURE_OBJECT_RUPEES_00 with source graphic `$28.");
        _interactions.Update(32.0 / 60.0, _player);
        FailIf(
            _player.Rupees != missingRupeesBefore + 1 ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got 1 Rupee!\n...",
            "The missing chest row did not grant `$28:$00's one Rupee " +
            "and TX_0001 message.");
        _dialogue.Close();
        _interactions.Update(0.0, _player);
        _random.RestoreState(randomBeforeMissingChest);

        GD.Print("Validated 0:49/$51 red chest palette, direction, 32-frame reward rise, " +
            "source graphic $2b, SND_OPENCHEST/SND_GETITEM, reward visibility through " +
            "TX_0005, 30 rupees, opened state, imported wrong-side TX_510d, and " +
            "getChestData's missing-row `$2800/TREASURE_OBJECT_RUPEES_00 fallback.");
    }

    private void ValidateBraceletChestAndPushGate()
    {
        BraceletDatabaseRecord braceletData = new BraceletDatabase().Data;
        var chests = new ChestDatabase();
        FailIf(
            !chests.TryGet(5, 0xa6, 0x37, out ChestRecord braceletChest) ||
            braceletChest.TreasureObject != "TREASURE_OBJECT_BRACELET_02" ||
            braceletChest.TreasureId != TreasureId.Bracelet ||
            braceletChest.Parameter != 2,
            "The original 5:a6/$37 chest did not resolve to TREASURE_OBJECT_BRACELET_02.");

        var pushables = new PushableTileDatabase();
        FailIf(
            !pushables.TryGet(2, 0x10, out PushableTileRecord braceletBlock) ||
            !braceletBlock.RequiresBracelet ||
            !braceletBlock.AllowsEveryDirection ||
            braceletBlock.SourceReplacement != 0xa0 ||
            braceletBlock.DestinationTile != 0x10,
            "Collision mode 2 tile $10 did not retain interactable parameter $c0 and pushblock data.");

        var breakables = new BreakableTileDatabase();
        FailIf(
            !breakables.TryGet(2, 0x10, out BreakableTileRecord liftablePot) ||
            !liftablePot.AllowsSource(BreakableTileDatabase.SourceBracelet) ||
            liftablePot.Replacement != 0xa0,
            "Collision mode 2 tile $10 did not import as a bracelet-breakable tile with replacement $a0.");

        LoadValidationRoom(4, 0xce);
        _interactions.ResetChestForTesting(4, 0xce, 0x67, "TREASURE_OBJECT_BRACELET_00");
        Vector2 debugBraceletChest = new(7 * OracleRoomData.MetatileSize + 8, 6 * OracleRoomData.MetatileSize + 8);
        FailIf(
            _currentRoom.GetMetatile(debugBraceletChest) != 0xf1,
            "The debug 4:ce/$67 Power Bracelet chest was not closed.");

        _player.WarpTo(new Vector2(debugBraceletChest.X, debugBraceletChest.Y + 12));
        _player.Face(Vector2I.Up);
        FailIf(
            !TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(debugBraceletChest) != 0xf0,
            "The debug 4:ce/$67 Power Bracelet chest did not open from below.");

        _interactions.Update(32.0 / 60.0, _player);
        FailIf(
            !_inventory.HasTreasure(TreasureId.Bracelet) ||
            _inventory.BraceletLevel != 1 ||
            _inventory.EquippedB != TreasureId.Bracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                "You got the\nPower Bracelet!\nHold the button\n" +
                "and press \\item(0x00)\nto lift heavy\nobjects!"),
            "TREASURE_OBJECT_BRACELET_00 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_0026.");
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        // linkInteractWithAButtonSensitiveObjects/interactWithTileBeforeLink
        // run before checkUseItems. Exercise Player's actual A input path so
        // an equipped Bracelet cannot consume the chest press.
        _interactions.ResetChestForTesting(
            4, 0xce, 0x67, "TREASURE_OBJECT_BRACELET_00");
        _inventory.EquipA(TreasureId.Bracelet);
        _player.WarpTo(new Vector2(
            debugBraceletChest.X, debugBraceletChest.Y + 12));
        _player.Face(Vector2I.Up);
        Input.ActionPress("attack");
        try
        {
            _player._PhysicsProcess(1.0 / 60.0);
        }
        finally
        {
            Input.ActionRelease("attack");
        }
        FailIf(
            !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(debugBraceletChest) != 0xf0 ||
            _bracelet.State != BraceletState.Idle,
            "The 4:ce/$67 chest did not retain A-button priority over an " +
            "equipped ITEM_BRACELET parent.");
        _interactions.Update(32.0 / 60.0, _player);
        _dialogue.Close();
        _interactions.Update(0.0, _player);
        _inventory.EquipB(TreasureId.Bracelet);

        // Exercise a canonical dungeon pot instead of installing `$10 into a
        // tileset position that never uses its graphics. INTERAC_PUSHBLOCK
        // reads the explicit metatile mapping before replacing the source.
        Vector2 movingPotCenter = new(
            7 * OracleRoomData.MetatileSize + 8,
            2 * OracleRoomData.MetatileSize + 8);
        Vector2 linkAboveMovingPot =
            movingPotCenter + Vector2.Up * 10.0f;
        FailIf(
            _currentRoom.GetMetatile(movingPotCenter) != 0x10 ||
            _currentRoom.GetMetatile(
                movingPotCenter + Vector2.Down *
                    OracleRoomData.MetatileSize) != 0xa0,
            "The canonical 4:ce/$27 moving-pot route was not `$10 over " +
            "open `$a0 ground.");
        using Texture2D expectedMovingPot =
            _currentRoom.BuildMimickedMetatileTexture(0x10);
        using Image expectedMovingPotImage = expectedMovingPot.GetImage();
        _playerWorld.UpdatePushableBlocks(linkAboveMovingPot,Vector2I.Down,Vector2.Zero);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _playerWorld.TilePushingDirection = 2; // Declared preceding graphics-pass contact.
            _playerWorld.UpdatePushableBlocks(
                linkAboveMovingPot, Vector2I.Down, Vector2.Down);
        }
        _pushBlocks.Advance(1.0 / 60.0);
        Texture2D? movingPotTexture = _pushBlocks.BlockTexture;
        FailIf(
            !_pushBlocks.Active ||
            movingPotTexture is null,
            "The canonical moving pot did not initialize its metatile-mimic texture.");
        using Image movingPotImage = movingPotTexture.GetImage();
        int movingPotTransparentPixels = 0;
        int movingPotOpaquePixels = 0;
        for (int y = 0; y < movingPotImage.GetHeight(); y++)
        for (int x = 0; x < movingPotImage.GetWidth(); x++)
        {
            float alpha = movingPotImage.GetPixel(x, y).A;
            if (alpha < 0.01f)
                movingPotTransparentPixels++;
            if (alpha > 0.99f)
                movingPotOpaquePixels++;
        }
        ulong movingPotPixelHash =
            OracleGraphicsCache.PixelHash(movingPotImage);
        ulong expectedMovingPotPixelHash =
            OracleGraphicsCache.PixelHash(expectedMovingPotImage);
        FailIf(
            movingPotTransparentPixels == 0 ||
            movingPotOpaquePixels == 0 ||
            movingPotPixelHash != expectedMovingPotPixelHash,
            "INTERAC_PUSHBLOCK did not render moving pot `$10 as an " +
            "objectMimicBgTile texture with a transparent ground border " +
            $"(transparent={movingPotTransparentPixels}, " +
            $"opaque={movingPotOpaquePixels}, " +
            $"actual={movingPotPixelHash:x16}, " +
            $"expected={expectedMovingPotPixelHash:x16}).");
        _pushBlocks.Cancel();

        // Reload before the independent lifting/presentation checks.
        LoadValidationRoom(4, 0xce);

        Vector2 fixedWallCenter = new(
            2 * OracleRoomData.MetatileSize + 8,
            OracleRoomData.MetatileSize / 2);
        FailIf(
            _currentRoom.GetMetatile(fixedWallCenter) != 0xb0,
            "The 4:ce/$02 unbreakable Bracelet wall was not tile $b0.");
        _player.WarpTo(fixedWallCenter + Vector2.Down * 10);
        _player.Face(Vector2I.Up);
        FailIf(
            !_playerWorld.TryUseBracelet(_player, primaryButton: false) ||
            _bracelet.State != BraceletState.GrabbingWall,
            "ITEM_BRACELET did not grab the unbreakable 4:ce/$02 wall.");
        for (int frame = 0; frame < 23; frame++)
        {
            FailIf(
                !_playerWorld.UpdateBracelet(
                _player, Vector2.Down,
                primaryHeld: false, secondaryHeld: true,
                itemButtonJustPressed: false),
                "ITEM_BRACELET released the unbreakable 4:ce/$02 wall while pulling.");
        }
        FailIf(
            _bracelet.State != BraceletState.GrabbingWall ||
            _bracelet.Counter != braceletData.GrabPullFrames ||
            _currentRoom.GetMetatile(fixedWallCenter) != 0xb0,
            "Failed tryToBreakTile retries restarted LINK_ANIM_MODE_LIFT_3 " +
            "instead of holding its terminal strain frame.");
        FailIf(
            _playerWorld.UpdateBracelet(
                _player, Vector2.Zero,
                primaryHeld: false, secondaryHeld: false,
                itemButtonJustPressed: false) ||
            _bracelet.State != BraceletState.Idle,
            "Releasing ITEM_BRACELET did not clear the unbreakable wall grab.");

        // Model a moving pot after it has been pushed over the decorated
        // non-solid `$a1 floor at 4:ce/$2c. breakableTiles.s restores the
        // original room-layout metatile for collision-set-1/2 tile `$10
        // instead of blindly installing the mode's generic `$a0 replacement.
        Vector2 liftPoint = new(
            12 * OracleRoomData.MetatileSize + 8,
            2 * OracleRoomData.MetatileSize + 8);
        byte liftGround = _currentRoom.GetMetatile(liftPoint);

        ulong RenderedLiftTileHash()
        {
            using Texture2D tileTexture =
                _currentRoom.BuildMimickedMetatileTexture(liftPoint);
            using Image tileImage = tileTexture.GetImage();
            return OracleGraphicsCache.PixelHash(tileImage);
        }

        ulong liftGroundPixelHash = RenderedLiftTileHash();
        FailIf(
            liftGround != 0xa1 ||
            _currentRoom.GetCollision(liftGround) != 0 ||
            liftablePot.ReplacementFor(_currentRoom, liftPoint) != liftGround ||
            !_currentRoom.ReplaceMetatile(
                liftPoint, liftGround, 0x10, (long)_animationTicks),
            "Could not prepare a moved dungeon pot over 4:ce/$2c's " +
            "original non-solid `$a1 ground.");
        ulong potPixelHash = RenderedLiftTileHash();
        FailIf(
            potPixelHash == liftGroundPixelHash ||
            potPixelHash == 0,
            "The moved dungeon pot did not visibly replace its distinct " +
            "original `$a1 ground before the lift " +
            $"(ground={liftGroundPixelHash:x16}, pot={potPixelHash:x16}, " +
            $"tile=${_currentRoom.GetMetatile(liftPoint):x2}).");
        // The original parent requires both $c0 top-edge wall bits. Link's
        // collision endpoint sits ten pixels below this metatile center.
        _player.WarpTo(new Vector2(liftPoint.X, liftPoint.Y + 10));
        _player.Face(Vector2I.Up);
        _sound.ClearPlayRequestAudit();
        FailIf(
            !_playerWorld.TryUseBracelet(_player, primaryButton: false) ||
            _bracelet.State != BraceletState.GrabbingWall ||
            _currentRoom.GetMetatile(liftPoint) != 0x10 ||
            RenderedLiftTileHash() != potPixelHash,
            "Equipped Bracelet did not enter its held-button wall-grab " +
            $"state without removing or redrawing the pot (collision=" +
            $"${_currentRoom.GetCollision(0x10):x2}, " +
            $"left={_currentRoom.IsSolid(_player.Position + new Vector2(-3, -3))}, " +
            $"right={_currentRoom.IsSolid(_player.Position + new Vector2(2, -3))}, " +
            $"state={_bracelet.State}).");
        for (int frame = 0; frame < 10; frame++)
        {
            FailIf(
                !_playerWorld.UpdateBracelet(
                    _player, Vector2.Down,
                    primaryHeld: false, secondaryHeld: true,
                    itemButtonJustPressed: false) ||
                _currentRoom.GetMetatile(liftPoint) != 0x10 ||
                RenderedLiftTileHash() != potPixelHash,
                "Bracelet removed or redrew the pot before " +
                "LINK_ANIM_MODE_LIFT_3 reached its 11-update pull boundary.");
        }
        FailIf(
            !_playerWorld.UpdateBracelet(
                _player, Vector2.Down,
                primaryHeld: false, secondaryHeld: true,
                itemButtonJustPressed: false) ||
            _bracelet.State != BraceletState.Lifting ||
            _currentRoom.GetMetatile(liftPoint) != liftGround ||
            RenderedLiftTileHash() != liftGroundPixelHash ||
            _sound.PlayRequestsFor(SoundId.SndPickup) != 1 ||
            _bracelet.LiftedObject is null ||
            !_player.BraceletLiftCollisionsDisabled,
            "Bracelet did not restore the moved pot's visible original `$a1 " +
            "ground, request SND_PICKUP, and disable Link collisions at the " +
            "native pull boundary.");
        using (Image liftedImage = _bracelet.LiftedObject.Texture.GetImage())
        {
            bool foundTransparent = false;
            bool foundOpaque = false;
            for (int y = 0; y < liftedImage.GetHeight(); y++)
            for (int x = 0; x < liftedImage.GetWidth(); x++)
            {
                float alpha = liftedImage.GetPixel(x, y).A;
                foundTransparent |= alpha < 0.01f;
                foundOpaque |= alpha > 0.99f;
            }
            FailIf(
                !foundTransparent || !foundOpaque ||
                OracleGraphicsCache.PixelHash(liftedImage) !=
                    potPixelHash,
                "Bracelet itemMimicBgTile output did not preserve opaque " +
                "pot pixels while making source color 0 transparent.");
        }
        for (int frame = 0; frame < 12; frame++)
        {
            FailIf(
                !_playerWorld.UpdateBracelet(
                _player, Vector2.Zero,
                primaryHeld: false, secondaryHeld: false,
                itemButtonJustPressed: false),
                "Bracelet re-enabled Link before the 13-update LINK_ANIM_MODE_LIFT_4/LIFT sequence finished.");
        }
        FailIf(
            _playerWorld.UpdateBracelet(
                _player, Vector2.Zero,
                primaryHeld: false, secondaryHeld: false,
                itemButtonJustPressed: false) ||
            !_bracelet.HoldingTile || !_player.IsCarryingObject ||
            _bracelet.LiftedObject?.GetParent() != _player ||
            _player.BraceletLiftCollisionsDisabled,
            "Bracelet did not re-enable Link collisions and enter the " +
            "carried-object walk pose after the native lift sequence.");

        FailIf(
            !_playerWorld.UpdateBracelet(
                _player, Vector2.Zero,
                primaryHeld: false, secondaryHeld: true,
                itemButtonJustPressed: true) ||
            _bracelet.State != BraceletState.Throwing ||
            _bracelet.LiftedObject is not
                { Thrown: true, SpeedRaw: ObjectSpeed.Speed0, SpeedZ: 0x1c } ||
            _sound.PlayRequestsFor(SoundId.SndThrow) != 1 ||
            _player.IsCarryingObject,
            "Bracelet did not preserve wLinkAngle=$ff as an in-place " +
            "weight-0 drop with SND_THROW and Link's throw pose.");
        BraceletLiftedObject thrown = _bracelet.LiftedObject ??
            throw new InvalidOperationException(
                "ITEM_BRACELET lost its tile immediately after throw setup.");
        Vector2 groundCenter =
            OracleObjectMath.ToPixelPosition(thrown.GroundPosition);
        FailIf(
            thrown.ThrowDirection != Vector2I.Zero ||
            !thrown.CollisionBounds(
                braceletData.RadiusX,
                braceletData.RadiusY).GetCenter().IsEqualApprox(groundCenter) ||
            Mathf.IsEqualApprox(thrown.Position.Y, groundCenter.Y),
            "Thrown ITEM_BRACELET did not keep its yh/xh collision center " +
            "separate from the airborne zh draw offset.");
        for (int frame = 0;
             frame < 80 && _bracelet.State != BraceletState.Idle;
             frame++)
        {
            _playerWorld.UpdateBracelet(
                _player, Vector2.Zero,
                primaryHeld: false, secondaryHeld: false,
                itemButtonJustPressed: false);
        }
        FailIf(
            _bracelet.State != BraceletState.Idle ||
            _bracelet.LiftedObject is not null ||
            _entities.Entities<RockDebrisEffect>().Count != 1,
            "Thrown Bracelet tile did not break into its stored INTERAC_ROCKDEBRIS effect.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _sound.PlayRequestsFor(SoundId.SndBreakRock) != 1,
            "Thrown Bracelet tile's INTERAC_ROCKDEBRIS did not request SND_BREAK_ROCK.");


        FailIf(
            RoomEntityManager.ObjectCollisionZOverlaps(
                targetZ: 0,
                itemZ: -braceletData.CollisionZRadius,
                radius: braceletData.CollisionZRadius) ||
            !RoomEntityManager.ObjectCollisionZOverlaps(
                targetZ: 0,
                itemZ: 1 - braceletData.CollisionZRadius,
                radius: braceletData.CollisionZRadius) ||
            !RoomEntityManager.ObjectCollisionZOverlaps(
                targetZ: -braceletData.CollisionZRadius,
                itemZ: 0,
                radius: braceletData.CollisionZRadius) ||
            RoomEntityManager.ObjectCollisionZOverlaps(
                targetZ: -braceletData.CollisionZRadius - 1,
                itemZ: 0,
                radius: braceletData.CollisionZRadius),
            "ITEM_BRACELET did not preserve collisionEffects.s's one-byte " +
            "$0e/$07 enemy/item zh window.");

        LoadValidationRoom(5, 0xa6);
        _interactions.ResetChestForTesting(5, 0xa6, 0x37);
        Vector2 chestPoint = new(7 * OracleRoomData.MetatileSize + 8, 3 * OracleRoomData.MetatileSize + 8);
        FailIf(
            _currentRoom.GetMetatile(chestPoint) != 0xf1,
            "The original 5:a6/$37 Power Glove chest was not closed.");

        _player.WarpTo(new Vector2(chestPoint.X, chestPoint.Y + 12));
        _player.Face(Vector2I.Up);
        FailIf(
            !TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0,
            "The 5:a6/$37 Power Glove chest did not open from below.");

        _interactions.Update(32.0 / 60.0, _player);
        FailIf(
            !_inventory.HasTreasure(TreasureId.Bracelet) ||
            _inventory.BraceletLevel != 2 ||
            _inventory.EquippedB != TreasureId.Bracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got the\nPower Glove!\nYou can now lift\nheavy objects.",
            "TREASURE_OBJECT_BRACELET_02 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_002f.");
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        GD.Print("Validated debug Power Bracelet chest TREASURE_OBJECT_BRACELET_00, " +
            "A-button chest priority, terminal unbreakable-wall strain, 11-update pull, " +
            "moving-pot original-ground retention, metatile-mimic lift, " +
            "13-update carry pose, angle-$ff in-place " +
            "drop and directional weight-0 throw/debris, ground-space " +
            "Y/X plus strict seven-pixel Z enemy collision, " +
            "damage-release angle $ff with knockback-independent gravity, " +
            "SND_PICKUP/SND_THROW, transparent objectMimicBgTile " +
            "pot rendering, original Power Glove upgrade, " +
            "and bracelet-required pushblock tile $10.");
    }
}
