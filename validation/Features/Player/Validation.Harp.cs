using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private const double HarpFrame = 1.0 / 60.0;

    private void ValidateHarp()
    {
        byte[] originalSave = new byte[OracleSaveData.FileSize];
        _saveData.ReadWramBytes(0xc5b0, originalSave);
        OracleRandomValidationSnapshot originalRandom =
            CaptureOracleRandomForValidation();
        ValidateHarpInventorySubmenu();

        HarpItemRecord record = _harp.Database.Record;
        FailIf(
            record is not
            {
                Item: InventoryState.ItemHarp,
                HarpTreasure: TreasureDatabase.TreasureHarp,
                EchoesTreasure: TreasureDatabase.TreasureTuneOfEchoes,
                CurrentsTreasure: TreasureDatabase.TreasureTuneOfCurrents,
                AgesTreasure: TreasureDatabase.TreasureTuneOfAges,
                SongFrames: 260,
                EmptySongFrames: 261,
                NoteInterval: 32,
                ProhibitedTilesetMask: 0x7e,
                PastMask: 0x80,
                PortalRoomFlag: OracleSaveData.RoomFlagPortalSpotDiscovered,
                EchoesSound: OracleSoundEngine.SndTuneOfEchoes,
                CurrentsSound: OracleSoundEngine.SndTuneOfCurrents,
                AgesSound: OracleSoundEngine.SndTuneOfAges
            } ||
            _harp.Database.LinkFrames.Length != 17 ||
            !_harp.Database.LinkAnimationParameters.SequenceEqual(
                [0, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0x81, 0xff]),
            "Imported ITEM_HARP mechanics or full Link animation changed.");

        EnsureHarpAndSongs();
        _dialogue.Close();
        _saveData.ClearTimePortalLocation();
        _saveData.SetRoomFlag(
            0, 0x3a,
            OracleSaveData.RoomFlagPortalSpotDiscovered,
            value: false);
        LoadValidationRoom(0, 0x3a);
        TimePortal dormant = _entities.Entities<TimePortal>().Single();
        FailIf(
            dormant.Active || dormant.Visible || dormant.Temporary,
            "Ordinary room 0:3a portal was not dormant before Echoes.");

        int randomBefore = _entities.RandomCalls;
        int notesBefore = _harp.NoteSpawnCount;
        _inventory.SelectHarpSong(1);
        _sound.ClearPlayRequestAudit();
        PlaySelectedHarpSong(validateNoteSides: true);
        FailIf(
            _player.IsUsingHarp || _player.HarpPoseActive ||
            _harp.PlayingSong != 0 || !dormant.Awakening || dormant.Active ||
            !_saveData.HasRoomFlag(
                0, 0x3a, OracleSaveData.RoomFlagPortalSpotDiscovered) ||
            _entities.RandomCalls != randomBefore + 8 ||
            _harp.NoteSpawnCount != notesBefore + 8 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTuneOfEchoes) != 1 ||
            _interactions.DialogueOpen,
            "Tune of Echoes did not run its 260-update/8-note parent, " +
            "mark the dormant portal, and finish without TX_5110.");
        _entities.Update(HarpFrame, _player);
        FailIf(
            !dormant.Active ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopSfx) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndTeleport) != 1,
            "Echoes portal did not activate with STOP_SFX/TELEPORT on " +
            "the first update after instrument playback ended.");

        LoadValidationRoom(0, 0x06);
        _saveData.SetRoomFlag(
            0, 0x06,
            OracleSaveData.RoomFlagPortalSpotDiscovered,
            value: false);
        _dialogue.Close();
        _inventory.SelectHarpSong(1);
        PlaySelectedHarpSong();
        FailIf(
            !_interactions.DialogueOpen || IsTransitioning,
            "Echoes without a portal spot did not show imported TX_5110.");
        _dialogue.Close();

        _inventory.SelectHarpSong(2);
        PlaySelectedHarpSong();
        FailIf(
            !_interactions.DialogueOpen || IsTransitioning,
            "Tune of Currents in the present did not show TX_5110.");
        _dialogue.Close();

        LoadValidationRoom(1, 0x06);
        FailIf(
            (_currentRoom.TilesetFlags & 0x80) == 0 ||
            (_currentRoom.TilesetFlags & 0x7e) != 0,
            "Canonical past room 1:06 is no longer a valid Currents screen.");
        _inventory.SelectHarpSong(2);
        Vector2 currentsPosition = _player.Position;
        int currentsPacked = _currentRoom.GetPackedPosition(currentsPosition);
        PlaySelectedHarpSong();
        FailIf(
            !_transitions.TimeWarpActive,
            "Tune of Currents in the past did not trigger CUTSCENE_TIMEWARP.");
        FinishTimeWarp();
        TimePortal currentsReturn = _entities.Entities<TimePortal>()
            .Single(portal => portal.Temporary);
        FailIf(
            _activeGroup != 0 || _currentRoom.Id != 0x06 ||
            currentsReturn.Position != PackedPoint(currentsPacked) ||
            !currentsReturn.Active ||
            _saveData.TimePortalGroup != 0 ||
            _saveData.TimePortalRoom != 0x06 ||
            _saveData.TimePortalPosition != currentsPacked,
            "Direct Currents warp did not create and persist the destination " +
            "INTERAC_TIMEPORTAL at wWarpDestPos.");

        int initialPalette = currentsReturn.CurrentPalette;
        _entities.Update(2.0 / 60.0, _player);
        FailIf(
            currentsReturn.CurrentPalette == initialPalette,
            "Temporary portal did not cycle its OAM palette on even updates.");
        _player.WarpTo(currentsReturn.Position + new Vector2(24, 0), recordSafe: false);
        _entities.Update(HarpFrame, _player);
        _player.WarpTo(currentsReturn.Position, recordSafe: false);
        _entities.Update(HarpFrame, _player);
        FailIf(
            !_transitions.TimeWarpActive ||
            _saveData.TimePortalGroup != 0xff,
            "Touching the persistent return portal did not clear wPortalGroup " +
            "and begin the paired time warp.");
        FinishTimeWarp();

        LoadValidationRoom(0, 0x06);
        _inventory.SelectHarpSong(3);
        PlaySelectedHarpSong();
        FailIf(!_transitions.TimeWarpActive, "Tune of Ages did not time-warp from the present.");
        FinishTimeWarp();
        FailIf(
            _activeGroup != 1 ||
            !_entities.Entities<TimePortal>().Any(portal => portal.Temporary) ||
            _saveData.TimePortalGroup != 1 ||
            _saveData.TimePortalRoom != 0x06,
            "Tune of Ages did not create its past-era return portal.");

        _saveData.ClearTimePortalLocation();
        LoadValidationRoom(0, 0x06);
        if (_saveData.WriteWramBytes(0xc5b0, originalSave))
            _saveData.CommitInventoryChange();
        RestoreOracleRandomForValidation(originalRandom);
        GD.Print(
            "Validated ITEM_HARP's Tune-of-Echoes inventory award, 2/3-song " +
            "selection submenu, composite inventory/HUD graphics, 260-update " +
            "Link animation, eight parameter-sided shared-RNG floating notes, " +
            "source sounds, object freeze, dormant `$e1:$00 activation and " +
            "room flag, TX_5110 failures, Currents past-only gate, Ages " +
            "bidirectional warp, source tile replacements, and persistent " +
            "palette-cycling INTERAC_TIMEPORTAL return point.");
    }

    private void ValidateHarpInventorySubmenu()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(TreasureDatabase.TreasureHarp, 0);
        inventory.GiveTreasure(TreasureDatabase.TreasureTuneOfEchoes, 0);
        inventory.GiveTreasure(TreasureDatabase.TreasureTuneOfCurrents, 0);
        inventory.GiveTreasure(TreasureDatabase.TreasureTuneOfAges, 0);
        FailIf(
            !inventory.HasTreasure(TreasureDatabase.TreasureHarp) ||
            !inventory.HasTreasure(TreasureDatabase.TreasureTuneOfEchoes) ||
            inventory.SelectedHarpSong != 1,
            "Tune of Echoes did not grant TREASURE_HARP parameter $01.");
        inventory.SwapStorageSlotWithButton(0, isA: false);

        var screen = new InventoryScreen
        {
            Name = "HarpInventoryValidation",
            Visible = false
        };
        AddChild(screen);
        screen.Initialize(_treasures, inventory);
        screen.Open();
        var menu = new InventoryMenuController(
            screen,
            _saveQuitScreen,
            _menuLifecycle,
            () => true,
            () => true,
            () => SaveResult.Succeeded,
            () => { },
            _sound.PlaySound);
        int selectRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem);
        FailIf(
            menu.EquipToAForValidation() ||
            !screen.ItemSubmenuActive ||
            screen.ItemSubmenuReady ||
            screen.ItemSubmenuWidth != 0 ||
            screen.ItemSubmenuHeight != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) !=
                selectRequests,
            "Equipping a three-song Harp did not enter submenu opening " +
            "state without prematurely swapping or playing SND_SELECTITEM.");
        menu.UpdateItemSubmenuForValidation(HarpFrame);
        FailIf(
            screen.ItemSubmenuWidth != 2 ||
            screen.ItemSubmenuHeight != 1 ||
            screen.ItemSubmenuReady,
            "Three-song Harp submenu did not draw its first two columns " +
            "on inventoryMenuState2's first update.");
        for (int update = 1; update < 16; update++)
            menu.UpdateItemSubmenuForValidation(HarpFrame);
        FailIf(screen.ItemSubmenuReady, "Three-song Harp submenu opened before source update 17.");
        menu.UpdateItemSubmenuForValidation(HarpFrame);
        FailIf(
            !screen.ItemSubmenuReady ||
            screen.ItemSubmenuWidth != 10 ||
            screen.ItemSubmenuHeight != 4 ||
            screen.ItemSubmenuIndex != 0,
            "Three-song Harp submenu did not finish its width/height expansion.");
        int moveRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        FailIf(
            !menu.MoveItemSubmenuForValidation(-1) ||
            screen.ItemSubmenuIndex != 2 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) !=
                moveRequests + 1 ||
            !menu.ConfirmItemSubmenuForValidation() ||
            screen.ItemSubmenuActive ||
            inventory.SelectedHarpSong != 3 ||
            inventory.EquippedA != InventoryState.ItemHarp ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) !=
                selectRequests + 1,
            "Harp submenu did not wrap, select Tune of Ages, equip to A, " +
            "and request the source movement/selection sounds.");
        FailIf(
            !OracleSaveData.TryDeserialize(
                save.Serialize(), out OracleSaveData? restored) ||
            new InventoryState(_treasures, restored!).SelectedHarpSong != 3,
            "Selected Harp song did not persist in wSelectedHarpSong.");
        screen.QueueFree();

        var twoSongInventory = new InventoryState(
            _treasures, OracleSaveData.CreateStandardGame());
        twoSongInventory.GiveTreasure(TreasureDatabase.TreasureHarp, 0);
        twoSongInventory.GiveTreasure(TreasureDatabase.TreasureTuneOfEchoes, 0);
        twoSongInventory.GiveTreasure(TreasureDatabase.TreasureTuneOfCurrents, 0);
        // TREASURE_TUNE_OF_ECHOES grants and initially equips ITEM_HARP.
        twoSongInventory.SwapStorageSlotWithButton(0, isA: false);
        var twoSongScreen = new InventoryScreen
        {
            Name = "TwoSongHarpInventoryValidation",
            Visible = false
        };
        AddChild(twoSongScreen);
        twoSongScreen.Initialize(_treasures, twoSongInventory);
        twoSongScreen.Open();
        var twoSongMenu = new InventoryMenuController(
            twoSongScreen,
            _saveQuitScreen,
            _menuLifecycle,
            () => true,
            () => true,
            () => SaveResult.Succeeded,
            () => { },
            _sound.PlaySound);
        FailIf(
            twoSongMenu.EquipToBForValidation(),
            "Equipping a two-song Harp bypassed inventoryMenuState2.");
        for (int update = 0; update < 14; update++)
            twoSongMenu.UpdateItemSubmenuForValidation(HarpFrame);
        FailIf(
            twoSongScreen.ItemSubmenuReady ||
            twoSongScreen.ItemSubmenuWidth != 8 ||
            twoSongScreen.ItemSubmenuHeight != 4,
            "Two-song Harp submenu reached input before source update 15.");
        twoSongMenu.UpdateItemSubmenuForValidation(HarpFrame);
        FailIf(!twoSongScreen.ItemSubmenuReady, "Two-song Harp submenu was not ready on source update 15.");
        twoSongScreen.QueueFree();
    }

    private void EnsureHarpAndSongs()
    {
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureHarp))
            _inventory.GiveTreasure(TreasureDatabase.TreasureHarp, 0);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureTuneOfEchoes))
            _inventory.GiveTreasure(TreasureDatabase.TreasureTuneOfEchoes, 0);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureTuneOfCurrents))
            _inventory.GiveTreasure(TreasureDatabase.TreasureTuneOfCurrents, 0);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureTuneOfAges))
            _inventory.GiveTreasure(TreasureDatabase.TreasureTuneOfAges, 0);
    }

    private void PlaySelectedHarpSong(bool validateNoteSides = false)
    {
        ReadOnlySpan<bool> expectedNoteSides =
            [false, true, true, false, true, true, true, true];
        int firstNoteSerial = _harp.NoteSpawnCount;
        int expectedFrames = _harp.Database.FramesForSong(
            _inventory.SelectedHarpSong);
        _player.StartHarpActionForValidation();
        FailIf(
            !_player.IsUsingHarp || !_player.HarpPoseActive ||
            _harp.PlayingSong != _inventory.SelectedHarpSong,
            "ITEM_HARP did not allocate its parent and Link pose.");
        for (int update = 0; update < expectedFrames; update++)
        {
            _entities.UpdateDuringHarp(HarpFrame, _player);
            _harp.Update(HarpFrame);
            _player.AdvanceHarpForValidation(1);
            if (validateNoteSides && (update + 1) % 32 == 0)
            {
                int noteIndex = (update + 1) / 32 - 1;
                NpcCharacter note = _entities.Entities<NpcCharacter>().Single(
                    actor => actor.Name ==
                        $"PlayableHarpMusicNote{firstNoteSerial + noteIndex}");
                float expectedX = _player.Position.X +
                    (expectedNoteSides[noteIndex] ? 8 : -8);
                FailIf(
                    !Mathf.IsEqualApprox(note.Position.X, expectedX),
                    $"ITEM_HARP note {noteIndex} spawned at x={note.Position.X} " +
                    $"instead of animation-parameter side x={expectedX}.");
            }
            FailIf(
                update + 1 < expectedFrames && !_player.IsUsingHarp,
                $"ITEM_HARP ended on update {update + 1}, before " +
                $"{expectedFrames}.");
        }
    }

    private void FinishTimeWarp()
    {
        for (int update = 0; update < 600 && IsTransitioning; update++)
            UpdateRoomWarpTransition(HarpFrame);
        FailIf(IsTransitioning, "Harp time warp did not finish within its source timing bound.");
    }

    private static Vector2 PackedPoint(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);
}
