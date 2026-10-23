using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFaroreScript()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (int index in new[] { 0x20, 0x30, 0x39 })
        {
            byte[] values = codec.GenerateSecretValues(index, _saveData);
            FailIf(!codec.TryDecodeShortSecret(values, _saveData, out int decoded) || decoded != index,
                $"Farore MENU_SECRET $ff lost decoded short index ${index:x2}.");
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Farore, SubId: 0 });
            FailIf(farore.Position != new Vector2(0x58, 0x28) ||
                farore.ObjectCollisionBounds.Size != new Vector2(12, 40),
                "Farore $10 state-one position or $14/$06 collision changed.");
            ApproachLibraryActor(farore, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.IsOpen || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("secrets", StringComparison.OrdinalIgnoreCase),
                "Unlinked pre-completion Farore did not show TX_5501 from room floor geometry.");
            _dialogue.Close();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Farore room exit retained input ownership.");

            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(GlobalFlag.SecretChestWaiting);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            FailIf(_saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting),
                "Farore $10 state zero did not clear GLOBALFLAG_SECRET_CHEST_WAITING $2c.");
            ApproachLibraryActor(farore, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.IsOpen || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("Farore", StringComparison.OrdinalIgnoreCase),
                "Linked Farore did not show TX_5506 on first A contact.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.ChoiceActive || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("secret", StringComparison.OrdinalIgnoreCase),
                "Linked Farore did not offer TX_5507 after TX_5506 closed.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("anytime", StringComparison.OrdinalIgnoreCase),
                "Linked Farore's No branch did not show TX_5508.");
            _dialogue.Close();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Linked Farore cancellation retained input control.");
        }
        GD.Print("Validated Farore $10 source position/radii, secret decoder, " +
            "unlinked opening and linked greeting/No branch through geometry " +
            "in split/batched gameplay updates.");
    }

    private void ValidateFaroreSecretResponses()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (bool batched in new[] { false, true })
        foreach (int shortIndex in new[] { 0x20, 0x30 })
        foreach (int status in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x50);
            _saveData.SetGlobalFlag(0x5a, status == 2);
            _saveData.SetGlobalFlag(0x50, status != 0);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            _dialogue.Close(); Step();
            FailIf(!_dialogue.ChoiceActive,
                "Linked Farore did not offer TX_5507 before MENU_SECRET.");
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            FailIf(!_secretEntry.IsActive || !_gameplayPause.IsLeased,
                "Farore askforsecret $ff did not acquire the shared MENU_SECRET lifecycle.");
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff]);
            FailIf(!_secretEntry.IsActive,
                "Invalid Farore checksum closed MENU_SECRET.");
            _secretEntry.Submit(codec.GenerateSecretValues(shortIndex, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(2);
            string message = DialogueBox.PlainText(_dialogue.CurrentMessage);
            int expectedText = shortIndex == 0x20 ? 0x550b : status switch
            {
                0 => 0x551c, 2 => 0x550c, _ => -1
            };
            if (expectedText >= 0)
                FailIf(message != DialogueBox.PlainText(new FaroreScriptDatabase().Text(expectedText)),
                    $"Farore secret ${shortIndex:x2} status {status} lost TX_{expectedText:x4}: '{message}'.");
            else
                FailIf(!_entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects).Equals((byte)0x91),
                    "Accepted Farore secret released object mask before reward signal.");
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) == 0x91,
                "Farore room exit retained secret script input or object mask.");
        }
        GD.Print("Validated Farore $ff MENU_SECRET entry, invalid checksum, " +
            "wrong-game $20, inactive $30, already-done $30 and accepted $30 " +
            "through room geometry in split/batched gameplay updates.");
    }

    private void ValidateFaroreRewards()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (bool batched in new[] { false, true })
        foreach (int quest in new[] { 0, 1, 2, 3, 6, 8 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x50 + quest);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            _dialogue.Close(); Step();
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(codec.GenerateSecretValues(0x30 + quest, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(3);
            FailIf(_saveData.HasGlobalFlag(0x5a + quest),
                $"Farore marked quest ${quest:x2} done before its reward.");
            Step(quest == 0 ? 150 : 300);
            if (quest == 0)
            {
                FailIf(_inventory.SwordLevel != 2 || _saveData.HasGlobalFlag(0x5a),
                    $"Farore sword upgrade did not grant TREASURE_OBJECT_SWORD_01 parameter $02 before cleanup " +
                    $"(level={_inventory.SwordLevel}, done={_saveData.HasGlobalFlag(0x5a)}, " +
                    $"mask={_entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}, " +
                    $"text='{_dialogue.CurrentMessage}').");
                FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "Noble", StringComparison.OrdinalIgnoreCase),
                    $"Farore sword upgrade did not use TREASURE_OBJECT_SWORD_01 pickup text " +
                    $"(text='{_dialogue.CurrentMessage}').");
                _dialogue.Close();
                Step(30);
                FailIf(_saveData.HasGlobalFlag(0x5a),
                    "Farore sword marked the secret done before its silent spin-sword grant.");
                Step(90);
                FailIf(!_saveData.HasGlobalFlag(0x5a) ||
                    !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                        "power", StringComparison.OrdinalIgnoreCase),
                    "Farore sword upgrade did not mark secret $00 and show TX_5509.");
                _dialogue.Close(); Step();
                FailIf(_player.CutsceneControlled,
                    "Farore sword upgrade did not return Link input after reward signal.");
            }
            else
            {
                Step(120);
                int expectedTreasure = quest switch
                {
                    1 => TreasureId.HeartContainer,
                    2 => TreasureId.Bombchus,
                    3 or 6 => TreasureId.Ring,
                    8 => TreasureId.BiggoronSword,
                    _ => throw new InvalidOperationException("Unexpected Farore chest quest.")
                };
                int expectedSubid = quest switch { 3 => 0x0c, 6 => 0x0d, _ => 1 };
                FailIf(_currentRoom.GetMetatile(new Vector2(0x58, 0x78)) != ChestDatabase.ClosedTile ||
                    !_saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting) ||
                    _saveData.HasGlobalFlag(0x5a + quest) ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wChestContentsOverride) != expectedTreasure ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wChestContentsOverride + 1) != expectedSubid,
                    $"Farore chest quest ${quest:x2} did not publish tile $f1, " +
                    "source treasure override and waiting flag $2c before collection.");
                _dialogue.Close(); Step();
                FailIf(_player.CutsceneControlled,
                    "Farore chest signal did not return Link input before collection.");
                Vector2 south = new(0x58, 0x98);
                FailIf(_collision.Collides(south) || _entities.BlocksLink(south) ||
                    _currentRoom.IsSolid(south),
                    "Farore chest's south approach is blocked by room geometry.");
                _player.WarpTo(south); _player.Face(Vector2I.Up);
                for (int i = 0; i < 24 && !_player.FacesTileWallForInteraction; i++)
                    StepGameplayUpdates(1, Vector2.Up, ["move_up"], ["move_up"], batched);
                FailIf(!_player.FacesTileWallForInteraction,
                    $"Farore chest approach did not publish both wall probes " +
                    $"(Link={_player.Position}, frontTile={_currentRoom.GetMetatile(_player.Position + Vector2.Up * 8):x2}, " +
                    $"frontSolid={_currentRoom.IsSolid(_player.Position + Vector2.Up * 8)}).");
                FailIf(_currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    "Farore chest approach entered solid geometry.");
                Press();
                FailIf(!_interactions.ChestRewardActive,
                    $"Farore secret chest quest ${quest:x2} did not open from the actual south floor " +
                    $"(Link={_player.Position}, tile={_currentRoom.GetMetatile(_player.Position + Vector2.Up * 8):x2}).");
                Step(36);
                FailIf(quest switch
                {
                    1 => !_inventory.HasTreasure(TreasureId.HeartContainer),
                    2 => _inventory.Bombchus != 0x10,
                    3 or 6 => _inventory.UnappraisedRingCount != 1,
                    8 => !_inventory.HasTreasure(TreasureId.BiggoronSword),
                    _ => true
                }, $"Farore chest quest ${quest:x2} did not grant source treasure ${expectedTreasure:x2}:${expectedSubid:x2}.");
                _dialogue.Close(); Step(2);
                Step(32);
                FailIf(!_saveData.HasGlobalFlag(0x5a + quest) ||
                    _saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting) ||
                    _currentRoom.GetMetatile(new Vector2(0x58, 0x78)) != 0xac,
                    $"Farore chest quest ${quest:x2} completion did not set done flag, clear $2c and restore tile $ac.");
            }
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Farore reward room exit retained input control.");
        }
        GD.Print("Validated Farore quest $00 upgrade and five native chest rewards " +
            "through MENU_SECRET, shared chest opening, item collection, " +
            "done flags and room exit in split/batched gameplay updates.");
    }

    private void ValidateFaroreUnlinkedSecrets()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("Farore", StringComparison.OrdinalIgnoreCase),
                "Completed unlinked Farore did not offer TX_5502.");
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            FailIf(!_secretEntry.IsActive,
                "Completed unlinked Farore did not request askforsecret $ff.");
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(codec.GenerateSecretValues(0x30, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(2);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                "King Zora", StringComparison.OrdinalIgnoreCase),
                "Unlinked Farore valid Ages secret $30 did not show TX_550f hint.");
            _dialogue.Close(); Step(30);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                "I, too", StringComparison.OrdinalIgnoreCase),
                "Unlinked Farore did not show TX_5504 after wait 30.");
            _dialogue.Close(); Step();
            FailIf(!_dialogue.ChoiceActive || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("Holodrum", StringComparison.OrdinalIgnoreCase),
                "Unlinked Farore did not offer the game-transfer secret after hint.");
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage.Contains("\\secret", StringComparison.Ordinal) ||
                _saveData.ReadWramByte(WramAddress.wFileIsLinkedGame) != 0,
                "Unlinked Farore did not generate and substitute both transfer-secret halves.");
            _dialogue.Close();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Unlinked Farore transfer-secret room exit retained input control.");
        }
        GD.Print("Validated completed unlinked Farore $30 hint, 30-update wait, " +
            "TX_5504, Holodrum offer and generated transfer secret in split/batched gameplay.");
    }

    private void ValidateFaroreUpgradeVariants()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (int quest in new[] { 4, 5, 7, 9 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x50 + quest);
            if (quest == 7) _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero);
            ApproachLibraryActor(farore, false);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]); Step();
            _dialogue.Close(); Step();
            _dialogue.SubmitChoiceForValidation(0); Step();
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(codec.GenerateSecretValues(0x30 + quest, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(170);
            FailIf(!_dialogue.IsOpen || _saveData.HasGlobalFlag(0x5a + quest),
                $"Farore upgrade quest ${quest:x2} skipped the held treasure text or set done early.");
            switch (quest)
            {
                case 4:
                    FailIf(_inventory.ShieldLevel != 2,
                        "Farore quest $04 did not grant TREASURE_OBJECT_SHIELD_01 parameter $02.");
                    break;
                case 5:
                    FailIf(_inventory.MaxBombs != 0x30 || _inventory.Bombs != 0x30,
                        "Farore quest $05 did not add packed-BCD $20 to both bomb bytes.");
                    break;
                case 7:
                    FailIf(_inventory.SeedSatchelLevel < 2,
                        "Farore quest $07 did not grant the Seed Satchel upgrade.");
                    break;
                case 9:
                    FailIf(_inventory.RingBoxLevel != 2,
                        "Farore quest $09 did not retain the source L-2 Ring Box grant.");
                    break;
            }
            _dialogue.Close(); Step(31);
            FailIf(!_saveData.HasGlobalFlag(0x5a + quest) ||
                _saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting) ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "power", StringComparison.OrdinalIgnoreCase),
                $"Farore upgrade quest ${quest:x2} did not finish with TX_5509 and done flag.");
            _dialogue.Close(); Step();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                $"Farore upgrade quest ${quest:x2} retained input control after room exit.");
        }
        GD.Print("Validated Farore native upgrades $04/$05/$07/$09, held item text, " +
            "packed-BCD bomb capacity, satchel and ring-box levels, completion flags and exit.");
    }
}
