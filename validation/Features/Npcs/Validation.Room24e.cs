using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom24eOldMan()
    {
        var commands = new NpcInteractionScriptDatabase().OldManRupees;
        FailIf(commands.Count != 18 ||
            commands[7] is not CutsceneWaitCommand { Frames: 8 } ||
            commands[8] is not CutsceneCheckRupeeDisplayCommand ||
            commands[14] is not CutsceneWaitCommand { Frames: 30 },
            "oldManScript_takesRupees lost its 8/30-update waits or HUD gate.");
        string Text(int id) => DialogueBox.PlainText(commands.OfType<CutsceneShowTextCommand>()
            .Single(command => command.TextId == id).Message);
        FailIf(!Text(0x3315).Contains("paying to fix") ||
            !Text(0x3316).Contains("Don't do it") ||
            !Text(0x3317).Contains("cannot take what") ||
            !Text(0x3317).Contains("Don't do it") || Text(0x3317).Contains("\\jump"),
            "TX_3315-TX_3317 lost source content or the final TX_3316 text jump.");

        foreach (bool batched in new[] { false, true })
        foreach (int wallet in new[] { 0, 1, 99, 250 })
        {
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, batched: batched);
            void PressA() => StepGameplayUpdates(1, Vector2.Zero,
                held: ["attack"], pressed: ["attack"], batched: batched);
            void RequireText(int id) => FailIf(!_dialogue.IsOpen ||
                DialogueBox.PlainText(_dialogue.CurrentMessage) != Text(id),
                $"Room 2:4e expected TX_{id:x4}, wallet={wallet}, batch={batched}.");
            NpcCharacter Approach()
            {
                NpcCharacter actor = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x2e);
                FailIf(actor.Position != new Vector2(40, 56) || actor.Record.SubId != 1 ||
                    actor.CurrentAnimationOpaquePixels == 0 || !actor.Record.CanFace ||
                    actor.Record.Implementation != NpcImplementationClassification.SpecializedNative,
                    "Room 2:4e $2e:$01 lost native $28/$38 position, visuals, or facing.");
                _player.WarpTo(new Vector2(80, 104));
                for (int i = 0; _player.Position.X > 40 && i < 80; i++) Step(movement: Vector2.Left);
                for (int i = 0; !actor.CanTalkTo(_player) && i < 80; i++)
                {
                    Step(movement: Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position), "Room 2:4e approach crossed solid room geometry.");
                }
                FailIf(!actor.CanTalkTo(_player) || !_entities.BlocksLink(actor.Position),
                    $"Room 2:4e old man is unreachable or not solid; Link={_player.Position}.");
                return actor;
            }

            _saveData.SetRoomFlag(2, 0x4e, 0x40, value: false);
            LoadValidationRoom(2, 0x4e);
            _inventory.AddRupees(wallet - _inventory.Rupees);
            _statusBar.SynchronizeRupees();
            NpcCharacter npc = Approach();
            OldManRupeesScriptHost host = _interactions.NpcScriptsForValidation.OldMan;
            PressA();
            RequireText(0x3315);
            FailIf(!host.InputDisabled || _inventory.Rupees != wallet,
                "Room 2:4e charged before TX_3315 closed or failed to disable input.");
            Vector2 heldPosition = _player.Position;
            Step(5, Vector2.Down);
            FailIf(_player.Position != heldPosition || _inventory.Rupees != wallet,
                "Room 2:4e dialogue did not hold Link and defer the payment helper.");
            _dialogue.Close();
            Step();
            FailIf(_inventory.Rupees != Math.Max(0, wallet - 100) ||
                host.Counter != (wallet == 0 ? 30 : 8) ||
                _saveData.HasRoomFlag(2, 0x4e, 0x40),
                "Room 2:4e payment/var3f/wait initialization differs from scriptHelp.oldMan_takeRupees.");
            if (wallet == 0)
            {
                Step(29);
                FailIf(_dialogue.IsOpen || host.Counter != 1,
                    "Room 2:4e broke text opened before its thirtieth wait update.");
                Step();
                RequireText(0x3317);
                _dialogue.Close();
                Step(2);
                FailIf(_saveData.HasRoomFlag(2, 0x4e, 0x40), "Room 2:4e broke branch set flag $40.");
                // A newly funded wallet still cannot be charged in this visit.
                _inventory.AddRupees(250);
            }
            else
            {
                Step(7);
                FailIf(host.Counter != 1 || !host.InputDisabled || _saveData.HasRoomFlag(2, 0x4e, 0x40),
                    "Room 2:4e payment completed before wait 8.");
                if (wallet >= 99)
                {
                    Step();
                    FailIf(host.CurrentCommandIndex != 8 || !host.InputDisabled ||
                        _saveData.HasRoomFlag(2, 0x4e, 0x40),
                        "Room 2:4e did not wait for the displayed wallet.");
                    for (int i = 0; _statusBar.DisplayedRupees != _inventory.Rupees && i < 110; i++) Step();
                    FailIf(_saveData.HasRoomFlag(2, 0x4e, 0x40),
                        "Room 2:4e observed the HUD catch-up before the following object update.");
                }
                Step();
                FailIf(host.CurrentCommandIndex != 10 || !host.InputDisabled ||
                    !_saveData.HasRoomFlag(2, 0x4e, 0x40),
                    "Room 2:4e did not commit flag $40 when the rupee gate opened.");
                Step();
                FailIf(host.CurrentCommandIndex != 11 || host.InputDisabled,
                    "Room 2:4e orroomflag must yield before enableinput on the next update.");
            }
            FailIf(host.InputDisabled, "Room 2:4e failed to release Link after payment/broke text.");
            int remaining = _inventory.Rupees;
            PressA();
            RequireText(0x3316);
            _dialogue.Close();
            Step(2);
            FailIf(_inventory.Rupees != remaining, "Room 2:4e charged again on a repeated conversation.");

            LoadValidationRoom(2, 0x4e);
            Approach();
            PressA();
            RequireText(wallet == 0 ? 0x3315 : 0x3316);
            // Cancellation releases the script's input owner and does not
            // invent a payment/completion for a still-open first message.
            _dialogue.Close();
            LoadValidationRoom(2, 0x4f);
            FailIf(host.HasState || host.InputDisabled || _inventory.Rupees != remaining,
                "Leaving room 2:4e leaked a script/input owner or charged during cancellation.");

            if (wallet == 250)
            {
                _saveData.SetRoomFlag(2, 0x4e, 0x40, value: false);
                LoadValidationRoom(2, 0x4e);
                _inventory.AddRupees(250 - _inventory.Rupees);
                _statusBar.SynchronizeRupees();
                Approach();
                PressA();
                _dialogue.Close();
                Step();
                Func<bool> disabled = _entities.InitializedObjectsDisabledSource;
                try
                {
                    _entities.InitializedObjectsDisabledSource = static () => true;
                    Step(3);
                    FailIf(host.Counter != 8,
                        "Room 2:4e script decremented while initialized interactions were disabled.");
                }
                finally { _entities.InitializedObjectsDisabledSource = disabled; }
                LoadValidationRoom(2, 0x4f);
                FailIf(host.HasState || host.InputDisabled || _inventory.Rupees != 150 ||
                    _saveData.HasRoomFlag(2, 0x4e, 0x40),
                    "Cancelling room 2:4e after subtraction rolled back money or committed flag $40 early.");
            }
        }
        GD.Print("Validated room 2:4e old man: actual geometry/A input, zero/short/full wallets, 8/30-update boundaries, HUD gate, repeat talks, re-entry, cancellation, and batched updates.");
    }
}
