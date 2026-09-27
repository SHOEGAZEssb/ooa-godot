using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2e7Mamamu()
    {
        var traces = new List<string[]>();
        foreach (bool batched in new[] { false, true })
        {
            var trace = new List<string>();
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            _saveData.SetGlobalFlag(GlobalFlag.ReturnedDog, false);
            _saveData.SetGlobalFlag(0x6a, false);
            foreach (byte flag in new byte[] { 0x20, 0x40, 0x80 }) _saveData.SetRoomFlag(2, 0xe7, flag, false);
            _inventory.GiveTreasure(TreasureId.TradeItem, 3);
            LoadValidationRoom(2, 0xe7);
            MamamuEvent trade = _roomEvents.Get<MamamuEvent>();
            NpcCharacter mamamu = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x53);
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Text(int id, string fragment)
            {
                FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains(fragment),
                    $"Room 2:e7 expected TX_{id:x4} '{fragment}', got '{_dialogue.CurrentMessage}'.");
                trace.Add($"{id:x4}:{trade.CurrentCommandIndex}:{trade.Counter}:{_inventory.TradeItem}");
            }
            void Approach(bool postgame = false)
            {
                // Entrance floor -> upper aisle -> Mamamu's right side.
                _player.WarpTo(new Vector2(0x50, 0x68));
                if (postgame) StepGameplayUpdates(8, Vector2.Left, ["move_left"], ["move_left"], batched);
                StepGameplayUpdates(76, Vector2.Up, ["move_up"], ["move_up"], batched);
                if (!postgame) StepGameplayUpdates(56, Vector2.Left, ["move_left"], ["move_left"], batched);
                Step(2);
                FailIf(!mamamu.CanTalkTo(_player) || _player.Position.DistanceTo(mamamu.Position) < 12 ||
                    _rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision != 0,
                    $"Room 2:e7 Mamamu unreachable: Link {_player.Position}, NPC {mamamu.Position}.");
            }
            void Talk(int id = 0x0b16, string fragment = "the top breeder")
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                Step(2); Text(id, fragment);
            }
            void Wait30()
            {
                _dialogue.Close(); Step(30);
                FailIf(_dialogue.IsOpen || trade.Counter != 1,
                    $"Mamamu wait 30 crossed early: command {trade.CurrentCommandIndex}, counter {trade.Counter}.");
                Step(1);
            }
            void Finish()
            {
                _dialogue.Close(); Step(5);
                FailIf(trade.BlocksGameplay || _player.CutsceneControlled, "Mamamu failed to release input.");
            }
            Step(2);
            FailIf(trade.Commands.Count != 78 || mamamu.Position != new Vector2(0x18, 0x1a) ||
                !mamamu.Record.CanFace || !trade.ButtonSensitive || mamamu.CurrentAnimationOpaquePixels == 0 ||
                trade.Commands[9] is not CutsceneTradeItemBranchCommand { Value: 4, TargetCommand: 13 } ||
                trade.Commands[21] is not CutsceneGiveItemCommand { TreasureId: 0x41, Parameter: 5 },
                "Room 2:e7 source placement, script branches or Mamamu graphics diverged.");
            Approach(); Talk();
            int frame = mamamu.CurrentAnimationFrame;
            var dog = _entities.EntityAdapters<MamamuDogRoomEntity>().Single();
            int dogCounter = dog.Script.MovementCounter;
            Step(24);
            FailIf(frame != mamamu.CurrentAnimationFrame || dogCounter != dog.Script.MovementCounter,
                "Mamamu/dog updated during text without the source always-update bit.");
            Wait30(); Text(0x0b17, "hide its face"); Finish();
            _inventory.GiveTreasure(TreasureId.TradeItem, 4);
            _inventory.LoseTreasure(TreasureId.TradeItem);
            Talk(); Wait30(); Text(0x0b17, "hide its face"); Finish();
            _inventory.GiveTreasure(TreasureId.TradeItem, 4);
            Talk(); Wait30(); Text(0x0b18, "Give her the");
            _dialogue.SubmitChoiceForValidation(1); Step(31); Text(0x0b1b, "Bad boy"); Finish();
            FailIf(_inventory.TradeItem != 4 || _saveData.HasRoomFlag(2, 0xe7, 0x20), "Mamamu refusal consumed Doggie Mask.");
            Talk(); Wait30(); _dialogue.SubmitChoiceForValidation(0); Step(31); Text(0x0b19, "Good boy");
            Wait30();
            for (int i=0; i<90 && !_dialogue.IsOpen; i++) Step(1);
            Text(0x005f, "Dumbbell");
            FailIf(_inventory.TradeItem != 5 || !_saveData.HasRoomFlag(2, 0xe7, 0x20), "Mamamu did not grant Dumbbell $41:$05 and room flag $20.");
            Wait30(); Text(0x0b1a, "What's wrong"); Finish();
            Talk(0x0b1c, "as cute"); Finish();
            LoadValidationRoom(2, 0xe7); Step(2); mamamu = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x53);
            Approach(); Talk(0x0b1c, "as cute"); Finish();

            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            LoadValidationRoom(2, 0xe7); Step(3); mamamu = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x53);
            FailIf(mamamu.Position != new Vector2(0x48, 0x28) ||
                _entities.Entities<NpcCharacter>().Any(n => n.Record.Id == 0x54 && n.Active),
                "Mamamu postgame position / indoor dog deletion changed.");
            Approach(true); Talk(0x0b3a, "What a mess");
            _dialogue.SubmitChoiceForValidation(1); Step(31); Text(0x0b3b, "Don't call me"); Finish();
            Talk(0x0b3a, "What a mess"); _dialogue.SubmitChoiceForValidation(0); Step(31);
            FailIf(!_secretEntry.IsActive || !_gameplayPause.IsLeased, "Mamamu failed to open MENU_SECRET $06.");
            for (int i=0; i<22; i++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Screen!.SelectSecretLowerOption(2);
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot(pressed: ["attack"], justPressed: ["attack"], movement: Vector2.Zero));
            try { _secretEntry.Update(1.0 / 60.0); } finally { Input.EndOriginalUpdate(); }
            for (int i=0; i<22; i++) _secretEntry.Update(1.0 / 60.0);
            Step(31); Text(0x0b3d, "Silly boy"); Finish();
            Talk(0x0b3a, "What a mess"); _dialogue.SubmitChoiceForValidation(0); Step(31);
            for (int i=0; i<22; i++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff]);
            FailIf(!_secretEntry.IsActive || _saveData.HasGlobalFlag(0x6a), "Invalid Mamamu secret advanced the quest.");
            _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(6, _saveData));
            for (int i=0; i<22; i++) _secretEntry.Update(1.0 / 60.0);
            Step(31); Text(0x0b3c, "My dear");
            FailIf(!_saveData.HasGlobalFlag(0x6a), "Valid Mamamu secret did not set GLOBALFLAG_BEGAN_MAMAMU_SECRET $6a.");
            _dialogue.SubmitChoiceForValidation(1); Step(32); Text(0x0b3e, "too bad"); Finish();
            Talk(0x0b43, "my request"); _dialogue.SubmitChoiceForValidation(0); Step(31); Text(0x0b3f, "gotten lost");
            _entities.RuntimeState.SetWramByte(OracleRuntimeState.MamamuDogLocationAddress, 0);
            Finish();
            FailIf(!_saveData.HasRoomFlag(2, 0xe7, 0x80) || _entities.RuntimeState.ReadWramByte(OracleRuntimeState.MamamuDogLocationAddress) == 0,
                "Mamamu search did not set room $80 and choose a different dog location.");
            Talk(0x0b40, "find my"); Finish();
            int rings = _inventory.UnappraisedRingCount;
            _saveData.SetGlobalFlag(GlobalFlag.ReturnedDog);
            LoadValidationRoom(2, 0xe7); Step(3); Text(0x0b41, "precious pup");
            FailIf(!_entities.Entities<NpcCharacter>().Any(n => n.Record.Id == 0x54 && n.Active), "Returned dog missing indoors.");
            Wait30();
            for (int i=0; i<90 && !_dialogue.IsOpen; i++) Step(1);
            FailIf(!_saveData.HasRoomFlag(2, 0xe7, 0x40) || _inventory.UnappraisedRingCount != rings + 1 ||
                _inventory.UnappraisedRingAt(rings) != 0x61, "Mamamu reward did not grant SNOWSHOE_RING $21 with bit 6 set and room $40.");
            _dialogue.Close();
            for (int i=0; i<90 && !_dialogue.IsOpen; i++) Step(1);
            Text(0x0b42, "appraisal"); Finish();
            LoadValidationRoom(2, 0xe7); Step(5); mamamu = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x53);
            Approach(); Talk(0x0b44, "appraisal"); Finish();
            FailIf(_inventory.UnappraisedRingCount != rings + 1, "Mamamu repeated her one-time ring reward.");
            trade.Cancel(); trade.Cancel();
            FailIf(trade.HasState || _player.CutsceneControlled, "Mamamu cancellation retained its script/input lease.");
            // Cancel an unfinished trade before its inventory write.
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            _saveData.SetRoomFlag(2, 0xe7, 0x20, false);
            _inventory.GiveTreasure(TreasureId.TradeItem, 4);
            LoadValidationRoom(2, 0xe7); Step(2); mamamu = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x53);
            Approach(); Talk(); _dialogue.Close(); trade.Cancel(); Step(40);
            FailIf(trade.HasState || _player.CutsceneControlled || _inventory.TradeItem != 4 || _saveData.HasRoomFlag(2, 0xe7, 0x20),
                "Cancelling Mamamu before the reward changed inventory/flags or left input locked.");
            traces.Add(trace.ToArray());
        }
        FailIf(!traces[0].SequenceEqual(traces[1]), "Mamamu differs between individual updates and batched host frames.");
    }

    private void ValidateRoom2e7MamamuDog()
    {
        _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
        _saveData.SetRoomFlag(2, 0xe7, 0x20, false);
        LoadValidationRoom(2, 0xe7);
        var dog = _entities.EntityAdapters<MamamuDogRoomEntity>().Single();
        var actor = (NpcCharacter)dog.Node;
        void Step(int n) => StepGameplayUpdates(n, Vector2.Zero);
        int randomCalls = _entities.RandomCalls;
        Step(1);
        int remaining = dog.Script.MovementCounter;
        FailIf(_entities.RandomCalls != randomCalls + 1 || !new[] { 0x77, 0xb3, 0xef, 0xfe }.Contains(remaining) || actor.Position != new Vector2(0x50, 0x38) || dog.Script.ZFixed != 0,
            "Indoor dog state 0 did not decrement its source random counter before first movement.");
        int expectedX = 0x50, direction = -1, z = 0, speedZ = -0xc0;
        for (int i=0; i<remaining; i++)
        {
            expectedX += direction;
            if (((expectedX - 0x18) & 0xff) >= 0x70) direction = -direction;
            // objectUpdateSpeedZ adds speed before gravity, clamps landing.
            z += speedZ; speedZ += 0x20;
            if (z >= 0) { z = 0; speedZ = -0xc0; }
            Step(1);
            if (i == remaining - 1) z = 0;
            FailIf(actor.Position != new Vector2(expectedX, 0x38) || dog.Script.ZFixed != z,
                $"Indoor dog update {i:x2}: expected ({expectedX:x2},$38) z={z}, got {actor.Position} z={dog.Script.ZFixed}.");
        }
        FailIf(dog.Script.MovementCounter != 0 || dog.Script.RestCounter != 180 || dog.Script.Animation >= 2,
            "Indoor dog zero counter did not stop, clear Z and enter wait 180.");
        Vector2 rest = actor.Position;
        Step(179);
        FailIf(actor.Position != rest || dog.Script.RestCounter != 1, "Indoor dog rest ended before update 180.");
        randomCalls = _entities.RandomCalls;
        Step(1);
        FailIf(_entities.RandomCalls != randomCalls + 1 || actor.Position != rest || dog.Script.Animation < 2 || dog.Script.RestCounter != 0,
            "Indoor dog wait-180 completion moved before its first counter branch.");
        Step(1); FailIf(actor.Position == rest, "Indoor dog failed to resume after rest.");
        _saveData.SetGlobalFlag(GlobalFlag.FinishedGame); _saveData.SetRoomFlag(2, 0xe7, 0x20);
        FailIf(!actor.Active, "Indoor dog repeated its state-0 deletion gate on a live save change.");
        _roomEvents.Get<MamamuEvent>().Cancel();
    }
}
