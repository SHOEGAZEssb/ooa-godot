using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBipinBlossomNaming()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            foreach (string field in new[] { "wChildStatus", "wChildStage", "wNextChildStage", "wc6e2", "wChildPersonality" })
                _saveData.WriteField(field, 0);
            _saveData.WriteWramBytes(AgesWramAddress.wKidName, new byte[6]);
            _saveData.CommitInventoryChange();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _runtimeState.SetField("wSeedTreeRefilledBitset", 0);
            LoadValidationRoom(2, 0xea);
            Step(3);
            var slot = _entities.EntityAdapters<FamilyNpcRoomEntity>().Single(s => s.Npc.Record.Id == InteractionId.Blossom);
            var blossom = slot.Npc;
            var script = slot.Script!;
            var menu = _gameplayMenus!.KidName;

            Talk();
            OpenMenu();
            Submit();
            FailIf(_runtimeState.ReadField("wTextInputResult") != 1 || _saveData.ChildName.Length != 0,
                "MENU_KIDNAME empty input must return1 without writing wKidName.");
            ReturnFromMenu();
            RequireText("more thought");
            _dialogue.Close(); Step();
            RequireEnabled();

            Talk(); OpenMenu();
            Type('P'); Type('i'); Type('p');
            Submit();
            FailIf(_runtimeState.ReadField("wTextInputResult") != 0 || _saveData.ChildName != "Pip" || _saveData.ChildNamed,
                "MENU_KIDNAME must write the valid candidate before TX_4407 confirmation, without naming the child.");
            ReturnFromMenu();
            FailIf(!_dialogue.ChoiceActive || !_dialogue.CurrentMessage.Contains("Pip", StringComparison.Ordinal),
                "blossomScript0 must read result0 and show TX_4407 with its live candidate.");
            _dialogue.SubmitChoiceForValidation(1); Step();
            FailIf(!menu.IsActive || script.Counter != 30 || _saveData.ChildNamed,
                "TX_4407 No must reopen MENU_KIDNAME and arm its native30-update wait.");
            Step(22);
            FailIf(menu.Screen is not { EnteredName: "Pip", NameEntryPosition: 0 },
                "Reopening MENU_KIDNAME must copy wKidName and reset the text-entry cursor to0.");
            Submit(); ReturnFromMenu();
            _dialogue.SubmitChoiceForValidation(0); Step();
            // P=$50, i=$69, p=$70: low nibbles sum9, status=(9%3)+1.
            FailIf(!_saveData.ChildNamed || _saveData.ReadField("wChildStatus") != 1 ||
                _saveData.ReadField("wChildStage") != 0 || _saveData.ReadField("wNextChildStage") != 1 || script.Counter != 30,
                "blossomScript0 confirmation must perform separate status/bit/next-stage writes before wait30.");
            Step(29);
            FailIf(_dialogue.IsOpen || script.Counter != 1, "TX_4408 appeared before30 eligible actor updates.");
            Step(); RequireText("Come visit us");
            _dialogue.Close(); Step(); RequireEnabled();

            var bipin = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Bipin);
            FailIf(bipin.TextId != 0x4301 || blossom.TextId != 0x4409 ||
                bipin.BaseRecord.TextId != 0x4300 || blossom.BaseRecord.TextId != 0x4400 ||
                !bipin.Message.Contains("Pip", StringComparison.Ordinal),
                "Native naming must refresh live family dialogue without changing imported base records.");
            _saveData.WriteField("wc6e2", 0); _saveData.CommitInventoryChange();
            FailIf(bipin.TextId != 0x4300 || blossom.TextId != 0x4400, "Clearing the named bit must reverse live text selection.");
            _saveData.WriteField("wc6e2", 1); _saveData.CommitInventoryChange();
            ApproachLibraryActor(blossom, batched); Tap("attack");
            RequireText("will grow strong");
            FailIf(!_dialogue.IsOpen || menu.IsActive || !blossom.Message.Contains("Pip", StringComparison.Ordinal),
                "The completed actor must repeat TX_4409 without reopening the menu.");
            _dialogue.Close(); Step();
            LoadValidationRoom(2, 0xea); Step(3);
            blossom = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Blossom);
            ApproachLibraryActor(blossom, batched); Tap("attack");
            RequireText("will grow strong");
            FailIf(!_dialogue.IsOpen || menu.IsActive || blossom.TextId != 0x4409,
                "Re-entry must retain the named branch of blossomScript0.");
            _dialogue.Close(); Step();

            _saveData.WriteField("wc6e2", 0); _saveData.CommitInventoryChange();
            LoadValidationRoom(2, 0xea); Step(3);
            slot = _entities.EntityAdapters<FamilyNpcRoomEntity>().Single(s => s.Npc.Record.Id == InteractionId.Blossom);
            blossom = slot.Npc; script = slot.Script!;
            Talk(); OpenMenu();
            LoadValidationRoom(2, 0xeb);
            FailIf(menu.IsActive || !_menuLifecycle.IsIdle, "Room cancellation must retire MENU_KIDNAME and its pause owner.");
            RequireEnabled();

            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Tap(string action) => StepGameplayUpdates(1, Vector2.Zero, [action], [action], batched);
            void RequireText(string text) => FailIf(!_dialogue.IsOpen ||
                !PlainWords(_dialogue.CurrentMessage).Contains(text, StringComparison.Ordinal), $"Missing native Blossom text containing '{text}'.");
            void RequireEnabled() => FailIf(_runtimeState.ReadField("wDisabledObjects") != 0 ||
                _runtimeState.ReadField("wMenuDisabled") != 0 || _player.CutsceneControlled,
                "Blossom completion/cancellation must release both native masks without private Link control.");
            void Talk()
            {
                ApproachLibraryActor(blossom, batched); Tap("attack");
                FailIf(_runtimeState.ReadField("wDisabledObjects") != 0x91 || _dialogue.IsOpen,
                    "Blossom's A probe must yield after setdisabledobjectsto91 before TX_4400.");
                Step(); RequireText("would you call");
            }
            void OpenMenu()
            {
                _dialogue.Close(); Step();
                FailIf(!menu.IsActive || script.Counter != 30 || menu.Screen is not null,
                    "Blossom must request the shared menu and arm wait30 before its opening fade.");
                Step(22);
                FailIf(menu.Screen is null || script.Counter != 30 || _dialogue.IsOpen,
                    "Both menu fades must freeze the actor and retain the armed30-update wait.");
            }
            void Submit()
            {
                Tap("inventory");
                FailIf(menu.Screen?.NameLowerChoice != 2 || !menu.IsActive,
                    "The first Start must select OK without committing.");
                Tap("inventory"); Step();
            }
            void ReturnFromMenu()
            {
                Step(22);
                FailIf(menu.IsActive || script.Counter != 30, "The closing menu update must not also advance Blossom's wait.");
                Step(29);
                FailIf(_dialogue.IsOpen || script.Counter != 1, "Blossom resumed before its native post-menu wait boundary.");
                Step();
            }
            void Type(char character)
            {
                int letter = char.ToUpperInvariant(character) - 'A';
                int row = letter / 6, column = letter % 6 + (char.IsLower(character) ? 6 : 0);
                for (int count = 0; (menu.Screen!.NameCursor >> 4) != row && count < 6; count++) Tap("move_down");
                for (int count = 0; (menu.Screen!.NameCursor & 15) != column && count < 12; count++) Tap("move_right");
                FailIf(menu.Screen!.NameCursor != row * 16 + column, "The shared name keyboard did not reach the source-derived character cell.");
                Tap("attack");
            }
        }
        GD.Print("Validated native blossomScript0 through collision approach, empty/name/No/Yes, both30-update waits, menu suspension, repeat, cancellation and re-entry in both host schedules.");
    }
}
