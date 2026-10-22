using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom168LinkedSubrosian()
    {
        // subrosian.s:subrosian_subid00 selects its script only in state zero.
        // getGameProgress_2 $06 must delete even if Twinrova's flag remains set.
        NpcRecord record = new NpcDatabase().GetRoomNpcs(1, 0x68).Single(n =>
            n.Id == InteractionId.Subrosian && n.SubId == 0);
        FailIf(record is not { Var03: 0, Y: 0x48, X: 0x38,
                TextId: 0x1c05, CanFace: true, DefaultAnimation: 2,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
            PlainWords(record.Message) != "What's with this village? It has no energy!",
            "Room 1:68 $4e:$00 lost source pose, ordinary adapter or TX_1c05.");

        foreach (bool batched in new[] { false, true })
        foreach ((bool linked, int stage, bool present, int text, string words) in new[]
        {
            (false, 5, false, 0x1c05, ""),
            (true, 0, false, 0x1c05, ""),
            (true, 5, true, 0x1c05, "What's with this village? It has no energy!"),
            (true, 6, false, 0x1c05, ""),
            (true, 7, true, 0x1c07, "Everyone's energy has returned!")
        })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, stage >= 5);
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, stage == 6);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, stage == 7);
            LoadValidationRoom(1, 0x68);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.Subrosian && n.Record.SubId == 0);
            FailIf(NpcStoryState.GetGameProgress2(_saveData) != stage ||
                npc.Active != present || npc.Visible != present ||
                npc.Position != new Vector2(0x38, 0x48),
                $"Room 1:68 $4e:$00 wrong linked/getGameProgress_2 ${stage:x2} admission or source pose.");
            if (!present)
            {
                FailIf(npc.CanTalkTo(_player) || _entities.BlocksLink(npc.Position),
                    $"Room 1:68 $4e:$00 remained interactive in excluded state ${stage:x2}.");
                continue;
            }

            FailIf(npc.TextId != text || PlainWords(npc.Message) != words ||
                npc.CurrentAnimationOpaquePixels == 0 || !_entities.BlocksLink(npc.Position),
                $"Room 1:68 $4e:$00 lost TX_{text:x4}, sprite, or NPC collision.");
            ApproachLibraryActor(npc, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != words,
                    $"Room 1:68 TX_{text:x4} was unreachable on talk {repeat}: actual='{PlainWords(_dialogue.CurrentMessage)}', player={_player.Position}.");
                int frame = npc.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(npc.CurrentAnimationFrame != frame,
                    "Room 1:68 $4e:$00 animated during active text.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }

            // The script pointer and state-zero admission are fixed for this
            // visit; a save change takes effect only when the room is reloaded.
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, stage == 5);
            _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, stage == 7);
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, stage == 5);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!npc.Active || npc.TextId != text,
                "Room 1:68 $4e:$00 reran its state-zero selection during the visit.");
            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != words,
                "Room 1:68 $4e:$00 changed the selected script during repeated talk.");
            LoadValidationRoom(1, 0x69);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled,
                "Room 1:68 $4e:$00 room exit did not release dialogue input.");
            LoadValidationRoom(1, 0x68);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            npc = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == InteractionId.Subrosian && n.Record.SubId == 0);
            int reentryText = stage == 5 ? 0x1c07 : 0x1c05;
            FailIf(!npc.Active || !npc.Visible || npc.TextId != reentryText,
                "Room 1:68 $4e:$00 failed to select the changed progress state on re-entry.");
        }
        GD.Print("Validated room 1:68 $4e:$00 linked state-zero admission, progress $05/$06/$07, TX_1c05/TX_1c07, collision approach, repeat, cancellation, re-entry and split/batched updates.");
    }
}
