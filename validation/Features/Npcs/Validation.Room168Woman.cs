using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom168Woman()
    {
        // femaleVillager.s:@initSubid03 selects the eight-entry script table
        // once; miscMan2.s:@data1 excludes only getGameProgress_2 state $01.
        int[] texts = [0x1500, 0x1501, 0x1502, 0x1503, 0x1504, 0x1505, 0x1508, 0x1507];
        string[] sourceWords = ["village elder", "", "prison", "nervous", "Strange things", "can't move", "peaceful days", "nice again"];
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
            LoadValidationRoom(1, 0x68);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var woman = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.FemaleVillager && n.Record.SubId == 3);
            bool present = stage != 1;
            FailIf(NpcStoryState.GetGameProgress2(_saveData) != stage ||
                woman.Active != present || woman.Visible != present ||
                woman.Position != new Vector2(0x68, 0x68) ||
                woman.Record is not { Palette: 2, DefaultAnimation: 2 },
                $"Room 1:68 $3b:$03 state ${stage:x2} lost source placement, pose or @data1 gate.");
            if (!present)
            {
                FailIf(woman.CanTalkTo(_player) || _entities.BlocksLink(woman.Position),
                    "Room 1:68 $3b:$03 was interactive in excluded state $01.");
                _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0x08);
                _saveData.CommitInventoryChange();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(woman.Active, "Room 1:68 $3b:$03 respawned without re-entry.");
                LoadValidationRoom(1, 0x68);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(!_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3b).Active,
                    "Room 1:68 $3b:$03 did not appear on state $02 re-entry.");
                continue;
            }
            int text = texts[stage];
            FailIf(woman.TextId != text ||
                !woman.Message.Contains(sourceWords[stage], StringComparison.Ordinal) ||
                woman.ObjectCollisionBounds.Size != new Vector2(12, 12),
                $"Room 1:68 $3b:$03 lost source TX_{text:x4} or solid $06/$06 radii.");
            ApproachLibraryActor(woman, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    PlainWords(_dialogue.CurrentMessage) != PlainWords(woman.Message),
                    $"Room 1:68 TX_{text:x4} was unreachable on conversation {repeat}.");
                int frame = woman.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(woman.CurrentAnimationFrame != frame,
                    "Room 1:68 $3b:$03 animated during active text.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, false);
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru, false);
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, false);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0x02);
            _saveData.CommitInventoryChange();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!woman.Active || woman.TextId != text,
                "Room 1:68 $3b:$03 repeated its state-zero selection during the visit.");
            LoadValidationRoom(1, 0x68);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3b).Active,
                "Room 1:68 $3b:$03 did not delete on state $01 re-entry.");
        }
        GD.Print("Validated room 1:68 $3b:$03 across getGameProgress_2 states $00-$07, source TX_1500-$1508, geometry, repeats and state-zero lifetime.");
    }
}
