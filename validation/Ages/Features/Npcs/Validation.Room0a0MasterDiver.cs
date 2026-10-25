using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoom0a0MasterDiver()
    {
        NpcRecord record = new NpcDatabase().GetRoomNpcs(0, 0xa0).Single();
        var codec = new LinkedGameNpcDatabase();
        LinkedGameNpcDatabaseRecord secret = codec.Get(0, 0xa0, AgesInteractionId.MasterDiver, 0);
        FailIf(record is not { Id: 0xcd, SubId: 0, Y: 0x38, X: 0x28,
                TextId: 0x4d0f, DefaultAnimation: 4, CanFace: false,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
            secret is not { SecretIndex: 3, ShortSecretIndex: 0x23,
                BeganFlag: 0x53, HasExtraText: false,
                OfferTextId: 0x4d0f, RefusalTextId: 0x4d10,
                SecretTextId: 0x4d12, FinalTextId: 0x4d13 } ||
            !secret.OfferMessage.Contains("master diver", StringComparison.Ordinal) ||
            !secret.SecretMessage.Contains("\\secret1", StringComparison.Ordinal),
            "Room 0:a0 $cd:$00 lost source pose, Diver Secret $03, or TX_4d0f-TX_4d13 data.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            LoadValidationRoom(0, 0xa0);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(npc.Active || npc.Visible,
                "Room 0:a0 $cd:$00 appeared in an unlinked file.");

            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, 0);
            _saveData.WriteWramByte(AgesWramAddress.wUnappraisedRingsEnd, 0x34);
            _saveData.WriteWramByte(AgesWramAddress.wUnappraisedRingsEnd + 1, 0x12);
            _saveData.SetGlobalFlag(0x53, false);
            LoadValidationRoom(0, 0xa0);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(!npc.Active || !npc.Visible || npc.Position != new Vector2(0x28, 0x38) ||
                npc.CurrentAnimationOpaquePixels == 0 || !_entities.BlocksLink(npc.Position),
                "Room 0:a0 linked $cd:$00 did not publish its solid source pose without an essence gate.");
            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive || !_player.CutsceneControlled ||
                !PlainWords(_dialogue.CurrentMessage).Contains("master diver", StringComparison.Ordinal),
                "Room 0:a0 $cd:$00 was unreachable or did not present TX_4d0f.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x53),
                "Room 0:a0 refusal finished before linked wait 20.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "No? Oh, well..." ||
                _saveData.HasGlobalFlag(0x53),
                "Room 0:a0 refusal did not show TX_4d10 without flag $53.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);

            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x53),
                "Room 0:a0 Diver secret generated before the source 20-update choice wait.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            byte[] sourceValues = [0x0b, 0x29, 0x13, 0x1c, 0x23];
            string sourceSecret = DialogueBox.PlainText(string.Concat(
                sourceValues.Select(value => codec.SecretSymbols[value])));
            FailIf(!_saveData.HasGlobalFlag(0x53) ||
                _saveData.ReadWramByte(AgesWramAddress.wShortSecretIndex) != 0x23 ||
                !_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(sourceSecret, StringComparison.Ordinal) ||
                !codec.GenerateSecretValues(0x23, _saveData).SequenceEqual(sourceValues),
                "Room 0:a0 did not encode source Diver secret $23, set flag $53, and show TX_4d12.");
            string repeated = _dialogue.CurrentMessage;
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(_dialogue.CurrentMessage != repeated || !_dialogue.ChoiceActive,
                "Room 0:a0 No did not repeat TX_4d12.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "I'm counting on you!" ||
                _dialogue.ChoiceActive,
                "Room 0:a0 Yes did not show TX_4d13.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x53),
                "Room 0:a0 final text did not release Link or retain flag $53.");

            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive ||
                !PlainWords(_dialogue.CurrentMessage).Contains("master diver", StringComparison.Ordinal),
                "Room 0:a0 second A press did not return to TX_4d0f's offer loop.");
            LoadValidationRoom(0, 0xa1);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x53),
                "Room 0:a0 cancellation did not release input or retain flag $53.");
        }
        GD.Print("Validated room 0:a0 $cd:$00 linked admission, solid collision approach, TX_4d0f-TX_4d13 waits, Diver secret $23, repeat and cancellation in split/batched updates.");
    }
}
