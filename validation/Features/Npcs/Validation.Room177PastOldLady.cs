using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom177PastOldLady()
    {
        // pastOldLady.s:@subid1 indexes exactly four scripts with
        // getGameProgress_2=$00-$03, then deletes on CP $04 / RET NC.
        int[] essences = [0, 0x02, 0x08, 0x08, 0x40, 0x40, 0x40, 0x40];
        string[] sourceWords = [
            "night for many", "Maku Tree", "good morning!", "taken to work on"
        ];
        foreach (bool batched in new[] { false, true })
        foreach (int stage in Enumerable.Range(0, 8))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(stage == 6);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, (byte)essences[stage]);
            _saveData.CommitInventoryChange();
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru, stage == 3);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, stage == 5);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, stage == 7);
            if (stage == 6)
                _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
            LoadValidationRoom(1, 0x77);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var npc = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.PastOldLady && n.Record.SubId == 1);
            bool present = stage < 4;
            FailIf(NpcStoryState.GetGameProgress2(_saveData) != stage ||
                npc.Active != present || npc.Visible != present ||
                npc.Position != new Vector2(0x58, 0x48) ||
                npc.Record.Palette != 3 || npc.Record.DefaultAnimation != 4,
                $"Room 1:77 $45:$01 state ${stage:x2} lost source placement, presentation or CP $04 gate.");
            if (!present)
            {
                FailIf(npc.CanTalkTo(_player) || _entities.BlocksLink(npc.Position),
                    $"Room 1:77 $45:$01 remained interactive after deletion in state ${stage:x2}.");
                continue;
            }
            int text = 0x1800 + stage;
            FailIf(npc.TextId != text ||
                !npc.Message.Contains(sourceWords[stage], StringComparison.Ordinal) ||
                npc.ObjectCollisionBounds.Size != new Vector2(12, 12),
                $"Room 1:77 $45:$01 did not select TX_{text:x4} and solid radius $06.");
            ApproachLibraryActor(npc, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    PlainWords(_dialogue.CurrentMessage) != PlainWords(npc.Message),
                    $"Room 1:77 TX_{text:x4} was unreachable on conversation {repeat}.");
                int animationFrame = npc.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(npc.CurrentAnimationFrame != animationFrame,
                    $"Room 1:77 TX_{text:x4} did not freeze the NPC while text was active.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!npc.Active || npc.TextId != text,
                "Room 1:77 $45:$01 repeated its state-zero selection during the same visit.");
            LoadValidationRoom(1, 0x77);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x45).Active,
                "Room 1:77 $45:$01 did not delete on finished-game re-entry.");
        }
        GD.Print("Validated room 1:77 $45:$01 source script states $00-$03, deletion states $04-$07, geometry, repeated dialogue, selection lifetime and re-entry.");
    }
}
