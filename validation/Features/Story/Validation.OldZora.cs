using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2f5OldZora()
    {
        var results = new List<string[]>();
        foreach (bool batched in new[] { false, true })
        {
            var observations = new List<string>();
            _saveData.SetRoomFlag(2, 0xf5, 0x20, false);
            _inventory.GiveTreasure(TreasureId.TradeItem, 9);
            LoadValidationRoom(2, 0xf5);
            OldZoraEvent trade = _roomEvents.Get<OldZoraEvent>();
            OldZoraCharacter zora = _entities.Entities<OldZoraCharacter>().Single();
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Text(int id, string fragment)
            {
                FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains(fragment),
                    $"Room 2:f5 expected TX_{id:x4} containing '{fragment}', got '{_dialogue.CurrentMessage}'.");
                observations!.Add($"{id:x4}:{trade.CurrentCommandIndex}:{trade.Counter}:{_inventory.TradeItem}:{_player.Position}");
            }
            void Approach()
            {
                // Start on the entrance floor, walk above the lower-right wall,
                // then approach the musician from below.
                _player.WarpTo(new Vector2(0x50, 0x68));
                StepGameplayUpdates(16, Vector2.Up, ["move_up"], ["move_up"], batched);
                StepGameplayUpdates(24, Vector2.Right, ["move_right"], ["move_right"], batched);
                StepGameplayUpdates(60, Vector2.Up, ["move_up"], ["move_up"], batched);
                Step(1);
                FailIf(!zora.CanTalkTo(_player) ||
                    _player.Position.DistanceTo(zora.Position) < 12 ||
                    _rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision != 0,
                    $"Old Zora unreachable through room 2:f5 floor: Link {_player.Position}, Zora {zora.Position}.");
            }
            void Talk(int id = 0x0b33, string fragment = "How I miss the")
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                Step(1);
                Text(id, fragment);
                FailIf(!trade.BlocksGameplay || !_player.CutsceneControlled,
                    "oldZoraScript failed to disable input before dialogue.");
            }
            void Wait30()
            {
                _dialogue.Close();
                Step(30);
                FailIf(_dialogue.IsOpen || trade.Counter != 1,
                    $"Old Zora wait 30 ended early: counter {trade.Counter}, command {trade.CurrentCommandIndex}.");
                Step(1);
            }
            void Finish()
            {
                _dialogue.Close();
                Step(3);
                FailIf(trade.BlocksGameplay || _player.CutsceneControlled || trade.CurrentCommandIndex != 1,
                    "Old Zora did not release input and return to checkabutton.");
            }

            FailIf(zora.Position != new Vector2(0x68, 0x28) ||
                zora.Record is not { Id: InteractionId.OldZora, SubId: 0, SpriteName: "spr_oldzora_cheval" } ||
                trade.Commands.Count != 23 || !trade.ButtonSensitive || trade.CurrentCommandIndex != 1 ||
                zora.CurrentAnimationOpaquePixels == 0,
                "Room 2:f5 lost Old Zora placement, graphics or state-0 script initialization.");
            FailIf(trade.Commands[3] is not CutsceneRoomFlagBranchCommand { Flag: 0x20, TargetCommand: 20 } ||
                trade.Commands[6] is not CutsceneTradeItemBranchCommand { Value: 0x0a, TargetCommand: 9 },
                "oldZoraScript source branch precedence/targets changed.");
            Approach();
            Talk();
            int frame = zora.CurrentAnimationFrame;
            Step(24);
            FailIf(zora.CurrentAnimationFrame == frame || trade.CurrentCommandIndex != 5,
                "Old Zora always-update animation or dialogue script gate changed.");
            Wait30();
            Text(0x0b34, "Back in my day,");
            Finish();
            FailIf(_inventory.TradeItem != 9 || _saveData.HasRoomFlag(2, 0xf5, 0x20),
                "Missing Sea Ukulele changed inventory/room flag.");
            // A stale value without the obtained bit must not qualify.
            _inventory.GiveTreasure(TreasureId.TradeItem, 0x0a);
            _inventory.LoseTreasure(TreasureId.TradeItem);
            Talk(); Wait30(); Text(0x0b34, "Back in my day,"); Finish();
            _inventory.GiveTreasure(TreasureId.TradeItem, 0x0a);
            Talk(); Wait30(); Text(0x0b35, "Sea Ukulele");
            FailIf(!_dialogue.ChoiceActive, "TX_0b35 lost Yes/No options.");
            _dialogue.SubmitChoiceForValidation(1);
            Step(31); Text(0x0b38, "Why do I smell"); Finish();
            FailIf(_inventory.TradeItem != 0x0a || _saveData.HasRoomFlag(2, 0xf5, 0x20),
                "Old Zora refusal committed the trade.");
            Talk(); Wait30(); Text(0x0b35, "Sea Ukulele");
            _dialogue.SubmitChoiceForValidation(0);
            Step(30);
            FailIf(_dialogue.IsOpen || trade.Counter != 1, "Old Zora accepted choice before wait 30.");
            Step(1); Text(0x0b36, "the spirit of");
            Wait30();
            for (int i = 0; i < 90 && !_dialogue.IsOpen; i++) Step(1);
            Text(0x0065, "Broken Sword");
            FailIf(_inventory.TradeItem != 0x0b || !_saveData.HasRoomFlag(2, 0xf5, 0x20),
                "Old Zora did not grant $41:$0b and persist room flag $20.");
            Wait30(); Text(0x0b37, "...What's that?"); Finish();
            Talk(0x0b39, "Ah! What fun!"); Finish();
            // Room flag takes precedence over the current trade item on re-entry.
            _inventory.GiveTreasure(TreasureId.TradeItem, 0x0a);
            LoadValidationRoom(2, 0xf5);
            zora = _entities.Entities<OldZoraCharacter>().Single();
            Approach(); Talk(0x0b39, "Ah! What fun!"); Finish();
            _saveData.SetRoomFlag(2, 0xf5, 0x20, false);
            Talk();
            _dialogue.Close();
            trade.Cancel(); trade.Cancel();
            FailIf(trade.HasState || _player.CutsceneControlled || _inventory.TradeItem != 0x0a ||
                _saveData.HasRoomFlag(2, 0xf5, 0x20), "Old Zora cancellation retained input or committed a reward.");
            results.Add(observations.ToArray());
        }
        FailIf(!results[0].SequenceEqual(results[1]),
            "Old Zora trade differs between single updates and batched host frames.");
    }
}
