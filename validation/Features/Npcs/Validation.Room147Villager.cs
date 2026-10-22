using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom147Villager()
    {
        // villager.s:@initSubid07 runs checkNpcShouldExistAtGameStage once.
        // miscMan2.s:@data4 admits states $03-$07; the indexed script at $03
        // branches on wIsLinkedGame to TX_1403 or TX_1408.
        int[] texts = [0x1400, 0x1401, 0x1402, 0x1403, 0x1404, 0x1405, 0x1406, 0x1407];
        string[] sourceWords = ["", "", "", "Ambi's Palace", "flowing tears", "hope", "Princess Zelda", "Welcome"];
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        foreach (int stage in Enumerable.Range(0, 8))
        {
            if (stage == 6 && !linked)
                continue;
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained,
                (byte)(stage == 0 ? 0 : stage == 1 ? 0x02 : stage is 2 or 3 ? 0x08 : 0x40));
            _saveData.CommitInventoryChange();
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru, stage == 3);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, stage == 5);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, stage == 7);
            if (stage == 6)
                _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
            LoadValidationRoom(1, 0x47);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var villager = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.MaleVillager && n.Record.SubId == 7);
            bool present = stage >= 3;
            FailIf(NpcStoryState.GetGameProgress2(_saveData) != stage ||
                villager.Active != present || villager.Visible != present ||
                villager.Position != new Vector2(0x48, 0x48) ||
                villager.Record is not { Palette: 1, DefaultAnimation: 2 },
                $"Room 1:47 $3a:$07 state ${stage:x2} lost source placement, pose or @data4 gate.");
            if (!present)
            {
                FailIf(villager.CanTalkTo(_player) || _entities.BlocksLink(villager.Position),
                    $"Room 1:47 $3a:$07 was interactive before state $03 (state ${stage:x2}).");
                _saveData.SetGlobalFlag(GlobalFlag.SavedNayru);
                _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0x08);
                _saveData.CommitInventoryChange();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(villager.Active, "Room 1:47 $3a:$07 respawned without re-entry.");
                LoadValidationRoom(1, 0x47);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(!_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3a).Active,
                    "Room 1:47 $3a:$07 did not appear on state $03 re-entry.");
                continue;
            }
            int text = stage == 3 && linked ? 0x1408 : texts[stage];
            FailIf(villager.TextId != text ||
                (stage == 3 && linked
                    ? !villager.Message.Contains("no idea", StringComparison.Ordinal)
                    : !villager.Message.Contains(sourceWords[stage], StringComparison.Ordinal)) ||
                villager.ObjectCollisionBounds.Size != new Vector2(12, 12),
                $"Room 1:47 $3a:$07 lost source TX_{text:x4} or solid $06/$06 radii.");
            ApproachLibraryActor(villager, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    PlainWords(_dialogue.CurrentMessage) != PlainWords(villager.Message),
                    $"Room 1:47 TX_{text:x4} was unreachable on conversation {repeat}.");
                int frame = villager.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(villager.CurrentAnimationFrame != frame,
                    "Room 1:47 $3a:$07 animated during active text.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, false);
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru, false);
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, false);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            _saveData.CommitInventoryChange();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!villager.Active || villager.TextId != text,
                "Room 1:47 $3a:$07 repeated its state-zero script selection during the visit.");
            LoadValidationRoom(1, 0x47);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3a).Active,
                "Room 1:47 $3a:$07 did not delete on state $00 re-entry.");
        }
        GD.Print("Validated room 1:47 $3a:$07 source stages $00-$07, linked TX_1408, room geometry, repeated dialogue and state-zero lifetime.");
    }
}
