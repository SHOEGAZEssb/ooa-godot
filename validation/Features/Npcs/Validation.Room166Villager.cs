using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom166Villager()
    {
        // villager.s:@initSubid08 deletes for getGameProgress_2<$04, then
        // indexes four scripts with (progress-$04) once at initialization.
        int[] texts = [0, 0, 0, 0, 0x1414, 0x1415, 0x1418, 0x1417];
        string[] sourceWords = ["", "", "", "", "hole", "Ralph", "Princess", "greetings"];
        foreach (bool batched in new[] { false, true })
        foreach (int stage in Enumerable.Range(0, 8))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(stage == 6);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained,
                (byte)(stage == 0 ? 0 : stage == 1 ? 0x02 : stage is 2 or 3 ? 0x08 : 0x40));
            _saveData.CommitInventoryChange();
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru, stage == 3);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, stage == 5);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, stage == 7);
            if (stage == 6)
                _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
            LoadValidationRoom(1, 0x66);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var npc = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.MaleVillager && n.Record.SubId == 8);
            bool present = stage >= 4;
            FailIf(NpcStoryState.GetGameProgress2(_saveData) != stage ||
                npc.Active != present || npc.Visible != present ||
                npc.Position != new Vector2(0x58, 0x68) ||
                npc.Record is not { Palette: 1, DefaultAnimation: 2 },
                $"Room 1:66 $3a:$08 state ${stage:x2} lost source placement, pose, or CP $04 deletion gate.");
            if (!present)
            {
                FailIf(npc.CanTalkTo(_player) || _entities.BlocksLink(npc.Position),
                    $"Room 1:66 $3a:$08 was interactive before state $04 (state ${stage:x2}).");
                continue;
            }
            FailIf(npc.TextId != texts[stage] ||
                !npc.Message.Contains(sourceWords[stage], StringComparison.Ordinal) ||
                npc.ObjectCollisionBounds.Size != new Vector2(12, 12),
                $"Room 1:66 $3a:$08 lost source TX_{texts[stage]:x4} or solid $06/$06 radii.");
            ApproachLibraryActor(npc, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    PlainWords(_dialogue.CurrentMessage) != PlainWords(npc.Message),
                    $"Room 1:66 TX_{texts[stage]:x4} was unreachable on conversation {repeat}.");
                int frame = npc.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(npc.CurrentAnimationFrame != frame,
                    "Room 1:66 $3a:$08 animated during active text.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, stage != 7);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!npc.Active || npc.TextId != texts[stage],
                "Room 1:66 $3a:$08 repeated its state-zero script selection during the visit.");
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, false);
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, false);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            _saveData.CommitInventoryChange();
            LoadValidationRoom(1, 0x66);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single().Active,
                "Room 1:66 $3a:$08 did not delete on state $00 re-entry.");
        }
        GD.Print("Validated room 1:66 $3a:$08 states $00-$07, source TX_1414/$1415/$1418/$1417, geometry, repeat conversations, initialization lifetime, and re-entry.");
    }
}
