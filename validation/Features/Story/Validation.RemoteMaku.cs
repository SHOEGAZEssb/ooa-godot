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
        RemoteMakuFirstEssenceRecord record = cutscene.Database.Record;
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
            if (!_dialogue.IsOpen)
            {
                throw new InvalidOperationException(
                    "Room 0:8d remote Maku dialogue did not open within its " +
                    "imported fade/confetti waits.");
            }
        }

        void FinishAfterDialogue(int initialState, int expectedMapText)
        {
            _dialogue.Close();
            StepRoomEventFrames(1);
            if (cutscene.CommandInstruction != 11 ||
                cutscene.CommandCounter != 1 ||
                !_hud.Visible ||
                !_hud.StatusBarHidden)
            {
                throw new InvalidOperationException(
                    "Remote Maku TX_05b0/TX_05c0 did not install the source " +
                    "wait-1 command before restoring the status bar.");
            }

            StepRoomEventFrames(1);
            if (!_hud.Visible ||
                _hud.StatusBarHidden ||
                _roomView.BackgroundFadeAlpha != 0.0f ||
                cutscene.CommandInstruction != 14 ||
                _warpFade.Color.A != 1.0f ||
                _warpFade.Position != Vector2.Zero ||
                _warpFade.Size != new Vector2(
                    OracleRoomData.ViewportWidth,
                    OracleRoomData.ScreenHeight) ||
                _warpFade.ZIndex <= _hud.ZIndex)
            {
                throw new InvalidOperationException(
                    "Remote Maku did not clear the black palette, show the HUD, " +
                    "and begin a full-screen fade from white in one update.");
            }

            StepRoomEventFrames(record.FadeFrames - 2);
            if (cutscene.CommandInstruction != 14 ||
                _warpFade.Color.A != 0.0f)
            {
                throw new InvalidOperationException(
                    "Remote Maku fadeinFromWhiteWithDelay(2) did not reach the " +
                    "visible palette two updates before its completion gate.");
            }
            StepRoomEventFrames(1);
            if (cutscene.CommandInstruction != 15 ||
                _warpFade.Position != originalFadePosition ||
                _warpFade.Size != originalFadeSize ||
                _warpFade.ZIndex != originalFadeZ)
            {
                throw new InvalidOperationException(
                    "Remote Maku white-fade completion did not restore the shared " +
                    "fade rectangle before resetmusic.");
            }

            StepRoomEventFrames(1);
            if (!SetAndReadRoomFlag() ||
                cutscene.CommandInstruction != 17 ||
                _saveData.MakuTreeState != initialState ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(group, room))
            {
                throw new InvalidOperationException(
                    "Remote Maku resetmusic/orroomflag $40 did not yield before " +
                    "incMakuTreeState.");
            }
            StepRoomEventFrames(1);
            if (cutscene.HasState || _roomEvents.Active ||
                _player.CutsceneControlled ||
                _saveData.MakuTreeState != initialState + 1 ||
                _saveData.MakuMapTextPresent != expectedMapText)
            {
                throw new InvalidOperationException(
                    "Remote Maku did not increment wMakuTreeState, restore input, " +
                    "and end after setting the correct map text.");
            }
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
            if (cutscene.HasState || _roomEvents.Active)
                throw new InvalidOperationException(
                    "Room 0:8d remote Maku event ignored its first-Essence predicate.");

            _saveData.WriteWramByte(
                0xc6bf, (byte)(originalEssences | record.EssenceMask));
            SetRoomFlag(true);
            LoadValidationRoom(group, room);
            if (cutscene.HasState || _roomEvents.Active)
                throw new InvalidOperationException(
                    "Room 0:8d remote Maku event replayed with room flag $40 set.");

            SetRoomFlag(false);
            _saveData.SetLinkedGame(false);
            _saveData.SetMakuTreeState(3);
            _saveData.SetMakuMapTextPresent(0);
            _sound.ClearPlayRequestAudit();
            var trace = new ValidationCutsceneTrace();
            _roomEvents.CommandTraceSink = trace;
            LoadValidationRoom(group, room);
            if (cutscene.Stage != RemoteMakuFirstEssenceEventStage.Running ||
                _player.CutsceneControlled)
            {
                throw new InvalidOperationException(
                    "Room 0:8d did not arm its imported $8a:$00 lane after loading.");
            }

            StepRoomEventFrames(1);
            if (!_player.CutsceneControlled ||
                cutscene.TextboxFlags != 0x04 ||
                cutscene.CommandInstruction != 3 ||
                _sound.ActiveMusic != OracleSoundEngine.MusMakuTree)
            {
                throw new InvalidOperationException(
                    "Remote Maku disableinput/textbox-palette/setmusic commands " +
                    "lost their first script update.");
            }
            StepRoomEventFrames(1);
            StepRoomEventFrames(record.InitialWait - 1);
            if (!_hud.Visible || _hud.StatusBarHidden ||
                cutscene.CommandCounter != 1 ||
                cutscene.CommandInstruction != 3)
            {
                throw new InvalidOperationException(
                    "Remote Maku hid the HUD before its imported 40-update wait.");
            }
            StepRoomEventFrames(1);
            if (!_hud.Visible ||
                !_hud.StatusBarHidden ||
                cutscene.DontUpdateStatusBar != record.HudLockByte ||
                cutscene.CommandInstruction != 6 ||
                _roomView.BackgroundFadeAlpha != 0.0f)
            {
                throw new InvalidOperationException(
                    "Remote Maku did not hide the HUD and start its black palette " +
                    "thread immediately after wait 40.");
            }

            StepRoomEventFrames(record.FadeFrames - 2);
            if (cutscene.CommandInstruction != 6 ||
                _roomView.BackgroundFadeAlpha != 1.0f ||
                _hud.HiddenStatusBarColorForValidation !=
                    _roomView.BackgroundFadeColorForValidation)
            {
                throw new InvalidOperationException(
                    "Remote Maku fadeoutToBlackWithDelay(2) did not reach one " +
                    "matching black across the room and hidden status-bar strip.");
            }
            StepRoomEventFrames(1);
            if (cutscene.CommandInstruction != 7)
                throw new InvalidOperationException(
                    "Remote Maku black-fade completion gate drifted from update 65.");

            StepRoomEventFrames(1);
            if (cutscene.Confetti is not
                { SpawnedPieces: 0, LivePieces: 0 } ||
                cutscene.CommandInstruction != 8 ||
                cutscene.CommandCounter != record.ConfettiHold1)
            {
                throw new InvalidOperationException(
                    "Remote Maku did not initialize $62:$00 before beginning wait 240.");
            }
            StepRoomEventFrames(1);
            if (cutscene.Confetti is not
                { SpawnedPieces: 1, LivePieces: 1 } ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndMagicPowder) != 1)
            {
                throw new InvalidOperationException(
                    "Present Maku confetti did not spawn its first $e8/$38 piece " +
                    "and SND_MAGIC_POWDER one update after initialization.");
            }
            Vector2 firstPosition = cutscene.Confetti.PiecePositions.Single();
            if (firstPosition != new Vector2(0x38, -24))
                throw new InvalidOperationException(
                    "The first present Maku confetti piece moved during its state-0 update.");

            StepRoomEventFrames(49);
            if (cutscene.Confetti.SpawnedPieces != 1)
                throw new InvalidOperationException(
                    "Present Maku confetti ignored its second-piece delay $32.");
            StepRoomEventFrames(1);
            if (cutscene.Confetti.SpawnedPieces != 2 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndMagicPowder) != 2)
            {
                throw new InvalidOperationException(
                    "Present Maku confetti did not spawn piece two after exactly $32 updates.");
            }

            StepUntilDialogue();
            if (_dialogue.CurrentMessage is not string message ||
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
                    record.ConfettiPieces)
            {
                throw new InvalidOperationException(
                    "Remote Maku standard TX_05b0, map text $b0, black palette, " +
                    "PALH_0d dialogue colors, or complete present confetti " +
                    "effect diverged.");
            }
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
            if (starts.Length != expectedOpcodes.Length ||
                starts.Where((entry, index) =>
                    entry.Source.Script != "remoteMakuCutsceneScript" ||
                    entry.Source.CommandIndex != index ||
                    entry.Source.Opcode != expectedOpcodes[index] ||
                    entry.Source.SourceLine <= 0).Any())
            {
                throw new InvalidOperationException(
                    "The imported remote-Maku command stream lost an opcode, " +
                    "source line, or source ordering boundary.");
            }
            _roomEvents.CommandTraceSink = null;

            LoadValidationRoom(group, room);
            if (cutscene.HasState || _roomEvents.Active)
                throw new InvalidOperationException(
                    "Completed room 0:8d remote Maku event replayed on re-entry.");

            // The same helper adds $10 for INTERAC_REMOTE_MAKU_CUTSCENE in a
            // linked game, updating both the shown ID and wMakuMapTextPresent.
            SetRoomFlag(false);
            _saveData.SetLinkedGame(true);
            _saveData.SetMakuTreeState(7);
            _saveData.SetMakuMapTextPresent(0);
            LoadValidationRoom(group, room);
            StepUntilDialogue();
            if (_saveData.MakuMapTextPresent != record.LinkedMapText)
                throw new InvalidOperationException(
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
            "script cadence, palette-0-preserving black fade, matching black status " +
            "strip, PALH_0d dialogue colors, HUD timing, five-piece present " +
            "confetti/sparkles, TX_05b0/TX_05c0 map offsets, full-screen white fade, " +
            "music restore, room flag $40, and Maku-state increment.");
    }
}
