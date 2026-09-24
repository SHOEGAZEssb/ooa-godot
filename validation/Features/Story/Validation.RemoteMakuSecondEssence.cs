using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRemoteMakuHudPlacement()
    {
        bool originalBottom = _presentationSettings.HudBottom;
        try
        {
            foreach (bool bottom in new[] { false, true })
            foreach (bool batched in new[] { false, true })
            foreach (bool past in new[] { false, true })
            // Completion, cancellation while hidden, cancellation during return fade.
            foreach (int cancellation in new[] { 0, 1, 2 })
            {
                RemoteMakuEvent cutscene = past
                    ? _roomEvents.Get<RemoteMakuSecondEssenceEvent>()
                    : _roomEvents.Get<RemoteMakuFourthEssenceEvent>();
                int group = past ? 1 : 0;
                int room = past ? 0x83 : 0x03;
                _presentationSettings.HudBottom = bottom;
                _saveData.WriteWramByte(0xc6bf, past ? (byte)0x02 : (byte)0x08);
                _saveData.CommitInventoryChange();
                _saveData.SetRoomFlag(group, room, 0x40, false);
                LoadValidationRoom(group, room);
                _scene.ApplyHudPlacement(bottom, _transitions);
                bool sawHidden = false;
                bool sawDialogue = false;
                bool sawFullFade = false;
                bool cancelled = false;
                int fieldTop = bottom ? 0 : 16;

                void CheckLayout()
                {
                    FailIf(_hud.Position.Y != (bottom ? 128 : 0) ||
                        _scene.RoomLoadReveal.Position.Y != fieldTop ||
                        _presentationSettings.HudBottom != bottom,
                        $"Remote Maku {group:x}:{room:x2} changed the selected HUD layout.");
                    Vector2 screen = _transitions.WorldToGameplayScreen(_player.Position);
                    FailIf(!(_player.Position - _roomCamera.Position + new Vector2(80, 72))
                            .IsEqualApprox(screen + new Vector2(0, fieldTop)),
                        "Remote Maku hide/show shifted the gameplay camera.");
                    bool full = _warpFade.Size.Y == 144;
                    FailIf(_warpFade.Position.Y != (full ? 0 : fieldTop),
                        "Remote Maku fade must cover the entire viewport while event-owned, then restore the field.");
                }

                CheckLayout();
                for (int frame = 0; frame < 900 && cutscene.HasState; frame += 4)
                {
                    StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                    CheckLayout();
                    sawHidden |= _hud.StatusBarHidden;
                    sawFullFade |= _warpFade.Size.Y == 144;
                    if ((cancellation == 1 && _hud.StatusBarHidden) ||
                        (cancellation == 2 && _warpFade.Size.Y == 144))
                    {
                        LoadValidationRoom(0, 0x11);
                        StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                        cancelled = true;
                        break;
                    }
                    if (_dialogue.IsOpen)
                    {
                        sawDialogue = true;
                        float y = _dialogue.Position.Y;
                        _scene.ApplyHudPlacement(!bottom, _transitions);
                        FailIf(_dialogue.Position.Y != y + (bottom ? 16 : -16),
                            "Remote Maku dialogue must follow the gameplay field while the HUD is hidden.");
                        _scene.ApplyHudPlacement(bottom, _transitions);
                        _dialogue.Close();
                    }
                }
                CheckLayout();
                FailIf(!sawHidden || cutscene.HasState || _hud.StatusBarHidden ||
                    _player.CutsceneControlled || _warpFade.Size.Y != 128 ||
                    (cancellation == 0 && (!sawDialogue || !sawFullFade ||
                        !_saveData.HasRoomFlag(group, room, 0x40))) ||
                    (cancellation != 0 && (!cancelled || _saveData.HasRoomFlag(group, room, 0x40))),
                    "Remote Maku HUD lifecycle did not complete/cancel with source hide/show and fade ownership intact.");
                if (cancellation == 0)
                {
                    LoadValidationRoom(group, room);
                    StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                    CheckLayout();
                    FailIf(cutscene.HasState, "Completed remote Maku event replayed on re-entry.");
                }
            }
        }
        finally
        {
            _dialogue.Close();
            _presentationSettings.HudBottom = originalBottom;
            LoadValidationRoom(0, 0x11);
            _scene.ApplyHudPlacement(originalBottom, _transitions);
        }
        GD.Print("Validated remote Maku top/bottom HUD placement in both eras through hidden dialogue, full-screen fade, completion, cancellation and re-entry with individual/batched gameplay updates.");
    }

    private void ValidateRemoteMakuSecondEssenceCutscene()
    {
        const int group = 1;
        const int room = 0x83;
        RemoteMakuSecondEssenceEvent cutscene =
            _roomEvents.Get<RemoteMakuSecondEssenceEvent>();
        RemoteMakuEventRecord record = cutscene.Database.Record;
        var text = (CutsceneShowTextVariantsCommand)
            cutscene.Database.Commands[10];
        byte originalEssences = _saveData.ReadWramByte(0xc6bf);
        int originalMakuState = _saveData.MakuTreeState;
        int originalPastMapText = _saveData.MakuMapTextPast;
        int originalPresentMapText = _saveData.MakuMapTextPresent;
        bool originalLinked = _saveData.IsLinkedGame;
        bool originalRoomFlag = _saveData.HasRoomFlag(
            group, room, (byte)record.RoomFlag);

        void SetRoomFlag(bool value) => _saveData.SetRoomFlag(
            group, room, (byte)record.RoomFlag, value);

        void SetEssences(int value)
        {
            if (_saveData.WriteWramByte(0xc6bf, (byte)value))
                _saveData.CommitInventoryChange();
        }

        void StepUntilDialogue()
        {
            for (int frame = 0; frame < 700 && !_dialogue.IsOpen; frame++)
                StepRoomEventFrames(1);
            FailIf(
                !_dialogue.IsOpen,
                "Room 1:83 second-Essence remote Maku dialogue did not " +
                "open within its imported fade/confetti waits.");
        }

        void FinishAfterDialogue(int initialState, int expectedMapText)
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
                _saveData.MakuMapTextPast != expectedMapText ||
                _saveData.MakuMapTextPresent != 0x5a ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(group, room),
                "Room 1:83 second-Essence remote Maku completion did not " +
                "restore input/music, set room flag $40, update only the " +
                "past map text, and increment wMakuTreeState.");
        }

        try
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            SetRoomFlag(false);
            SetEssences(0);
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Room 1:83 remote Maku event ignored its second-Essence " +
                "bit-$02 predicate.");

            SetEssences(record.EssenceMask);
            SetRoomFlag(true);
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Room 1:83 remote Maku event replayed with room flag $40 set.");

            SetRoomFlag(false);
            _saveData.SetLinkedGame(false);
            _saveData.SetMakuTreeState(4);
            _saveData.SetMakuMapTextPast(0);
            _saveData.SetMakuMapTextPresent(0x5a);
            _sound.ClearPlayRequestAudit();
            LoadValidationRoom(group, room);
            FailIf(
                cutscene.Stage != RemoteMakuEventStage.Running ||
                cutscene.Record is not
                {
                    Group: group,
                    Room: room,
                    InteractionId: 0x8a,
                    SubId: 0x01,
                    Var03: 0x03,
                    EssenceMask: 0x02,
                    RoomFlag: 0x40,
                    ConfettiKind: RemoteMakuConfettiKind.Past,
                    ConfettiPieces: 12,
                    ConfettiHold2: 60
                } ||
                cutscene.Database.Commands[7] is not
                    CutsceneNativeCommand { Handler: "SpawnPastConfetti" },
                "Room 1:83 did not select its imported $8a:$01/v$03 " +
                "second-Essence lane and native past-confetti branch.");

            StepRoomEventFrames(1);
            FailIf(
                !_player.CutsceneControlled ||
                cutscene.TextboxFlags != 0x04 ||
                cutscene.CommandInstruction != 3 ||
                _sound.ActiveMusic != OracleSoundEngine.MusMakuTree,
                "Room 1:83 remote Maku input lock, alternate textbox " +
                "palette, or Maku Tree music drifted on its first update.");
            StepRoomEventFrames(1);
            StepRoomEventFrames(record.InitialWait - 1);
            StepRoomEventFrames(1);
            StepRoomEventFrames(32);
            StepRoomEventFrames(record.FadeFrames - 2 - 32);
            StepRoomEventFrames(1);
            StepRoomEventFrames(1);
            FailIf(
                cutscene.Confetti is not
                { SpawnedPieces: 0, LivePieces: 0 } ||
                cutscene.CommandInstruction != 8 ||
                cutscene.CommandCounter != record.ConfettiHold1 ||
                !_hud.StatusBarHidden ||
                _roomView.BackgroundFadeAlpha != 1.0f,
                "Room 1:83 did not initialize $62:$01 after the exact " +
                "wait-40 and delay-2 black-fade boundaries.");

            StepRoomEventFrames(1);
            Vector2 cameraOrigin = _roomCamera.Position - new Vector2(
                OracleRoomData.ViewportWidth / 2.0f,
                OracleRoomData.ScreenHeight / 2.0f -
                    OracleRoomData.GameplayScreenTop);
            FailIf(
                cutscene.Confetti is not
                { SpawnedPieces: 1, LivePieces: 1 } ||
                cutscene.Confetti.PiecePositions.Single() !=
                    cameraOrigin + new Vector2(0x10, 0x80) ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndMakuTreePast) != 0,
                "Past Maku confetti did not create its first stationary " +
                "$80,$10 leaf one update after initialization.");
            StepRoomEventFrames(1);
            FailIf(
                cutscene.Confetti.PiecePositions.Single() !=
                    cameraOrigin + new Vector2(0x13, 0x7d),
                "The first past Maku leaf lost its initial $0400/$fd80 " +
                "velocity or first $fff0 horizontal deceleration.");
            StepRoomEventFrames(7);
            FailIf(
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndMakuTreePast) != 0,
                "SND_MAKU_TREE_PAST played before the source 10-update " +
                "initial counter expired.");
            StepRoomEventFrames(1);
            FailIf(
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndMakuTreePast) != 1,
                "SND_MAKU_TREE_PAST did not play on the source 10th " +
                "spawner update.");

            StepUntilDialogue();
            FailIf(
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(text.StandardMessage) ||
                _saveData.MakuMapTextPast != record.StandardMapText ||
                _saveData.MakuMapTextPresent != 0x5a ||
                cutscene.Confetti is not
                    { Finished: true, SpawnedPieces: 12, LivePieces: 0 } ||
                _sound.PlayRequestsFor(
                    OracleSoundEngine.SndMakuTreePast) != 5 ||
                !_hud.StatusBarHidden ||
                _dialogue.TextboxFlagsForValidation != 0x04,
                "Room 1:83 did not show standard TX_05b3, write past map " +
                "byte $b3, and complete all 12 source leaves with five " +
                "SND_MAKU_TREE_PAST requests.");
            FinishAfterDialogue(initialState: 4, record.StandardMapText);

            LoadValidationRoom(group, room);
            FailIf(
                cutscene.HasState || _roomEvents.Active,
                "Completed room 1:83 second-Essence remote Maku event " +
                "replayed on re-entry.");

            SetRoomFlag(false);
            _saveData.SetLinkedGame(true);
            _saveData.SetMakuTreeState(8);
            _saveData.SetMakuMapTextPast(0);
            _saveData.SetMakuMapTextPresent(0x5a);
            LoadValidationRoom(group, room);
            StepUntilDialogue();
            FailIf(
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(text.LinkedMessage) ||
                _saveData.MakuMapTextPast != record.LinkedMapText ||
                _saveData.MakuMapTextPresent != 0x5a,
                "Linked room 1:83 remote Maku guidance did not apply " +
                "TX_05c3/map byte $c3 exclusively to past map state.");
            FinishAfterDialogue(initialState: 8, record.LinkedMapText);
        }
        finally
        {
            if (_dialogue.IsOpen)
                _dialogue.Close();
            SetEssences(originalEssences);
            _saveData.SetMakuTreeState(originalMakuState);
            _saveData.SetMakuMapTextPast(originalPastMapText);
            _saveData.SetMakuMapTextPresent(originalPresentMapText);
            _saveData.SetLinkedGame(originalLinked);
            SetRoomFlag(originalRoomFlag);
            LoadValidationRoom(0, 0x11);
        }

        GD.Print(
            "Validated room 1:83 second-Essence remote Maku bit-$02 and " +
            "room-$40 predicates, imported $8a:$01/v$03 lane, 12-piece " +
            "past confetti motion/sound, TX_05b3/TX_05c3 past-map offsets, " +
            "HUD/fade cadence, music/input restore, Maku-state increment, " +
            "and re-entry suppression.");
    }
}
