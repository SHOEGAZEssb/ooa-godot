using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayCookScript()
    {
        var commands = CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/tokay_cook_commands.tsv");
        FailIf(commands.Count != 35 ||
            !commands.OfType<CutsceneWaitCommand>().Select(c => c.Frames).SequenceEqual(
                new[] { 30, 30, 30, 30, 30, 40, 30 }) ||
            commands[22] is not CutsceneWriteObjectByteCommand { Address: 0x3f, Value: 1 } ||
            commands[24] is not CutsceneMemoryGateCommand { Binding: "CookAwayFromStart", Value: 0 } ||
            commands[25] is not CutsceneWriteObjectByteCommand { Address: 0x3f, Value: 0 } ||
            commands[29] is not CutsceneGiveItemCommand { TreasureId: 0x41, Parameter: 3 } ||
            commands.OfType<CutsceneBranchYieldCommand>().Count() != 4,
            "tokayCookScript lost source waits, $3e/$3f handshake, Tasty Meat or wBigBuffer jump boundaries.");
        var texts = new TokayInteractionDatabase();
        var results = new List<string[]>();
        foreach (bool batched in new[] { false, true })
        {
            var observations = new List<string>();
            _saveData.SetRoomFlag(2, 0x3f, 0x20, false);
            _inventory.GiveTreasure(0x41, 2);
            LoadValidationRoom(2, 0x3f);
            TokayCookEvent cooking = _roomEvents.Get<TokayCookEvent>();
            TokayCharacter cook = _entities.Entities<TokayCharacter>().Single(n => n.Record.SubId == 0x05);
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            }
            void ExpectText(int id)
            {
                FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(texts.Text(id)),
                    $"Cook expected TX_{id:x4} at command {cooking.CurrentCommandIndex}.");
                observations.Add($"{id:x4}:{cooking.Counter}:{cook.Position}:{cook.ScriptDrawOffset}:{_player.CutsceneControlled}");
            }
            void PressA()
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                Step(1);
            }
            void Talk(int text)
            {
                FailIf(!cook.CanTalkTo(_player), $"Cook is unreachable from {_player.Position}.");
                PressA();
                ExpectText(text);
            }
            void CloseAndWait(int frames)
            {
                _dialogue.Close();
                Step(frames);
                FailIf(_dialogue.IsOpen || cooking.Counter != 1,
                    $"Cook ended wait {frames} before its zero update (counter {cooking.Counter}).");
                Step(1);
            }

            // Approach from the open floor below the counter using normal Link collision.
            _player.WarpTo(new Vector2(0x48, 0x58));
            StepGameplayUpdates(48, Vector2.Up, ["move_up"], ["move_up"], batched);
            Step(1);
            FailIf(_player.Position.DistanceTo(cook.Position) < 8 ||
                _rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision != 0,
                "Cook fixture bypassed the room's collision geometry.");
            _inventory.GiveTreasure(0x41, 1);
            Talk(0x0a00);
            CloseAndWait(30);
            ExpectText(0x0a09);
            _dialogue.Close();
            Step(3);
            FailIf(cooking.BlocksGameplay || _inventory.TradeItem != 1,
                "Missing Stink Bag changed inventory or retained input.");
            _inventory.GiveTreasure(0x41, 2);
            Talk(0x0a00);
            CloseAndWait(30);
            ExpectText(0x0a01);
            _dialogue.SubmitChoiceForValidation(1);
            Step(31);
            ExpectText(0x0a08);
            _dialogue.Close();
            Step(3);
            FailIf(cooking.BlocksGameplay || _inventory.TradeItem != 2 ||
                _saveData.HasRoomFlag(2, 0x3f, 0x20),
                "Declining the cook trade changed its item/room flag or retained input.");
            Talk(0x0a00);
            CloseAndWait(30);
            ExpectText(0x0a01);
            _dialogue.SubmitChoiceForValidation(0);
            Step(30);
            FailIf(_dialogue.IsOpen || cooking.Counter != 1, "Cook resolved its choice before wait 30.");
            Step(1);
            for (int text = 0x0a02; text <= 0x0a04; text++)
            {
                ExpectText(text);
                CloseAndWait(30);
            }
            FailIf(!cooking.Jumping || _dialogue.IsOpen,
                "writeobjectbyte $3f,$01 must start the native tail before the next update's TX_0a05.");
            Step(1);
            ExpectText(0x0a05);
            Vector2 start = cook.Position;
            int sounds = _sound.PlayRequestsFor(0x53);
            Step(10);
            FailIf(cook.Position == start || !_dialogue.IsOpen,
                $"Cook native tail during TX_0a05: {start} -> {cook.Position}, jumping={cooking.Jumping}, z={cook.ScriptDrawOffset}, command={cooking.CurrentCommandIndex}, dialogue={_dialogue.IsOpen}, batch={batched}, player={_player.Position}.");
            Step(500);
            FailIf(_sound.PlayRequestsFor(0x53) < sounds + 6,
                "Cook did not repeat its six native jump paths during long dialogue.");
            _dialogue.Close();
            int returning = 0;
            while (cooking.CurrentCommandIndex != 26 && returning++ < 600) Step(1);
            FailIf(returning >= 600 || cooking.Jumping || cook.Position != new Vector2(0x48, 0x28),
                "Cook failed to return to $48,$28 before clearing $3f.");
            // The successful checkobjectbyteeq and writeobjectbyte each yield.
            Step(40);
            FailIf(_dialogue.IsOpen || cooking.Counter != 1, "Cook skipped the native-return wait 40.");
            Step(1);
            ExpectText(0x0a06);
            CloseAndWait(30);
            for (int update = 0; update < 90 && !_dialogue.IsOpen; update++) Step(1);
            FailIf(!_dialogue.IsOpen || _inventory.TradeItem != 3,
                "Cook failed to grant TREASURE_TRADEITEM:$03.");
            _dialogue.Close();
            Step(3);
            FailIf(cooking.BlocksGameplay || _player.CutsceneControlled ||
                !_saveData.HasRoomFlag(2, 0x3f, 0x20),
                "Cook failed to restore input and retain the trade room flag.");
            Talk(0x0a07);
            _dialogue.Close();
            Step(3);
            cooking.Cancel();
            cooking.Cancel();
            FailIf(_player.CutsceneControlled || cook.ScriptOwnsNativeUpdate || cooking.HasState,
                "Repeated cook cancellation retained native or input ownership.");
            results.Add(observations.ToArray());
        }
        FailIf(!results[0].SequenceEqual(results[1]),
            "Cook state differs between individual updates and batched host frames.");
        ValidateTokayCookCancellation();
    }

    private void ValidateTokayCookCancellation()
    {
        foreach (bool batched in new[] { false, true })
        {
            _saveData.SetRoomFlag(2, 0x3f, 0x20, false);
            _inventory.GiveTreasure(0x41, 2);
            _inventory.LoseTreasure(TreasureDatabase.TreasurePotion);
            _player.RefillHealth();
            LoadValidationRoom(2, 0x3f);
            TokayCookEvent cooking = _roomEvents.Get<TokayCookEvent>();
            TokayCharacter cook = _entities.Entities<TokayCharacter>().Single(n => n.Record.SubId == 5);
            _player.WarpTo(new Vector2(0x48, 0x58));
            StepGameplayUpdates(48, Vector2.Up, ["move_up"], ["move_up"], batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!_dialogue.IsOpen || !cooking.BlocksGameplay, "Cook re-entry did not restore its A-button route.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero);
            int command = cooking.CurrentCommandIndex;
            int counter = cooking.Counter;
            // specialObjectAnimationsAndDamage.s sets wLinkDeathTrigger=$ff
            // on lethal damage before the later interactionRunScript pass.
            FailIf(!_player.ApplyDamage(_player.MaxHealthQuarters) || !_player.IsDying,
                "Cook death fixture did not arm Link's death trigger.");
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(cooking.CurrentCommandIndex != command || cooking.Counter != counter,
                "interactionRunScript decremented a cook counter while Link was dying.");
            cooking.Cancel();
            // Death normally replaces the scene. Reset only this fixture's
            // death lifecycle before exercising the separate cancellation path.
            foreach (string field in new[] { "_deathPending", "_deathAnimationActive", "_deathSlowFadeRequested" })
                typeof(Player).GetField(field, System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!.SetValue(_player, false);
            _player.RefillHealth();
            _player.WarpTo(new Vector2(0x48, 0x58));
            LoadValidationRoom(2, 0x3f);
            cook = _entities.Entities<TokayCharacter>().Single(n => n.Record.SubId == 5);
            _player.WarpTo(new Vector2(0x48, 0x58));
            StepGameplayUpdates(48, Vector2.Up, ["move_up"], ["move_up"], batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            for (int update = 0; !cooking.Jumping && update < 300; update++)
            {
                if (_dialogue.ChoiceActive) _dialogue.SubmitChoiceForValidation(0);
                else if (_dialogue.IsOpen) _dialogue.Close();
                StepGameplayUpdates(1, Vector2.Zero);
            }
            FailIf(!cooking.Jumping || !cooking.BlocksGameplay,
                $"Cook never reached native cancellation: command={cooking.CurrentCommandIndex}, " +
                $"counter={cooking.Counter}, dead={_player.IsDying}, text={_dialogue.CurrentMessage}, " +
                $"player={_player.Position}, sensitive={cook.ScriptButtonSensitive}.");
            StepGameplayUpdates(3, Vector2.Zero);
            cooking.Cancel();
            cooking.Cancel();
            _dialogue.Close();
            FailIf(cook.ScriptOwnsNativeUpdate || cook.ScriptButtonSensitive ||
                cook.ScriptDrawOffset != Vector2.Zero || _player.CutsceneControlled ||
                _inventory.TradeItem != 2 || _saveData.HasRoomFlag(2, 0x3f, 0x20),
                "Cancelling an airborne cook retained actor/input state or committed the trade.");
            Vector2 before = _player.Position;
            StepGameplayUpdates(4, Vector2.Down, ["move_down"], ["move_down"], batched);
            FailIf(_player.Position.Y <= before.Y, "Cook cancellation did not return control to the gameplay loop.");
        }
    }
}
