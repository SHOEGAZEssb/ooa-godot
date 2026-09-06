using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokaySecret()
    {
        var codec = new LinkedGameNpcDatabase();
        var save = OracleSaveData.CreateStandardGame();
        save.WriteWramByte(0xc600, 0x34);
        save.WriteWramByte(0xc601, 0x12);
        FailIf(!codec.ValidateSecret([0x0b, 0x29, 0x13, 0x18, 0x2f], 0x21, save),
            "Secret decoder rejected the existing source-packing Graveyard fixture.");
        for (int index = 0; index < 64; index++)
        {
            byte[] code = codec.GenerateSecretValues(index, save);
            FailIf(!codec.ValidateSecret(code, index, save) || codec.ValidateSecret(code, index ^ 1, save),
                $"Secret decoder lost short index ${index:x2}.");
            code[4] ^= 1;
            FailIf(codec.ValidateSecret(code, index, save), "Secret decoder accepted a corrupt checksum.");
        }
        _saveData.WriteWramByte(0xc600, 0x34);
        _saveData.WriteWramByte(0xc601, 0x12);
        var database = new WildTokayGameDatabase();
        var texts = new TokayInteractionDatabase();
        _saveData.SetGlobalFlag(database.FinishedGameFlag);
        _saveData.SetGlobalFlag(database.BeganSecretFlag, value: false);
        _saveData.SetGlobalFlag(database.DoneSecretFlag, value: false);
        LoadValidationRoom(2, 0xe5);
        NpcCharacter manager = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.SubId == 0x19);
        WildTokayGameEvent game = _roomEvents.WildTokayGame;
        FailIf(!game.TryInteractNpc(manager), "Present Tokay manager did not accept A.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(20);
        FailIf(_secretEntry.IsActive, "Tokay secret menu skipped its 20-update prompt wait.");
        StepRoomEventFrames(1);
        FailIf(!_secretEntry.IsActive || !_gameplayPause.IsLeased,
            "Tokay askforsecret did not acquire the shared menu lifecycle.");
        for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
        FailIf(_secretEntry.Screen is not { EnteringSecret: true }, "Secret screen was not installed at white.");
        var keyboard = new SecretEntryDatabase();
        FailIf(keyboard.KeyboardGlyph(2, 6) != ' ' || keyboard.KeyboardGlyph(4, 12) != '@',
            "Secret keyboard lost its source blank cell or final symbol.");
        _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff]);
        FailIf(!_secretEntry.IsActive || _saveData.HasGlobalFlag(database.BeganSecretFlag),
            "Invalid Tokay secret advanced the quest.");
        _secretEntry.Submit(codec.GenerateSecretValues(0x05, _saveData));
        for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
        FailIf(_secretEntry.IsActive || _gameplayPause.IsLeased || game.Counter != 20,
            "Secret close did not restore ownership and start the post-menu wait.");
        StepRoomEventFrames(19);
        FailIf(_saveData.HasGlobalFlag(database.BeganSecretFlag), "Tokay secret flag was set early.");
        StepRoomEventFrames(1);
        FailIf(!_saveData.HasGlobalFlag(database.BeganSecretFlag) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(texts.Text(0x0a47)),
            "Valid Tokay secret did not enter TX_0a47.");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(23);
        FailIf(_dialogue.CurrentMessage != DialogueBox.PlainText(texts.Text(0x0a52)),
            "Present play refusal lost its 2+20-update waits.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        _saveData.SetGlobalFlag(database.DoneSecretFlag);
        FailIf(!game.TryInteractNpc(manager) || _dialogue.CurrentMessage.Contains("\\secret1", StringComparison.Ordinal) ||
            _saveData.ReadWramByte(0xc6fb) != 0x15,
            "Completed Tokay quest did not generate its $15 return secret.");
        game.Cancel();
        _dialogue.Close();
        GD.Print("Validated short-secret checksum/type/game-ID/index decoding, original secret-entry assets and modal lifecycle, invalid/valid Tokay input, prompt waits, and return-secret generation.");
    }
}
