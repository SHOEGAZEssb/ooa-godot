using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRemoteMakuFirstEssenceCutscene()
    {
        const int group = 0;
        const int room = 0x8d;
        RemoteMakuFirstEssenceEvent cutscene =
            _roomEvents.RemoteMakuFirstEssence;
        RemoteMakuEventRecord record = cutscene.Database.Record;
        byte originalEssences = _saveData.ReadWramByte(0xc6bf);
        int originalMakuState = _saveData.MakuTreeState;
        int originalMapText = _saveData.MakuMapTextPresent;
        bool originalLinked = _saveData.IsLinkedGame;
        bool originalRoomFlag = _saveData.HasRoomFlag(
            group, room, (byte)record.RoomFlag);
        Vector2 originalFadePosition = _warpFade.Position;
        Vector2 originalFadeSize = _warpFade.Size;
        int originalFadeZ = _warpFade.ZIndex;

        void SetRoomFlag(bool value) => _saveData.SetRoomFlag(
            group, room, (byte)record.RoomFlag, value);

        void StepUntilDialogue(int limit = 700)
        {
            for (int frame = 0; frame < limit && !_dialogue.IsOpen; frame++)
                StepRoomEventFrames(1);
            FailIf(
                !_dialogue.IsOpen,
                "Room 0:8d remote Maku dialogue did not open within its " +
                "imported fade/confetti waits.");
        }

        void FinishAfterDialogue(int initialState, int expectedMapText)
        {
            _dialogue.Close();
            StepRoomEventFrames(1);
            FailIf(
                cutscene.CommandInstruction != 11 ||
                cutscene.CommandCounter != 1 ||
                !_hud.Visible ||
                !_hud.StatusBarHidden,
                "Remote Maku TX_05b0/TX_05c0 did not install the source " +
                "wait-1 command before restoring the status bar.");

            StepRoomEventFrames(1);
            FailIf(
                !_hud.Visible ||
                _hud.StatusBarHidden ||
                _roomView.BackgroundFadeAlpha != 0.0f ||
                cutscene.CommandInstruction != 14 ||
                _warpFade.Color.A != 1.0f ||
                _warpFade.Position != Vector2.Zero ||
                _warpFade.Size != new Vector2(
                    OracleRoomData.ViewportWidth,
                    OracleRoomData.ScreenHeight) ||
                _warpFade.ZIndex <= _hud.ZIndex,
                "Remote Maku did not clear the black palette, show the HUD, " +
                "and begin a full-screen fade from white in one update.");

            StepRoomEventFrames(record.FadeFrames - 2);
            FailIf(
                cutscene.CommandInstruction != 14 ||
                _warpFade.Color.A != 0.0f,
                "Remote Maku fadeinFromWhiteWithDelay(2) did not reach the " +
                "visible palette two updates before its completion gate.");
            StepRoomEventFrames(1);
            FailIf(
                cutscene.CommandInstruction != 15 ||
                _warpFade.Position != originalFadePosition ||
                _warpFade.Size != originalFadeSize ||
                _warpFade.ZIndex != originalFadeZ,
                "Remote Maku white-fade completion did not restore the shared " +
                "fade rectangle before resetmusic.");

            StepRoomEventFrames(1);
            FailIf(
                !SetAndReadRoomFlag() ||
                cutscene.CommandInstruction != 17 ||
                _saveData.MakuTreeState != initialState ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(group, room),
                "Remote Maku resetmusic/orroomflag $40 did not yield before " +
                "incMakuTreeState.");
            StepRoomEventFrames(1);
            FailIf(
                cutscene.HasState || _roomEvents.Active ||
                _player.CutsceneControlled ||
                _saveData.MakuTreeState != initialState + 1 ||
                _saveData.MakuMapTextPresent != expectedMapText,
                "Remote Maku did not increment wMakuTreeState, restore input, " +
                "and end after setting the correct map text.");
        }

        bool SetAndReadRoomFlag() => _saveData.HasRoomFlag(
            group, room, (byte)record.RoomFlag);

        try
        {
            // INTERAC_REMOTE_MAKU_CUTSCENE var03=$00 deletes itself unless
            // wEssencesObtained bit 0 is set, and room flag $40 suppresses
            // subsequent entries even when that Essence remains owned.
            SetRoomFlag(false);
            _saveData.WriteWramByte(
                0xc6bf, (byte)(originalEssences & ~record.EssenceMask));
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Room 0:8d remote Maku event ignored its first-Essence predicate.");

            _saveData.WriteWramByte(
                0xc6bf, (byte)(originalEssences | record.EssenceMask));
            SetRoomFlag(true);
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Room 0:8d remote Maku event replayed with room flag $40 set.");

            SetRoomFlag(false);
            _saveData.SetLinkedGame(false);
            _saveData.SetMakuTreeState(3);
            _saveData.SetMakuMapTextPresent(0);
            _sound.ClearPlayRequestAudit();
            var trace = new ValidationCutsceneTrace();
            _roomEvents.CommandTraceSink = trace;
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.Stage != RemoteMakuEventStage.Running ||
                _player.CutsceneControlled,
                "Room 0:8d did not arm its imported $8a:$00 lane after loading.");

            StepRoomEventFrames(1);
            FailIf(
                !_player.CutsceneControlled ||
                cutscene.TextboxFlags != 0x04 ||
                cutscene.CommandInstruction != 3 ||
                _sound.ActiveMusic != OracleSoundEngine.MusMakuTree,
                "Remote Maku disableinput/textbox-palette/setmusic commands " +
                "lost their first script update.");
            StepRoomEventFrames(1);
            StepRoomEventFrames(record.InitialWait - 1);
            FailIf(
                !_hud.Visible || _hud.StatusBarHidden ||
                cutscene.CommandCounter != 1 ||
                cutscene.CommandInstruction != 3,
                "Remote Maku hid the HUD before its imported 40-update wait.");
            StepRoomEventFrames(1);
            FailIf(
                !_hud.Visible ||
                !_hud.StatusBarHidden ||
                cutscene.DontUpdateStatusBar != record.HudLockByte ||
                cutscene.CommandInstruction != 6 ||
                _roomView.BackgroundFadeAlpha != 0.0f ||
                _hud.HiddenStatusBarFadeAlphaForValidation != 0.0f ||
                _hud.HiddenStatusBarColorForValidation !=
                    _hud.HiddenStatusBarBaseColorForValidation,
                "Remote Maku did not hide the HUD and start its black palette " +
                "thread immediately after wait 40.");

            const int midpointFrames = 32;
            StepRoomEventFrames(midpointFrames);
            FailIf(
                cutscene.CommandInstruction != 6 ||
                _roomView.BackgroundFadeAlpha != 0.5f ||
                _hud.HiddenStatusBarFadeAlphaForValidation !=
                    _roomView.BackgroundFadeAlpha ||
                _hud.HiddenStatusBarColorForValidation ==
                    _hud.HiddenStatusBarBaseColorForValidation ||
                _hud.HiddenStatusBarColorForValidation == Colors.Black,
                "Remote Maku background palette 0 did not share the room's " +
                "delay-2 black-fade midpoint.");

            StepRoomEventFrames(record.FadeFrames - 2 - midpointFrames);
            FailIf(
                cutscene.CommandInstruction != 6 ||
                _roomView.BackgroundFadeAlpha != 1.0f ||
                _hud.HiddenStatusBarFadeAlphaForValidation != 1.0f ||
                _hud.HiddenStatusBarColorForValidation !=
                    _roomView.BackgroundFadeColorForValidation,
                "Remote Maku fadeoutToBlackWithDelay(2) did not reach one " +
                "matching black across the room and hidden status-bar strip.");
            StepRoomEventFrames(1);
            FailIf(
                cutscene.CommandInstruction != 7,
                "Remote Maku black-fade completion gate drifted from update 65.");

            StepRoomEventFrames(1);
            FailIf(
                cutscene.Confetti is not
                { SpawnedPieces: 0, LivePieces: 0 } ||
                cutscene.CommandInstruction != 8 ||
                cutscene.CommandCounter != record.ConfettiHold1,
                "Remote Maku did not initialize $62:$00 before beginning wait 240.");
            StepRoomEventFrames(1);
            FailIf(
                cutscene.Confetti is not
                { SpawnedPieces: 1, LivePieces: 1 } ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndMagicPowder) != 1,
                "Present Maku confetti did not spawn its first $e8/$38 piece " +
                "and SND_MAGIC_POWDER one update after initialization.");
            Vector2 firstPosition = cutscene.Confetti.PiecePositions.Single();
            FailIf(
                firstPosition != new Vector2(0x38, -24),
                "The first present Maku confetti piece moved during its state-0 update.");

            StepRoomEventFrames(49);
            FailIf(
                cutscene.Confetti.SpawnedPieces != 1,
                "Present Maku confetti ignored its second-piece delay $32.");
            StepRoomEventFrames(1);
            FailIf(
                cutscene.Confetti.SpawnedPieces != 2 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndMagicPowder) != 2,
                "Present Maku confetti did not spawn piece two after exactly $32 updates.");

            StepUntilDialogue();
            FailIf(
                _dialogue.CurrentMessage is not string message ||
                !message.Contains("Western Woods", StringComparison.Ordinal) ||
                !message.Contains("Can you go", StringComparison.Ordinal) ||
                _saveData.MakuMapTextPresent != record.StandardMapText ||
                _sound.ActiveMusic != OracleSoundEngine.MusMakuTree ||
                _roomView.BackgroundFadeAlpha != 1.0f ||
                !_hud.Visible ||
                !_hud.StatusBarHidden ||
                _dialogue.TextboxFlagsForValidation != 0x04 ||
                _dialogue.ResolvedTextColorForValidation(0) !=
                    DialogueBox.DefaultTextColorForValidation ||
                _dialogue.ResolvedTextColorForValidation(2) !=
                    DialogueBox.RedTextColorForValidation ||
                _dialogue.ResolvedTextColorForValidation(3) !=
                    DialogueBox.BlueTextColorForValidation ||
                _dialogue.ResolvedTextColorForValidation(4) !=
                    DialogueBox.DefaultTextColorForValidation ||
                _dialogue.GlyphColorForValidation(0, 1, 0) != 3 ||
                _dialogue.GlyphColorForValidation(0, 1, 9) != 4 ||
                _dialogue.GlyphColorForValidation(0, 2, 8) != 2 ||
                _dialogue.GlyphColorForValidation(0, 4, 0) != 3 ||
                _dialogue.GlyphColorForValidation(0, 4, 13) != 4 ||
                cutscene.Confetti is not { Finished: true } ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndMagicPowder) <
                    record.ConfettiPieces,
                "Remote Maku standard TX_05b0, map text $b0, black palette, " +
                "PALH_0d dialogue colors, or complete present confetti " +
                "effect diverged.");
            FinishAfterDialogue(initialState: 3, record.StandardMapText);

            CutsceneCommandTraceEntry[] starts = trace.Entries
                .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
                .ToArray();
            string[] expectedOpcodes =
            {
                "disableinput", "writememory", "setmusic", "wait",
                "writememory", "native", "nativeblock", "native", "wait",
                "wait", "showtextdifferentforlinked", "wait", "native",
                "native", "nativeblock", "native", "orroomflag", "native",
                "enableinput", "scriptend"
            };
            FailIf(
                starts.Length != expectedOpcodes.Length ||
                starts.Where((entry, index) =>
                    entry.Source.Script != "remoteMakuCutsceneScript" ||
                    entry.Source.CommandIndex != index ||
                    entry.Source.Opcode != expectedOpcodes[index] ||
                    entry.Source.SourceLine <= 0).Any(),
                "The imported remote-Maku command stream lost an opcode, " +
                "source line, or source ordering boundary.");
            _roomEvents.CommandTraceSink = null;

            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Completed room 0:8d remote Maku event replayed on re-entry.");

            // The same helper adds $10 for INTERAC_REMOTE_MAKU_CUTSCENE in a
            // linked game, updating both the shown ID and wMakuMapTextPresent.
            SetRoomFlag(false);
            _saveData.SetLinkedGame(true);
            _saveData.SetMakuTreeState(7);
            _saveData.SetMakuMapTextPresent(0);
            LoadValidationRoom(group, room);
            StepUntilDialogue();
            FailIf(
                _saveData.MakuMapTextPresent != record.LinkedMapText,
                "Linked remote Maku text did not apply the source $10 offset " +
                "from TX_05b0/$b0 to TX_05c0/$c0.");
            FinishAfterDialogue(initialState: 7, record.LinkedMapText);
        }
        finally
        {
            _roomEvents.CommandTraceSink = null;
            if (_dialogue.IsOpen)
                _dialogue.Close();
            _saveData.WriteWramByte(0xc6bf, originalEssences);
            _saveData.SetMakuTreeState(originalMakuState);
            _saveData.SetMakuMapTextPresent(originalMapText);
            _saveData.SetLinkedGame(originalLinked);
            SetRoomFlag(originalRoomFlag);
            LoadValidationRoom(0, 0x11);
        }

        GD.Print(
            "Validated room 0:8d first-Essence remote Maku predicates, imported " +
            "script cadence, palette-0-preserving black fade, synchronized hidden " +
            "status strip, PALH_0d dialogue colors, HUD timing, five-piece present " +
            "confetti/sparkles, TX_05b0/TX_05c0 map offsets, full-screen white fade, " +
            "music restore, room flag $40, and Maku-state increment.");
    }

    private void ValidateRemoteMakuHarpCutscene()
    {
        const int group = 0;
        const int room = 0x3a;
        RemoteMakuHarpEvent cutscene = _roomEvents.RemoteMakuHarp;
        RemoteMakuEventRecord record = cutscene.Database.Record;
        var text = (CutsceneShowTextVariantsCommand)
            cutscene.Database.Commands[10];
        bool originalHarp = _saveData.HasTreasure(record.RequiredTreasure);
        int originalMakuState = _saveData.MakuTreeState;
        int originalMapText = _saveData.MakuMapTextPresent;
        bool originalLinked = _saveData.IsLinkedGame;
        bool originalRoomFlag = _saveData.HasRoomFlag(
            group, room, (byte)record.RoomFlag);

        void SetRoomFlag(bool value) => _saveData.SetRoomFlag(
            group, room, (byte)record.RoomFlag, value);

        void StepUntilDialogue()
        {
            for (int frame = 0; frame < 700 && !_dialogue.IsOpen; frame++)
                StepRoomEventFrames(1);
            FailIf(
                !_dialogue.IsOpen,
                "Room 0:3a post-Harp remote Maku dialogue did not open " +
                "within its imported fade/confetti waits.");
        }

        void FinishDialogue(int initialState, int expectedMapText)
        {
            _dialogue.Close();
            for (int frame = 0; frame < 100 && cutscene.HasState; frame++)
                StepRoomEventFrames(1);
            FailIf(
                cutscene.HasState ||
                _roomEvents.Active ||
                _player.CutsceneControlled ||
                !_saveData.HasRoomFlag(
                    group, room, (byte)record.RoomFlag) ||
                _saveData.MakuTreeState != initialState + 1 ||
                _saveData.MakuMapTextPresent != expectedMapText ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(group, room),
                "Room 0:3a post-Harp remote Maku completion did not " +
                "restore input/music, set room flag $40, update the map " +
                "text, and increment wMakuTreeState.");
        }

        try
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            SetRoomFlag(false);
            SetTreasure(_saveData, record.RequiredTreasure, value: false);
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Room 0:3a remote Maku event ignored its TREASURE_HARP " +
                "predicate.");

            SetTreasure(_saveData, record.RequiredTreasure);
            SetRoomFlag(true);
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Room 0:3a remote Maku event replayed with room flag $40 set.");

            SetRoomFlag(false);
            _saveData.SetLinkedGame(false);
            _saveData.SetMakuTreeState(5);
            _saveData.SetMakuMapTextPresent(0);
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.Stage != RemoteMakuEventStage.Running ||
                cutscene.Record.Room != record.Room ||
                cutscene.Record.Var03 != 0x02 ||
                cutscene.Record.RequiredTreasure !=
                    TreasureDatabase.TreasureHarp,
                "Room 0:3a did not select its imported $8a:$00/v$02 " +
                "post-Harp lane.");

            StepRoomEventFrames(1);
            FailIf(
                !_player.CutsceneControlled ||
                cutscene.CommandInstruction != 3 ||
                _sound.ActiveMusic != OracleSoundEngine.MusMakuTree,
                "Room 0:3a post-Harp lane lost the shared first-update " +
                "input lock, script order, or Maku Tree music.");
            StepUntilDialogue();
            FailIf(
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(text.StandardMessage) ||
                _saveData.MakuMapTextPresent != record.StandardMapText ||
                cutscene.Confetti is not { Finished: true } ||
                !_hud.StatusBarHidden,
                "Room 0:3a did not show standard TX_05b2, write map byte " +
                "$b2, and finish the present-day confetti under the hidden HUD " +
                $"(text={_dialogue.CurrentMessage ==
                    DialogueBox.PlainText(text.StandardMessage)}, " +
                $"map=${_saveData.MakuMapTextPresent:x2}, " +
                $"confetti={cutscene.Confetti?.Finished}, " +
                $"hudHidden={_hud.StatusBarHidden}).");
            FinishDialogue(initialState: 5, record.StandardMapText);

            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Completed room 0:3a post-Harp remote Maku event replayed " +
                "on re-entry.");

            SetRoomFlag(false);
            _saveData.SetLinkedGame(true);
            _saveData.SetMakuTreeState(9);
            _saveData.SetMakuMapTextPresent(0);
            LoadValidationRoom(group, room);
            StepUntilDialogue();
            FailIf(
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(text.LinkedMessage) ||
                _saveData.MakuMapTextPresent != record.LinkedMapText,
                "Linked room 0:3a remote Maku guidance did not apply the " +
                "source $10 offset from TX_05b2/$b2 to TX_05c2/$c2.");
            FinishDialogue(initialState: 9, record.LinkedMapText);
        }
        finally
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            SetTreasure(_saveData, record.RequiredTreasure, originalHarp);
            _saveData.SetMakuTreeState(originalMakuState);
            _saveData.SetMakuMapTextPresent(originalMapText);
            _saveData.SetLinkedGame(originalLinked);
            SetRoomFlag(originalRoomFlag);
            LoadValidationRoom(0, 0x11);
        }

        GD.Print(
            "Validated room 0:3a post-Harp remote Maku TREASURE_HARP and " +
            "room-$40 predicates, imported $8a:$00/v$02 lane, present confetti, " +
            "TX_05b2/TX_05c2 map offsets, music/input restore, Maku-state " +
            "increment, and re-entry suppression.");
    }
}
