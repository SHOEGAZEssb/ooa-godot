using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom078LinkedOldLady()
    {
        NpcRecord record = new NpcDatabase().GetRoomNpcs(0, 0x78).Single();
        var codec = new LinkedGameNpcDatabase();
        LinkedGameNpcDatabaseRecord secret = codec.Get(0, 0x78, InteractionId.OldLady, 0x04);
        FailIf(record is not { Id: 0x3d, SubId: 0x04, Y: 0x18, X: 0x28,
                TextId: 0x4d00, DefaultAnimation: 2, CanFace: true,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
            secret is not { SecretIndex: 0, ShortSecretIndex: 0x20,
                BeganFlag: 0x50, HasExtraText: true,
                OfferTextId: 0x4d00, RefusalTextId: 0x4d01,
                ExplanationTextId: 0x4d02, SecretTextId: 0x4d03,
                FinalTextId: 0x4d04 } ||
            !secret.OfferMessage.Contains("useful tip", StringComparison.Ordinal) ||
            !secret.ExplanationMessage.Contains("Clock Shop", StringComparison.Ordinal) ||
            !secret.SecretMessage.Contains("\\secret1", StringComparison.Ordinal),
            "Room 0:78 $3d:$04 lost source placement, Clock Shop secret $00, or TX_4d00-TX_4d04 content.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0x08);
            _saveData.SetLinkedGame(false);
            LoadValidationRoom(0, 0x78);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(npc.Active || npc.Visible,
                "Room 0:78 $3d:$04 appeared in an unlinked file after D4.");
            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            LoadValidationRoom(0, 0x78);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(npc.Active || npc.Visible,
                "Room 0:78 $3d:$04 appeared in a linked file before D4.");

            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0x08);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x34);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd + 1, 0x12);
            _saveData.SetGlobalFlag(0x50, false);
            LoadValidationRoom(0, 0x78);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(!npc.Active || !npc.Visible || npc.Position != new Vector2(0x28, 0x18),
                "Room 0:78 $3d:$04 did not appear at source Y/X $18/$28 after linked+D4.");
            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive || !_player.CutsceneControlled ||
                !PlainWords(_dialogue.CurrentMessage).Contains("useful tip", StringComparison.Ordinal),
                "Room 0:78 old lady was unreachable or did not present TX_4d00.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x50),
                "Room 0:78 refusal finished before linked wait 20.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Have you no desire?" ||
                _saveData.HasGlobalFlag(0x50),
                "Room 0:78 refusal did not show TX_4d01 without flag $50.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);

            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.ChoiceActive ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Clock Shop", StringComparison.Ordinal) ||
                _saveData.HasGlobalFlag(0x50),
                $"Room 0:78 Yes did not open TX_4d02 explanation before generation: choice={_dialogue.ChoiceActive}, flag={_saveData.HasGlobalFlag(0x50)}, text={_dialogue.CurrentMessage}.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.ChoiceActive || _saveData.HasGlobalFlag(0x50),
                "Room 0:78 No did not repeat TX_4d02 without setting flag $50.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x50),
                "Room 0:78 generated the Clock Shop secret before wait 20 ended.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            byte[] sourceValues = [0x03, 0x35, 0x27, 0x04, 0x18];
            string sourceSecret = DialogueBox.PlainText(string.Concat(
                sourceValues.Select(value => codec.SecretSymbols[value])));
            FailIf(!_saveData.HasGlobalFlag(0x50) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x20 ||
                !_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(sourceSecret, StringComparison.Ordinal) ||
                !codec.GenerateSecretValues(0x20, _saveData).SequenceEqual(sourceValues),
                "Room 0:78 did not encode source Clock Shop secret $20, set flag $50, and show TX_4d03.");
            string repeated = _dialogue.CurrentMessage;
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(_dialogue.CurrentMessage != repeated || !_dialogue.ChoiceActive,
                "Room 0:78 No did not repeat TX_4d03.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "That's it." ||
                _dialogue.ChoiceActive,
                "Room 0:78 Yes did not show TX_4d04.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x50),
                "Room 0:78 final text did not release Link and retain flag $50.");
            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.ChoiceActive ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Clock Shop", StringComparison.Ordinal),
                "Room 0:78 second A press did not revisit the source explanation.");
            LoadValidationRoom(0, 0x79);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x50),
                "Room 0:78 cancellation did not release input or retain flag $50.");
        }
        GD.Print("Validated room 0:78 $3d:$04 linked+D4 admission, reachable choices, TX_4d00-TX_4d04 waits, Clock Shop secret $20, repeat and cancellation in split/batched updates.");
    }
}
