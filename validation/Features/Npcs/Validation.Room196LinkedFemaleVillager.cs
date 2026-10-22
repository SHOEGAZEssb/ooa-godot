using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom196LinkedFemaleVillager()
    {
        NpcRecord record = new NpcDatabase().GetRoomNpcs(1, 0x96).Single();
        var codec = new LinkedGameNpcDatabase();
        LinkedGameNpcDatabaseRecord secret = codec.Get(1, 0x96, InteractionId.FemaleVillager, 0x06);
        FailIf(record is not { Id: 0x3b, SubId: 0x06, Var03: 0, Y: 0x48, X: 0x28,
                TextId: 0x4d19, DefaultAnimation: 2, CanFace: true,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
            secret is not { SecretIndex: 0x05, ShortSecretIndex: 0x25,
                BeganFlag: 0x55, HasExtraText: false,
                OfferTextId: 0x4d19, RefusalTextId: 0x4d1a,
                SecretTextId: 0x4d1c, FinalTextId: 0x4d1d } ||
            !secret.OfferMessage.Contains("Holodrum", StringComparison.Ordinal) ||
            !secret.SecretMessage.Contains("\\secret1", StringComparison.Ordinal) ||
            PlainWords(secret.FinalMessage) != "Thanks so much!",
            "Room 1:96 $3b:$06 lost source placement, linked script index $05, or TX_4d19-TX_4d1d data.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            LoadValidationRoom(1, 0x96);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(npc.Active || npc.Visible,
                "Room 1:96 $3b:$06 appeared in an unlinked file.");

            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x34);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd + 1, 0x12);
            _saveData.SetGlobalFlag(0x55, false);
            LoadValidationRoom(1, 0x96);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(!npc.Active || !npc.Visible || npc.Position != new Vector2(0x28, 0x48) ||
                npc.CurrentAnimationOpaquePixels == 0,
                "Room 1:96 linked $3b:$06 did not publish its source pose without an essence gate.");
            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Holodrum", StringComparison.Ordinal) ||
                !_player.CutsceneControlled,
                "Room 1:96 $3b:$06 was not reachable for the TX_4d19 linked offer.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x55),
                "Room 1:96 refusal advanced before the linked script's 20-update wait.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "How rude!" ||
                _saveData.HasGlobalFlag(0x55),
                "Room 1:96 refusal did not show TX_4d1a without setting flag $55.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);

            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x55) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0,
                "Room 1:96 secret generation ran before the choice's wait 20 finished.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            byte[] sourceValues = [0x0b, 0x29, 0x13, 0x1a, 0x2d];
            string sourceSecret = string.Concat(sourceValues.Select(value => codec.SecretSymbols[value]));
            FailIf(!_saveData.HasGlobalFlag(0x55) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x25 ||
                !_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(DialogueBox.PlainText(sourceSecret), StringComparison.Ordinal) ||
                !codec.GenerateSecretValues(0x25, _saveData).SequenceEqual(sourceValues),
                "Room 1:96 did not set flag $55, encode source short secret $25, and present TX_4d1c.");
            string repeated = _dialogue.CurrentMessage;
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(_dialogue.CurrentMessage != repeated || !_dialogue.ChoiceActive,
                "Room 1:96 No did not repeat the same TX_4d1c secret.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Thanks so much!" ||
                _dialogue.ChoiceActive,
                "Room 1:96 Yes did not reach TX_4d1d.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x55),
                "Room 1:96 linked script did not release Link and retain flag $55.");

            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Holodrum", StringComparison.Ordinal),
                "Room 1:96 second A press did not return to TX_4d19's offer loop.");
            LoadValidationRoom(1, 0x97);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x55),
                "Room 1:96 cancellation did not release input and retain generated flag $55.");
        }
        GD.Print("Validated room 1:96 $3b:$06 linked visibility, collision approach, TX_4d19-TX_4d1d choice waits, source secret $25, repeat, cancellation and batched updates.");
    }
}
