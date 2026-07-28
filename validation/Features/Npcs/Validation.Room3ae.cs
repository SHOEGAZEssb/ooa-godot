using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom3aeInteractions()
    {
        const int group = 3;
        const int room = 0xae;
        HarpOfAgesEvent harpEvent = _roomEvents.HarpOfAges;
        HarpOfAgesEventDatabase database = harpEvent.Database;
        HarpOfAgesEventRecord record = database.Record;
        byte originalRoomFlags = _saveData.GetRoomFlags(group, room);
        var inventorySnapshot = new byte[0x39];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);
        MethodInfo? reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Vector2 originalFadePosition = _warpFade.Position;
        Vector2 originalFadeSize = _warpFade.Size;
        int originalFadeZ = _warpFade.ZIndex;
        FailIf(
            reloadInventory is null,
            "Could not resolve InventoryState.LoadFromSaveData for room 3:ae validation.");

        void RestoreRoomFlags(byte flags)
        {
            foreach (byte bit in new byte[]
                { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
            {
                _saveData.SetRoomFlag(
                    group, room, bit, (flags & bit) != 0);
            }
        }

        try
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            SetTreasure(
                _saveData, TreasureDatabase.TreasureHarp, value: false);
            SetTreasure(
                _saveData,
                TreasureDatabase.TreasureTuneOfEchoes,
                value: false);
            reloadInventory.Invoke(_inventory, null);

            _saveData.SetRoomFlag(
                group,
                room,
                OracleSaveData.RoomFlagItem,
                value: true);
            LoadValidationRoom(group, room);
            FailIf(
                harpEvent.HasState ||
                _entities.Entities<GroundTreasurePickup>().Count != 0 ||
                _entities.Entities<NpcCharacter>().Any(npc =>
                    npc.Name.ToString() is
                        "HarpOfAgesSparkle" or "HarpOfAgesNayru"),
                "Room 3:ae replayed INTERAC_HARP_OF_AGES_SPAWNER $b3:$00 " +
                "after ROOMFLAG_ITEM $20 was set.");

            _saveData.SetRoomFlag(
                group,
                room,
                OracleSaveData.RoomFlagItem,
                value: false);
            _sound.ClearPlayRequestAudit();
            var trace = new ValidationCutsceneTrace();
            _roomEvents.CommandTraceSink = trace;
            LoadValidationRoom(group, room);

            GroundTreasurePickup harp =
                _entities.Entities<GroundTreasurePickup>().Single();
            NpcCharacter sparkle = harpEvent.Sparkle ??
                throw new InvalidOperationException(
                    "Room 3:ae did not create sparkle $84:$0c.");
            TreasureObjectRecord harpObject =
                _treasures.GetObject(record.HarpObject);
            HarpOfAgesVisualRecord sparkleVisual =
                database.Visual("Sparkle");
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.AwaitingPickup ||
                _roomEvents.Active ||
                harpEvent.Harp != harp ||
                harp.Record.TreasureObject != record.HarpObject ||
                harp.Record.SpawnMode != 0 ||
                harp.Record.GrabMode != 2 ||
                harp.Position != new Vector2(record.HarpX, record.HarpY) ||
                harp.PixelHash == 0 ||
                sparkle.Record is not
                    { Id: 0x84, SubId: 0x0c, DefaultAnimation: 0 } ||
                sparkle.Position != harp.Position ||
                sparkle.Record.DownAnimation != sparkleVisual.Animation0 ||
                sparkleVisual.SourceOffset != 0x1c00 ||
                sparkle.GraphicsSourceOffset != sparkleVisual.SourceOffset ||
                sparkle.CurrentAnimationTextureSize != new Vector2I(48, 48) ||
                sparkle.CurrentAnimationOffset != new Vector2(-24, -24) ||
                sparkle.CurrentAnimationOpaquePixels == 0 ||
                sparkle.CurrentAnimationPixelHash != 0xd53f8dfc747a0f65UL ||
                sparkle.AnimationRate != 1.0f,
                "Room 3:ae did not initialize the static Harp of Ages and " +
                "attached animated $84:$0c sparkle from spr_link+$1c00 " +
                $"(hash=${sparkle.CurrentAnimationPixelHash:x16}).");

            _player.WarpTo(new Vector2(0x18, 0x78), recordSafe: false);
            StepRoomEventFrames(2);
            FailIf(
                harp.State != PickupState.Waiting,
                "The room 3:ae Harp did not reach static treasure state 2.");

            _player.WarpTo(harp.Position, recordSafe: false);
            StepRoomEventFrames(1);
            FailIf(
                harp.State != PickupState.Collected ||
                harp.Held ||
                harpEvent.Stage != HarpOfAgesEventStage.PickupDelay ||
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(harpObject.Message) ||
                !_inventory.HasTreasure(record.HarpTreasure) ||
                _inventory.SelectedHarpSong != 0 ||
                !_saveData.HasRoomFlag(
                    group,
                    room,
                    OracleSaveData.RoomFlagItem),
                "Touching the room 3:ae Harp did not grant treasure $11, " +
                "set ROOMFLAG_ITEM, open TX_0071, and preserve the spawner's " +
                "one-update observation delay.");

            StepRoomEventFrames(1);
            FailIf(
                !harp.Held ||
                !_player.IsHoldingItemTwoHands ||
                harp.Position != _player.Position + new Vector2(0, -14) ||
                sparkle.Position != harp.Position ||
                harpEvent.Stage != HarpOfAgesEventStage.AwaitingTextOpen ||
                !_player.CutsceneControlled ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndCtrlStopMusic) != 1,
                "Harp state 1 did not lift the reward, keep the sparkle " +
                "attached, stop music, and disable Link on the following update.");

            StepRoomEventFrames(1);
            FailIf(
                harpEvent.Stage !=
                    HarpOfAgesEventStage.AwaitingTextClose ||
                _player.FacingVector != Vector2I.Up,
                "Harp state 2 did not face Link upward while TX_0071 was active.");

            _dialogue.Close();
            _interactions.Update(1.0 / 60.0, _player);
            _entities.Update(1.0 / 60.0, _player);
            if (GodotObject.IsInstanceValid(harp))
                harp.Free();
            _roomEvents.Update(1.0 / 60.0);
            _sound.Tick();
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.FadeOut ||
                harpEvent.StageCounter != 0 ||
                sparkle.Active ||
                _player.IsHoldingItemTwoHands ||
                _entities.Entities<GroundTreasurePickup>().Count != 0 ||
                !_hud.StatusBarHidden ||
                !Mathf.IsEqualApprox(
                    _roomView.BackgroundFadeAlpha, 0.0f),
                "Closing TX_0071 did not clear cfc0 bit 0, delete the held " +
                "Harp, hide the HUD, and begin the delay-2 background fade.");

            StepRoomEventFrames(record.FadeFrames - 1);
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.FadeOut ||
                harpEvent.StageCounter != record.FadeFrames - 1 ||
                !Mathf.IsEqualApprox(
                    _roomView.BackgroundFadeAlpha, 1.0f),
                "Room 3:ae fadeoutToBlackWithDelay(2) did not reach black " +
                "one update before its 65-update completion gate.");
            StepRoomEventFrames(1);
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.BlackHold ||
                harpEvent.StageCounter != record.BlackHold,
                "Room 3:ae black fade did not install counter $28 exactly " +
                "after update 65.");

            StepRoomEventFrames(record.BlackHold - 1);
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.BlackHold ||
                harpEvent.StageCounter != 1 ||
                harpEvent.Nayru is not null,
                "Room 3:ae spawned Nayru before all 40 black-hold decrements.");
            StepRoomEventFrames(1);
            NpcCharacter nayru = harpEvent.Nayru ??
                throw new InvalidOperationException(
                    "Room 3:ae did not create Nayru $36:$07 after counter $28.");
            HarpOfAgesVisualRecord nayruVisual = database.Visual("Nayru");
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.NayruFlicker ||
                harpEvent.StageCounter != record.NayruFlicker ||
                nayru.Record is not
                    { Id: 0x36, SubId: 0x07 } ||
                nayru.Position !=
                    new Vector2(record.SpawnerX, record.SpawnerY) ||
                nayru.SourceGraphicsWidth != 256 ||
                nayru.CurrentScriptAnimationSource !=
                    nayruVisual.Animation2 ||
                nayru.CurrentAnimationOpaquePixels == 0 ||
                nayru.AnimationRate != 0.0f,
                "The room 3:ae vision did not compose Nayru's two graphics " +
                "sheets, idle animation, position, and native animation rate.");

            StepRoomEventFrames(record.NayruFlicker - 1);
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.NayruFlicker ||
                harpEvent.StageCounter != 1,
                "Nayru's state-1 flicker ended before its 30th decrement.");
            StepRoomEventFrames(1);
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.Script ||
                harpEvent.CommandInstruction != 0 ||
                !nayru.Visible ||
                _sound.ActiveMusic != record.NayruMusic,
                "Nayru's final flicker update did not show her, start " +
                "MUS_NAYRU, and arm nayruScript07.");

            StepRoomEventFrames(1);
            FailIf(
                harpEvent.CommandInstruction != 0 ||
                harpEvent.CommandCounter != 12,
                "nayruScript07 did not install its opening wait 12.");
            StepRoomEventFrames(12);
            FailIf(
                !_dialogue.IsOpen ||
                harpEvent.CommandInstruction != 3 ||
                harpEvent.TextboxFlags != record.TextboxFlags ||
                _dialogue.TextboxFlagsForValidation != record.TextboxFlags ||
                database.Commands[2] is not
                    CutsceneShowTextCommand firstNayruText ||
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(firstNayruText.Message),
                "nayruScript07 lost its exact opening wait, alternate " +
                "textbox palette, or TX_1d10 Harp teaching dialogue " +
                $"(open={_dialogue.IsOpen}, instruction=" +
                $"{harpEvent.CommandInstruction}, eventFlags=" +
                $"${harpEvent.TextboxFlags:x2}, dialogueFlags=" +
                $"${_dialogue.TextboxFlagsForValidation:x2}, text='" +
                $"{_dialogue.CurrentMessage}').");

            _dialogue.Close();
            StepRoomEventFrames(1);
            StepRoomEventFrames(16);
            FailIf(
                harpEvent.CommandInstruction != 5 ||
                nayru.CurrentScriptAnimationSource !=
                    nayruVisual.Animation7,
                "Nayru did not select animation $07 after the post-text wait 16.");
            StepRoomEventFrames(1);
            _sound.ClearPlayRequestAudit();
            StepRoomEventFrames(1);
            StepRoomEventFrames(1);
            FailIf(
                harpEvent.CommandInstruction != 7 ||
                harpEvent.CommandCounter != 210 ||
                _sound.PlayRequestsFor(record.SongSound) != 1,
                "Nayru did not set direction $07, play SND_TUNE_OF_ECHOES, " +
                "and install wait 210 in source command order.");

            StepRoomEventFrames(210);
            FailIf(
                harpEvent.CommandInstruction != 9 ||
                harpEvent.CommandCounter != 75 ||
                harpEvent.MusicNotesSpawned < 3 ||
                harpEvent.MusicNoteCount == 0,
                "Nayru's 210-update Tune of Echoes performance lost its " +
                "music-note parameters or cfc0 pause boundary.");
            int pausedFrame = nayru.CurrentAnimationFrame;
            int pausedNotes = harpEvent.MusicNotesSpawned;
            StepRoomEventFrames(74);
            FailIf(
                harpEvent.CommandInstruction != 9 ||
                harpEvent.CommandCounter != 1 ||
                nayru.CurrentAnimationFrame != pausedFrame ||
                harpEvent.MusicNotesSpawned != pausedNotes,
                "cfc0 bit 0 did not pause Nayru's animation and new notes " +
                "through the first 74 updates of wait 75.");
            StepRoomEventFrames(1);
            FailIf(
                harpEvent.CommandInstruction != 12 ||
                nayru.CurrentScriptAnimationSource !=
                    nayruVisual.Animation2,
                "The final wait-75 update did not resume Nayru and restore " +
                "idle animation $02.");

            StepRoomEventFrames(1);
            StepRoomEventFrames(1);
            StepRoomEventFrames(16);
            FailIf(
                !_dialogue.IsOpen ||
                harpEvent.CommandInstruction != 16 ||
                _dialogue.TextboxFlagsForValidation != record.TextboxFlags ||
                database.Commands[15] is not
                    CutsceneShowTextCommand secondNayruText ||
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(secondNayruText.Message),
                "nayruScript07 lost its second wait 16 or TX_1d11 " +
                "Time Portal explanation.");

            _dialogue.Close();
            StepRoomEventFrames(1);
            StepRoomEventFrames(record.SongInitialDelay - 1);
            FailIf(
                _player.HarpPoseActive ||
                harpEvent.CommandInstruction != 16,
                "INTERAC_PLAY_HARP_SONG left state 1 before its four-update delay.");
            _sound.ClearPlayRequestAudit();
            StepRoomEventFrames(1);
            FailIf(
                !_player.HarpPoseActive ||
                _player.HarpPoseFrame != 0 ||
                database.LinkHarpFrames[0].SourceOffset != 0 ||
                !database.LinkHarpFrames[0].Parts
                    .Select(part => part.Tile)
                    .SequenceEqual([0x30, 0x32, 0x34]) ||
                !database.LinkHarpFrames[1].Parts
                    .Select(part => part.Tile)
                    .SequenceEqual([0x36, 0x38, 0x34]) ||
                _sound.PlayRequestsFor(record.SongSound) != 1,
                "INTERAC_PLAY_HARP_SONG state 1 did not select " +
                "LINK_ANIM_MODE_HARP_2 with retained Harp tiles and replay " +
                "the Tune of Echoes.");

            int notesBeforeLink = harpEvent.MusicNotesSpawned;
            int phraseUpdates =
                record.SongPhaseFrames * record.SongPhases;
            StepRoomEventFrames(phraseUpdates - 1);
            FailIf(
                !_player.HarpPoseActive ||
                _player.HarpPoseFrame != 11,
                "Link's four 52-update Harp phrases advanced the imported " +
                "208-update animation one update early.");
            StepRoomEventFrames(1);
            FailIf(
                !_player.HarpPoseActive ||
                _player.HarpPoseFrame != 12 ||
                harpEvent.MusicNotesSpawned - notesBeforeLink < 6,
                "Link's final Harp phrase did not enter the source's " +
                "one-update terminal frame or emit alternating note effects.");
            StepRoomEventFrames(1);
            FailIf(
                _player.HarpPoseActive ||
                harpEvent.CommandInstruction != 17,
                "INTERAC_PLAY_HARP_SONG state 6 did not restore Link's walk " +
                "animation and set cfc0 bit 7 on update 214.");

            StepRoomEventFrames(1);
            FailIf(harpEvent.CommandCounter != 36, "nayruScript07 did not install its post-song wait 36.");
            _sound.ClearPlayRequestAudit();
            StepRoomEventFrames(36);
            GroundTreasurePickup echoReward =
                harpEvent.EchoReward ??
                throw new InvalidOperationException(
                    "nayruScript07 did not create the Tune of Echoes reward.");
            TreasureObjectRecord echoObject =
                _treasures.GetObject(record.EchoesObject);
            FailIf(
                !echoReward.Held ||
                echoReward.Record.TreasureObject != record.EchoesObject ||
                echoReward.Record is not
                {
                    SpawnMode: 0,
                    GrabMode: 2,
                    InventoryWrite:
                        GroundTreasureInventoryWrite.TreasureObject,
                    RoomFlagTiming:
                        GroundTreasureRoomFlagTiming.OnActivation,
                    SoundOrder:
                        GroundTreasureSoundOrder.BehaviourThenGrab,
                    DialogueTiming:
                        GroundTreasureDialogueTiming.BeforeGrab,
                    CompletionOwner:
                        GroundTreasureCompletionOwner.SharedInteraction
                } ||
                echoReward.Position !=
                    _player.Position + new Vector2(0, -14) ||
                !_player.IsHoldingItemTwoHands ||
                !_inventory.HasTreasure(record.EchoesTreasure) ||
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(echoObject.Message) ||
                _dialogue.TextboxFlagsForValidation != record.TextboxFlags ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndGetItem) != 1,
                "giveitem TREASURE_TUNE_OF_ECHOES $00 did not use the " +
                "two-hand reward lifecycle, TX_0072, inventory bit, and " +
                "single explicit SND_GETITEM.");

            _dialogue.Close();
            _interactions.Update(1.0 / 60.0, _player);
            StepRoomEventFrames(1);
            FailIf(
                _player.IsHoldingItemTwoHands ||
                harpEvent.CommandInstruction != 20 ||
                harpEvent.CommandCounter != 16 ||
                _entities.Entities<GroundTreasurePickup>().Count != 0,
                "Closing TX_0072 did not delete the reward and install " +
                "nayruScript07's final wait 16.");

            StepRoomEventFrames(16);
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.FadeInTail ||
                _roomEvents.Active ||
                _player.CutsceneControlled ||
                !_hud.Visible ||
                _hud.StatusBarHidden ||
                !Mathf.IsEqualApprox(
                    _roomView.BackgroundFadeAlpha, 0.0f) ||
                nayru.Active ||
                !Mathf.IsEqualApprox(_warpFade.Color.A, 1.0f) ||
                _warpFade.Position != Vector2.Zero ||
                _warpFade.Size != new Vector2(
                    OracleRoomData.ViewportWidth,
                    OracleRoomData.ScreenHeight) ||
                _warpFade.ZIndex <= _hud.ZIndex ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(group, room),
                "nayruScript07 scriptend did not restore room music, input, " +
                "HUD/background palette, remove Nayru, and begin the full-screen " +
                "white fade in one update.");

            StepRoomEventFrames(record.FinalFadeFrames - 1);
            FailIf(
                harpEvent.Stage != HarpOfAgesEventStage.FadeInTail ||
                !Mathf.IsEqualApprox(_warpFade.Color.A, 0.0f),
                "fadeinFromWhiteWithDelay(4) did not reach the visible " +
                "palette one update before its 129-update completion gate.");
            StepRoomEventFrames(1);
            FailIf(
                harpEvent.HasState ||
                _warpFade.Position != originalFadePosition ||
                _warpFade.Size != originalFadeSize ||
                _warpFade.ZIndex != originalFadeZ,
                "Room 3:ae's final fade did not restore the shared fade " +
                "rectangle and retire its controller on update 129.");

            CutsceneCommandTraceEntry[] starts = trace.Entries
                .Where(entry =>
                    entry.Phase == CutsceneCommandTracePhase.Started)
                .ToArray();
            string[] expectedOpcodes =
            [
                "wait", "writememory", "showtext", "wait",
                "setanimation", "writeobjectbyte", "playsound", "wait",
                "native", "wait", "native", "setanimation",
                "writeobjectbyte", "wait", "writememory", "showtext",
                "nativeblock", "wait", "writememory", "giveitem",
                "wait", "scriptend"
            ];
            FailIf(
                starts.Length != expectedOpcodes.Length ||
                starts.Where((entry, index) =>
                    entry.Source.Script != "nayruScript07" ||
                    entry.Source.CommandIndex != index ||
                    entry.Source.Opcode != expectedOpcodes[index] ||
                    entry.Source.SourceLine <= 0).Any(),
                "Room 3:ae's imported nayruScript07 trace lost an opcode, " +
                "source line, or source-order boundary.");
            _roomEvents.CommandTraceSink = null;

            LoadValidationRoom(group, room);
            FailIf(
                harpEvent.HasState ||
                _entities.Entities<GroundTreasurePickup>().Count != 0,
                "Completed room 3:ae replayed its Harp/Nayru sequence on re-entry.");
        }
        finally
        {
            _roomEvents.CommandTraceSink = null;
            if (_dialogue.IsOpen)
                _dialogue.Close();
            _saveData.WriteWramBytes(0xc688, inventorySnapshot);
            _saveData.CommitInventoryChange();
            reloadInventory.Invoke(_inventory, null);
            RestoreRoomFlags(originalRoomFlags);
            LoadValidationRoom(0, 0x11);
        }

        GD.Print(
            "Validated room 3:ae Harp of Ages $b3:$00: ROOMFLAG_ITEM " +
            "predicate, full 48x48 $84:$0c sparkle from spr_link+$1c00, " +
            "real ground-treasure pickup, " +
            "65/40/30-update native wrapper, two-sheet Nayru vision, complete " +
            "nayruScript07 typed trace, alternate-palette TX_1d10/TX_1d11, " +
            "210-update song and 75-update pause, exact 214-update Link Harp " +
            "response with partial graphics-load retention, Tune of Echoes " +
            "reward, room-music/input/HUD restore, " +
            "and 129-update full-screen white fade.");
    }
}
