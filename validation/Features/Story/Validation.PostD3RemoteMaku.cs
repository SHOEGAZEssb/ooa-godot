using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePostD3RemoteMakuCutscene()
    {
        const int group = 0;
        const int room = 0xba;
        PostD3RemoteMakuEvent postD3 = _roomEvents.PostD3RemoteMaku;
        RemoteMakuThirdEssenceEvent remote =
            _roomEvents.RemoteMakuThirdEssence;
        PostD3RemoteMakuRecord record = postD3.Database.Record;
        RemoteMakuEventRecord remoteRecord = remote.Database.Record;
        var remoteText = (CutsceneShowTextVariantsCommand)
            remote.Database.Commands[10];
        byte originalEssences = _saveData.ReadWramByte(0xc6bf);
        bool originalRoomFlag = _saveData.HasRoomFlag(
            group, room, (byte)record.RoomFlag);
        bool originalPastFlag = _saveData.HasRoomFlag(
            record.PastFlagGroup,
            record.PastFlagRoom,
            (byte)record.PastRoomFlag);
        bool originalFluteFlag = _saveData.HasGlobalFlag(
            record.StandardGlobalFlag);
        bool originalLinked = _saveData.IsLinkedGame;
        int originalMakuState = _saveData.MakuTreeState;
        int originalMapText = _saveData.MakuMapTextPresent;

        void SetEssences(int value)
        {
            if (_saveData.WriteWramByte(0xc6bf, (byte)value))
                _saveData.CommitInventoryChange();
        }

        void SetCompletionFlag(bool value) => _saveData.SetRoomFlag(
            group, room, (byte)record.RoomFlag, value);

        void StepToRemoteDialogue()
        {
            for (int frame = 0; frame < 900 && !_dialogue.IsOpen; frame++)
                StepRoomEventFrames(1);
            FailIf(
                !_dialogue.IsOpen,
                "Room 0:ba third-Essence remote Maku dialogue did not open " +
                "after its imported present-confetti waits.");
        }

        void FinishRemote(int initialMakuState, int mapText)
        {
            _dialogue.Close();
            for (int frame = 0; frame < 100 && remote.HasState; frame++)
                StepRoomEventFrames(1);
            FailIf(
                postD3.HasState || remote.HasState || _roomEvents.Active ||
                _player.CutsceneControlled ||
                !_saveData.HasRoomFlag(
                    group, room, (byte)record.RoomFlag) ||
                _saveData.MakuTreeState != initialMakuState + 1 ||
                _saveData.MakuMapTextPresent != mapText ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(group, room),
                "Room 0:ba remote Maku completion did not set room flag $40, " +
                "increment Maku state, update the present map byte, and " +
                "restore room music/input.");
        }

        void RunPreludeToRemote()
        {
            for (int frame = 0; frame < 700 && !remote.HasState; frame++)
            {
                if (_dialogue.IsOpen)
                    _dialogue.Close();
                StepRoomEventFrames(1);
            }
            FailIf(
                postD3.HasState || remote.Stage != RemoteMakuEventStage.Running,
                "Room 0:ba post-D3 route did not hand off to dynamic " +
                "$8a:$00/v$04.");
        }

        try
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            SetCompletionFlag(false);
            SetEssences(originalEssences & ~record.EssenceMask);
            LoadValidationRoom(group, room);
            FailIf(
                postD3.HasState || remote.HasState || _roomEvents.Active,
                "Room 0:ba interaction $6b:$06 ignored Essence bit $04.");

            SetEssences(originalEssences | record.EssenceMask);
            SetCompletionFlag(true);
            LoadValidationRoom(group, room);
            FailIf(
                postD3.HasState || remote.HasState || _roomEvents.Active,
                "Room 0:ba post-D3 route replayed with room flag $40 set.");

            SetCompletionFlag(false);
            _saveData.SetLinkedGame(false);
            _saveData.SetGlobalFlag(record.StandardGlobalFlag, value: false);
            _saveData.SetRoomFlag(
                record.PastFlagGroup,
                record.PastFlagRoom,
                (byte)record.PastRoomFlag,
                value: false);
            _saveData.SetMakuTreeState(5);
            _saveData.SetMakuMapTextPresent(0);
            _sound.ClearPlayRequestAudit();
            LoadValidationRoom(group, room);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.InitialWait ||
                postD3.Counter != record.InitialWait ||
                !_player.CutsceneControlled || remote.HasState,
                "Room 0:ba did not initialize its exact 90-update locked wait.");

            StepRoomEventFrames(record.InitialWait - 1);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.InitialWait ||
                postD3.Counter != 1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndLightning) != 0,
                "Room 0:ba lightning began before the 90th update.");
            StepRoomEventFrames(1);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.InitialFlash ||
                postD3.InitialFlashCounter != 1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndLightning) != 1,
                "Room 0:ba did not arm flashScreen b=$01 and play lightning " +
                "on the 90th update.");
            StepRoomEventFrames(record.FlashFrames);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.PreludeFadeOut ||
                _warpFade.Color.A != 0.0f,
                "Room 0:ba initial 13-update lightning flash drifted.");
            StepRoomEventFrames(record.FadeFrames);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.LoadPalace ||
                _warpFade.Color.A != 1.0f,
                "Room 0:ba did not complete fadeoutToWhite in 32 updates.");
            StepRoomEventFrames(1);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.PalaceFadeIn ||
                _rooms.ActiveGroup != record.PalaceGroup ||
                _rooms.CurrentRoom.Id != record.PalaceRoom ||
                postD3.Ambi is not { Record.Id: 0x4d, Record.SubId: 0x08 } ||
                postD3.Ambi.Position != new Vector2(0x48, 0x28) ||
                postD3.Nayru is not { Record.Id: 0x36, Record.SubId: 0x0e } ||
                postD3.Nayru.Position != new Vector2(0x58, 0x28) ||
                _player.Visible ||
                _sound.ActiveMusic != OracleSoundEngine.MusDisaster,
                "The cutscene-only room 1:16 load did not create Ambi " +
                "$4d:$08 and possessed Nayru $36:$0e in source order.");
            StepRoomEventFrames(record.FadeFrames);
            StepRoomEventFrames(record.PalaceWait);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.PalaceDialogue ||
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(
                    record.PalaceText),
                "Post-D3 Ambi did not show TX_1316 after fade completion and " +
                "the imported 60-update wait.");
            _dialogue.Close();
            StepRoomEventFrames(1);
            StepRoomEventFrames(record.PalacePostWait);
            StepRoomEventFrames(record.FadeFrames);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.LoadTower,
                "Ambi's post-TX_1316 wait/fade did not reach the tower scene.");
            StepRoomEventFrames(1);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.TowerFadeIn ||
                postD3.TowerScreen is not { } screen ||
                screen.BackgroundPixelHash != 0xd73336f411bc2213UL ||
                _hud.Visible ||
                _warpFade.ZIndex <= _hud.ZIndex,
                "Black Tower stage 1 did not use its distinct stage-2/middle " +
                "layout, two OAM lists, hidden HUD, and full-screen white fade.");
            StepRoomEventFrames(record.FadeFrames);
            StepRoomEventFrames(record.ExplanationWait);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.TowerDialogue ||
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(
                    record.ExplanationText) ||
                _dialogue.TextboxFlagsForValidation !=
                    record.ExplanationTextboxFlags,
                "Black Tower stage 1 did not show no-colors TX_1317 after " +
                "its imported 60-update wait.");
            _dialogue.Close();
            StepRoomEventFrames(1);
            StepRoomEventFrames(record.ExplanationPostWait);
            StepRoomEventFrames(record.FadeFrames);
            StepRoomEventFrames(1);
            FailIf(
                postD3.Stage != PostD3RemoteMakuStage.ReturnFadeIn ||
                _rooms.ActiveGroup != group || _rooms.CurrentRoom.Id != room ||
                _player.Position != new Vector2(record.ReturnX, record.ReturnY) ||
                _player.FacingVector != Vector2I.Down || !_player.Visible ||
                !_hud.Visible || !remote.Prepared ||
                !_saveData.HasRoomFlag(
                    record.PastFlagGroup,
                    record.PastFlagRoom,
                    (byte)record.PastRoomFlag) ||
                !_saveData.HasGlobalFlag(record.StandardGlobalFlag) ||
                _sound.ActiveMusic != 0,
                "The stage-1 return did not restore room 0:ba, Link at " +
                "$65,$58 facing down, status bar, STOPMUSIC, past room 1:76 " +
                "bit $01, and the unlinked flute-purchase flag.");
            StepRoomEventFrames(record.FadeFrames);
            FailIf(
                postD3.HasState || remote.Stage != RemoteMakuEventStage.Running ||
                remote.Record is not
                {
                    Group: group, Room: room, InteractionId: 0x8a,
                    SubId: 0, Var03: 4, EssenceMask: 0x04,
                    ConfettiKind: RemoteMakuConfettiKind.Present
                } || !_player.CutsceneControlled ||
                remote.CommandInstruction != 3 ||
                _sound.ActiveMusic != OracleSoundEngine.MusMakuTree,
                "Room 0:ba did not transfer ownership to imported " +
                "$8a:$00/v$04 after the return fade.");
            StepToRemoteDialogue();
            FailIf(
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(remoteText.StandardMessage) ||
                _saveData.MakuMapTextPresent != remoteRecord.StandardMapText,
                "Standard room 0:ba remote Maku guidance did not show " +
                "TX_05b4 and write present-map byte $b4.");
            FinishRemote(5, remoteRecord.StandardMapText);

            LoadValidationRoom(group, room);
            FailIf(
                postD3.HasState || remote.HasState || _roomEvents.Active,
                "Completed room 0:ba post-D3 route replayed on re-entry.");

            SetCompletionFlag(false);
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(record.StandardGlobalFlag, value: false);
            _saveData.SetRoomFlag(
                record.PastFlagGroup,
                record.PastFlagRoom,
                (byte)record.PastRoomFlag,
                value: false);
            _saveData.SetMakuTreeState(9);
            _saveData.SetMakuMapTextPresent(0);
            LoadValidationRoom(group, room);
            RunPreludeToRemote();
            FailIf(
                _saveData.HasGlobalFlag(record.StandardGlobalFlag) ||
                !_saveData.HasRoomFlag(
                    record.PastFlagGroup,
                    record.PastFlagRoom,
                    (byte)record.PastRoomFlag),
                "Linked $8a:$00/v$04 incorrectly enabled flute purchases or " +
                "failed to set past room 1:76 bit $01.");
            StepToRemoteDialogue();
            FailIf(
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(remoteText.LinkedMessage) ||
                _saveData.MakuMapTextPresent != remoteRecord.LinkedMapText,
                "Linked room 0:ba remote Maku guidance did not show TX_05c4 " +
                "and write present-map byte $c4.");
            FinishRemote(9, remoteRecord.LinkedMapText);
        }
        finally
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            SetEssences(originalEssences);
            SetCompletionFlag(originalRoomFlag);
            _saveData.SetRoomFlag(
                record.PastFlagGroup,
                record.PastFlagRoom,
                (byte)record.PastRoomFlag,
                originalPastFlag);
            _saveData.SetGlobalFlag(
                record.StandardGlobalFlag, originalFluteFlag);
            _saveData.SetLinkedGame(originalLinked);
            _saveData.SetMakuTreeState(originalMakuState);
            _saveData.SetMakuMapTextPresent(originalMapText);
            LoadValidationRoom(0, 0x11);
        }

        GD.Print(
            "Validated room 0:ba interaction $6b:$06 Essence/room predicates, " +
            "90-update lightning prelude, cutscene-only room 1:16 Ambi/Nayru " +
            "scene with TX_1316, Black Tower stage-1 TX_1317 presentation, " +
            "exact return position, dynamic $8a:$00/v$04 handoff, standard/" +
            "linked flute and past-room side effects, TX_05b4/TX_05c4 map " +
            "offsets, and re-entry suppression.");
    }
}
