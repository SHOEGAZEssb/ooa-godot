using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom12dSmithSubrosian()
    {
        NpcRecord record = new NpcDatabase().GetRoomNpcs(1, 0x2d).Single();
        var codec = new LinkedGameNpcDatabase();
        LinkedGameNpcDatabaseRecord secret = codec.Get(1, 0x2d, InteractionId.Subrosian, 4);
        FailIf(record is not { Id: 0x4e, SubId: 4, Y: 0x38, X: 0x88,
                TextId: 0x4d14, DefaultAnimation: 2, CanFace: true,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
            secret is not { SecretIndex: 4, ShortSecretIndex: 0x24,
                BeganFlag: 0x54, HasExtraText: false,
                OfferTextId: 0x4d14, RefusalTextId: 0x4d15,
                SecretTextId: 0x4d17, FinalTextId: 0x4d18 } ||
            !PlainWords(secret.OfferMessage).Contains("The best defense is a good offense", StringComparison.Ordinal) ||
            secret.SecretMessage != "Tell this \\col(1)secret\\col(0)\nto the master at\nthe Subrosian\nSmithy:\\stop\n\\secret1\\stop\nUnderstand?\n  \\opt()Yes \\opt()No" ||
            PlainWords(secret.FinalMessage) != "Give the master my regards!",
            "1:2d $4e:$04 lost source placement, TX_4d14-TX_4d18 controls, or Smith secret $04 metadata.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            LoadValidationRoom(1, 0x2d);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(npc.Active || npc.Visible,
                "1:2d $4e:$04 appeared in an unlinked file.");

            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x34);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd + 1, 0x12);
            _saveData.WriteWramByte(WramAddress.wShortSecretIndex, 0);
            _saveData.SetGlobalFlag(0x54, false);
            LoadValidationRoom(1, 0x2d);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(!npc.Active || !npc.Visible || npc.Position != new Vector2(0x88, 0x38) ||
                npc.CurrentAnimationOpaquePixels == 0 || !_entities.BlocksLink(npc.Position),
                "1:2d linked $4e:$04 did not publish its solid source pose without an essence gate.");
            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive || !_player.CutsceneControlled ||
                !PlainWords(_dialogue.CurrentMessage).Contains("The best defense is a good offense", StringComparison.Ordinal),
                "1:2d $4e:$04 was unreachable or did not present TX_4d14.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x54),
                "1:2d refusal finished before linked wait 20.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "You never know what life may bring..." ||
                _saveData.HasGlobalFlag(0x54),
                "1:2d refusal did not show TX_4d15 without flag $54.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);

            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x54) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0,
                "1:2d Smith secret generated before the source 20-update choice wait.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            // bank3.s packing, checksum and XOR, independently calculated for
            // GameID $1234 and short secret index $24: $03,$35,$27,$06,$1a.
            byte[] sourceValues = [0x03, 0x35, 0x27, 0x06, 0x1a];
            string sourceSecret = DialogueBox.PlainText(string.Concat(
                sourceValues.Select(value => codec.SecretSymbols[value])));
            FailIf(!_saveData.HasGlobalFlag(0x54) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x24 ||
                !_dialogue.ChoiceActive ||
                !_dialogue.CurrentMessage.Contains(sourceSecret, StringComparison.Ordinal) ||
                !codec.GenerateSecretValues(0x24, _saveData).SequenceEqual(sourceValues),
                "1:2d did not encode source Smith secret $24, set flag $54, and show TX_4d17.");
            string repeated = _dialogue.CurrentMessage;
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(_dialogue.CurrentMessage != repeated || !_dialogue.ChoiceActive,
                "1:2d No did not repeat TX_4d17.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(21, Vector2.Zero, batched: batched);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Give the master my regards!" ||
                _dialogue.ChoiceActive,
                "1:2d Yes did not show TX_4d18.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x54),
                "1:2d final text did not release Link or retain flag $54.");

            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.ChoiceActive ||
                !PlainWords(_dialogue.CurrentMessage).Contains("The best defense", StringComparison.Ordinal),
                "1:2d second A press did not return to TX_4d14's offer loop.");
            LoadValidationRoom(1, 0x2e);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x54),
                "1:2d cancellation did not release input or retain flag $54.");
        }
        GD.Print("Validated room 1:2d $4e:$04 linked admission, solid collision approach, TX_4d14-TX_4d18 waits, Smith secret $24, repeat and cancellation in split/batched updates.");
    }
}
