using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ApproachRoom3f8Npc(int x, bool batched)
    {
        // Begin on the house's lower floor, then use the real collision loop.
        _player.WarpTo(new Vector2(x, 0x68));
        FailIf(_rooms.CurrentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
            $"3:f8 approach X=${x:x2} starts in an obstacle.");
        StepGameplayUpdates(48, Vector2.Up, ["move_up"], batched: batched);
        FailIf(_entities.FindTalkTarget(_player) is null,
            $"3:f8 NPC X=${x:x2} is unreachable through room geometry; Link={_player.Position}.");
    }

    private void TalkRoom3f8(bool batched)
    {
        StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
        for (int i = 0; i < 3 && !_dialogue.IsOpen; i++) StepGameplayUpdates(1, Vector2.Zero);
        FailIf(!_dialogue.IsOpen, "3:f8 A-button interaction did not open text.");
    }

    private void ValidateRoom3f8Npcs()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool savedNayru in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetGlobalFlag(0x14, false);
            _saveData.SetGlobalFlag(0x11, savedNayru);
            _saveData.SetLinkedGame(false);
            LoadValidationRoom(3, 0xf8);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            var actors = _entities.Entities<NpcCharacter>();
            var plen = actors.Single(n => n.Record.Id == 0xcc);
            FailIf(plen.Position != new Vector2(0x70, 0x48) || !plen.Active ||
                plen.TextId != (savedNayru ? 0x3715 : 0x3714) || plen.Record.CanFace ||
                plen.CurrentAnimationOpaquePixels == 0 ||
                actors.Any(n => n.Record.Id == 0x3d && n.Active),
                "3:f8 Plen $cc:$00 placement, story text, graphics, or unlinked $3d:$05 suppression changed.");
            ApproachRoom3f8Npc(0x70, batched);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                TalkRoom3f8(batched);
                string words = PlainWords(_dialogue.CurrentMessage);
                FailIf(!words.Contains(savedNayru ? "Queen Ambi" : "hundreds of years", StringComparison.Ordinal),
                    "3:f8 Plen lost source TX_3714/TX_3715 content.");
                int frame = plen.CurrentAnimationFrame;
                StepGameplayUpdates(16, Vector2.Zero, batched: batched);
                FailIf(plen.CurrentAnimationFrame == frame,
                    "3:f8 Plen lost plen.s's always-update animation during dialogue.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
        }

        foreach (bool linked in new[] { false, true })
        foreach (byte essence in new byte[] { 0, 1, 2, 3 })
        {
            _saveData.SetLinkedGame(linked);
            _saveData.WriteWramByte(0xc6bf, essence);
            _saveData.CommitInventoryChange();
            LoadValidationRoom(3, 0xf8);
            bool visible = _entities.Entities<NpcCharacter>().Any(n => n.Record.Id == 0x3d && n.Active);
            FailIf(visible != (linked && (essence & 2) != 0),
                $"3:f8 $3d:$05 must require linked+D2, linked={linked}, essences=${essence:x2}.");
        }

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(0xc6bf, 2);
            _saveData.SetGlobalFlag(0x59, false);
            _saveData.CommitInventoryChange();
            LoadValidationRoom(3, 0xf8);
            var lady = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3d);
            FailIf(lady.Position != new Vector2(0x58, 0x38) || !lady.Active ||
                lady.TextId != 0x4d2d || lady.CurrentAnimationOpaquePixels == 0,
                "3:f8 $3d:$05 lost its original placement/text/graphics.");
            ApproachRoom3f8Npc(0x58, batched);
            TalkRoom3f8(batched);
            FailIf(!_dialogue.ChoiceActive || !PlainWords(_dialogue.CurrentMessage).Contains("saved Holodrum", StringComparison.Ordinal),
                "3:f8 old lady must offer TX_4d2d.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "No..?", "3:f8 refusal lost TX_4d2e.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            TalkRoom3f8(batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("Mayor Ruul", StringComparison.Ordinal),
                "3:f8 explanation lost TX_4d2f.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.ChoiceActive || _dialogue.SelectedChoice != 1 || _saveData.HasGlobalFlag(0x59),
                "3:f8 declining the explanation must repeat without setting $59.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(!_saveData.HasGlobalFlag(0x59) || _saveData.ReadWramByte(0xc6fb) != 0x29 ||
                _dialogue.CurrentMessage.Contains("\\secret1", StringComparison.Ordinal),
                "3:f8 Ruul secret must write flag $59 / short index $29 and substitute text.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.ChoiceActive || _dialogue.SelectedChoice != 1,
                "3:f8 secret repeat lost its No choice.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Don't forget!", "3:f8 final text must be TX_4d31.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            TalkRoom3f8(batched);
            FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("Mayor Ruul", StringComparison.Ordinal),
                "3:f8 extra-text NPC must repeat its explanation after completing the secret.");
            _dialogue.Close();
            LoadValidationRoom(3, 0xf8);
            FailIf(!_saveData.HasGlobalFlag(0x59), "3:f8 Ruul secret flag did not persist across re-entry.");
        }
    }

    private void ValidatePlenSecret()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetLinkedGame(false);
            _saveData.SetGlobalFlag(0x14);
            _saveData.SetGlobalFlag(0x11); // FINISHED takes precedence over SAVED_NAYRU.
            _saveData.SetGlobalFlag(0x67, false);
            _saveData.SetGlobalFlag(0x71, false);
            LoadValidationRoom(3, 0xf8);
            var plen = _roomEvents.Get<PlenEvent>();
            ApproachRoom3f8Npc(0x70, batched);
            TalkRoom3f8(batched);
            FailIf(!_dialogue.ChoiceActive || !plen.BlocksGameplay || !plen.FreezesNonInteractionObjects,
                "3:f8 finished-game Plen must offer TX_3700 and apply disableinput $81.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || !plen.BlocksGameplay, "Plen skipped the 30-update prompt wait.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "I see...", "Plen No must show TX_3701.");
            _dialogue.Close();
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(plen.BlocksGameplay, "Plen refusal did not restore input.");

            // MENU_SECRET's cancel button returns wTextInputResult != 0.
            TalkRoom3f8(batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Screen!.SelectSecretLowerOption(2);
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot(
                pressed: ["attack"], justPressed: ["attack"], movement: Vector2.Zero));
            try { _secretEntry.Update(1.0 / 60.0); }
            finally { Input.EndOriginalUpdate(); }
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            FailIf(_secretEntry.IsActive || _gameplayPause.IsLeased ||
                PlainWords(_dialogue.CurrentMessage) != "What's that?" ||
                _saveData.HasGlobalFlag(0x67) || _saveData.HasGlobalFlag(0x71),
                "Cancelling PLEN_SECRET must show TX_3703 without setting $67/$71.");
            _dialogue.Close();
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(plen.BlocksGameplay, "Plen failed-secret branch retained input.");

            TalkRoom3f8(batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            FailIf(_secretEntry.IsActive, "Plen opened MENU_SECRET before the prompt wait completed.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!_secretEntry.IsActive || !_gameplayPause.IsLeased,
                "Plen PLEN_SECRET $03 did not acquire MENU_SECRET.");
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff]);
            FailIf(!_secretEntry.IsActive || _saveData.HasGlobalFlag(0x67),
                "Invalid Plen secret must remain in the menu without setting $67.");
            _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(3, _saveData));
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            FailIf(_secretEntry.IsActive || _gameplayPause.IsLeased, "Plen secret menu did not close cleanly.");
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            FailIf(_saveData.HasGlobalFlag(0x67), "Plen set BEGAN $67 before the post-menu wait.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!_saveData.HasGlobalFlag(0x67) || _saveData.HasGlobalFlag(0x71) ||
                !PlainWords(_dialogue.CurrentMessage).Contains("oddly dressed", StringComparison.Ordinal),
                "Plen valid secret lost BEGAN $67 or TX_3702.");
            int rings = _inventory.UnappraisedRingCount;
            _dialogue.Close();
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            FailIf(_saveData.HasGlobalFlag(0x71) || _inventory.UnappraisedRingCount != rings,
                "Plen granted the ring before his 30-update reward wait.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!_saveData.HasGlobalFlag(0x71) || _inventory.UnappraisedRingCount != rings + 1 ||
                // createRingTreasure sets bit 6 on the unappraised ring byte.
                _inventory.UnappraisedRingAt(rings) != 0x6f,
                $"Plen must award one unappraised SPIN_RING $2f and set DONE $71: done={_saveData.HasGlobalFlag(0x71)}, rings={_inventory.UnappraisedRingCount}/{rings}, ring=${_inventory.UnappraisedRingAt(rings):x2}, text={_dialogue.CurrentMessage}.");
            // The wait was installed in the grant update, then frozen by text.
            _dialogue.Close();
            StepGameplayUpdates(29, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || !plen.BlocksGameplay,
                "Plen's final 30-update wait ended early after the ring text.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Remember--it needs appraisal.",
                "Plen lost TX_3704 at the end of his final 30-update wait.");
            _dialogue.Close();
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(plen.BlocksGameplay || _player.CutsceneControlled,
                "Plen reward completion retained input ownership.");
            TalkRoom3f8(batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Remember--it needs appraisal." ||
                _inventory.UnappraisedRingCount != rings + 1,
                "Completed Plen secret must show TX_3705 without duplicating its ring.");
            plen.Cancel();
            _dialogue.Close();
            FailIf(plen.BlocksGameplay || plen.HasState, "Plen cancellation retained state/input.");
            LoadValidationRoom(3, 0xf8);
            ApproachRoom3f8Npc(0x70, batched);
            TalkRoom3f8(batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Remember--it needs appraisal.",
                "Plen DONE $71 did not survive room re-entry.");
            _dialogue.Close();
        }
    }
}
