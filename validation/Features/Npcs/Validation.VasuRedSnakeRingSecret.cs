using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVasuRedSnakeRingSecret()
    {
        var codec = new LinkedGameNpcDatabase();
        OracleSaveData source = OracleSaveData.CreateStandardGame();
        source.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x34);
        source.WriteWramByte(WramAddress.wUnappraisedRingsEnd + 1, 0x12);
        byte[] rings = [0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80];
        for (int index = 0; index < rings.Length; index++)
            source.WriteWramByte(WramAddress.wRingsObtained + index, rings[index]);
        byte[] secret = codec.GenerateRingSecretValues(source);
        // bank3.s: entry2 packs the eight ring bytes in +1,+5,+7,+3,+0,+4,+2,+6
        // order, then two name bits, checksum, and the GameID-selected cipher.
        FailIf(!secret.SequenceEqual(new byte[]
            { 0x1d, 0x28, 0x3f, 0x33, 0x09, 0x2d, 0x3b, 0x20, 0x00, 0x16, 0x3f, 0x3a, 0x28, 0x11, 0x3e }),
            "bank3.s ring-secret source fixture lost field order, 90-bit boundary, checksum, or cipher.");
        (string first, string second) = codec.GenerateRingSecretText(source);
        FailIf(first != string.Concat(secret.Take(5).Select(value => codec.SecretSymbols[value])) + " " +
                string.Concat(secret.Skip(5).Take(5).Select(value => codec.SecretSymbols[value])) ||
            second != string.Concat(secret.Skip(10).Select(value => codec.SecretSymbols[value])) + " ",
            "Ring-secret TX_301d text did not use source $25/$05/$25/$00 spacing and split.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x34);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd + 1, 0x12);
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(GlobalFlag.RingSecretGenerated, false);
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            LoadValidationRoom(2, 0xee);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            VasuShopEvent shop = _roomEvents.Get<VasuShopEvent>();
            NpcCharacter red = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Vasu, SubId: 0x06 });
            ApproachLibraryActor(red, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(shop.Stage != EventStage.RedLinkedMenu ||
                !shop.Database.Text(0x3018).Contains("secret", StringComparison.OrdinalIgnoreCase),
                "Red Snake $89:$06 did not enter linked TX_3018 from a collision-valid approach.");

            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            (string ownFirst, string ownSecond) = codec.GenerateRingSecretText(_saveData);
            string expectedText = DialogueBox.PlainText(shop.Database.Text(0x301d)
                .Replace("\\secret1", ownFirst, StringComparison.Ordinal)
                .Replace("\\secret2", ownSecond, StringComparison.Ordinal));
            FailIf(shop.Stage != EventStage.RedSecretListen ||
                !_saveData.HasGlobalFlag(GlobalFlag.RingSecretGenerated) ||
                _dialogue.CurrentMessage.Contains("\\secret", StringComparison.Ordinal) ||
                _dialogue.CurrentMessage != expectedText,
                $"Red Snake did not set global flag $28 and present generated TX_301d ring text: stage={shop.Stage}, flag={_saveData.HasGlobalFlag(GlobalFlag.RingSecretGenerated)}, text={_dialogue.CurrentMessage}.");
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.RedSecretListen || !_dialogue.IsOpen,
                "Red Snake did not repeat TX_301d on Yes.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.SnakeFinalText ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(shop.Database.Text(0x3010)),
                "Red Snake did not take the TX_3010 quit branch.");
            _dialogue.Close();
            StepGameplayUpdates(18, Vector2.Zero, batched: batched);
            FailIf(shop.HasState, "Red Snake did not complete its retreat after secret generation.");

            ApproachLibraryActor(red, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(shop.Stage != EventStage.RedSecretInputMenu || !_secretEntry.IsActive ||
                !_gameplayPause.IsLeased,
                "Red Snake did not acquire MENU_SECRET from the linked Input branch.");
            for (int update = 0; update < 22; update++) _secretEntry.Update(1.0 / 60.0);
            FailIf(_secretEntry.Screen is not { EnteringSecret: true } screen ||
                screen.EnteredSecret.Length != 15,
                "Ring-secret MENU_SECRET did not open with fifteen input symbols.");
            _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff]);
            FailIf(!_secretEntry.IsActive || _inventory.HasAppraisedRing(0x00),
                "Invalid ring input changed wRingsObtained or closed MENU_SECRET.");
            _secretEntry.Submit(secret);
            for (int update = 0; update < 22; update++) _secretEntry.Update(1.0 / 60.0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_secretEntry.IsActive || _gameplayPause.IsLeased ||
                shop.Stage != EventStage.SnakeFinalText ||
                !_inventory.HasAppraisedRing(0x00) ||
                _saveData.ReadWramByte(WramAddress.wRingsObtained + 7) != 0x80 ||
                !_dialogue.CurrentMessage.Contains("rings", StringComparison.OrdinalIgnoreCase),
                "Valid ring secret did not OR all eight ring bytes into live WRAM and show TX_3027.");
            _dialogue.Close();
            StepGameplayUpdates(18, Vector2.Zero, batched: batched);
            FailIf(shop.HasState, "Red Snake did not return to idle after accepted ring input.");

            ApproachLibraryActor(red, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            _dialogue.SubmitChoiceForValidation(0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            for (int update = 0; update < 22; update++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Screen!.SelectSecretLowerOption(2);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            for (int update = 0; update < 22; update++) _secretEntry.Update(1.0 / 60.0);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_secretEntry.IsActive || shop.Stage != EventStage.SnakeFinalText ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(shop.Database.Text(0x301e)) ||
                _saveData.ReadWramByte(WramAddress.wRingsObtained + 7) != 0x80,
                "Cancelling Red Snake MENU_SECRET did not show TX_301e and retain acquired rings.");
            _dialogue.Close();
            StepGameplayUpdates(18, Vector2.Zero, batched: batched);
            FailIf(shop.HasState, "Red Snake did not return to idle after cancelled ring input.");
        }
        GD.Print("Validated Red Snake $89:$06 source ring codec, TX_301d repeat, reachable MENU_SECRET, invalid/accepted/cancelled 15-symbol input, live ring merge and retreat in individual/batched updates.");
    }
}
