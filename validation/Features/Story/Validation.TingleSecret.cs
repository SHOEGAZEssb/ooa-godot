using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTingleSecret()
    {
        // scripts/ages/scripts.s:@postgame asks TINGLE_SECRET $07, waits $1e,
        // then grants the upgrade. Level 3 generates return index $17.
        ReinitializeGameplayForValidation();
        TingleDatabase source = new();
        TingleRecord record = source.Record;
        FailIf(record.SecretIndex != 0x07 || record.ReturnSecretIndex != 0x17,
            "Tingle's imported secret indices diverged from constants/common/secrets.s.");
        _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
        FailIf(_saveData.IsCompleted,
            "Tingle's finished-game fixture must leave wFileIsCompleted clear.");
        _inventory.GiveTreasure(record.IslandChartTreasure, 1);
        _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
        _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
        foreach (int treasure in new[] { TreasureId.EmberSeeds,
            TreasureId.EmberSeeds + 1, TreasureId.EmberSeeds + 2 })
            _inventory.GiveTreasure(treasure, 1);
        LoadValidationRoom(0, 0x79);
        StepGameplayUpdates(1, Vector2.Zero);
        TingleRoomEntity tingle = _entities.Entities<TingleRoomEntity>().Single();
        Rect2 balloon = new(tingle.Npc.Position - new Vector2(4, 4), new Vector2(8, 8));
        FailIf(!_entities.ApplySwordHit(balloon, tingle.Npc.Position, 1,
                EnemyKnockbackStrength.Normal, itemZ: tingle.CollisionZ),
            "Tingle secret fixture could not pop PART_TINGLE_BALLOON $44.");
        int falling = 0;
        while (!tingle.Grounded && falling++ < 120)
            StepGameplayUpdates(1, Vector2.Zero);
        FailIf(!tingle.Grounded || !tingle.HasEnoughSeedTypes,
            "Tingle did not ground with three source seed types before secret entry.");
        ApproachLibraryActor(tingle.Npc, false);
        StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
        TingleEvent quest = _roomEvents.Get<TingleEvent>();
        FailIf(quest.Stage != TingleEventStage.PostgamePrompt ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(source.Text(0x1e09)),
            $"Tingle's finished-game level-2 Satchel prompt was unreachable: stage {quest.Stage}, text '{_dialogue.CurrentMessage}', chart {_inventory.HasTreasure(record.IslandChartTreasure)}, satchel {_inventory.SeedSatchelLevel}, flag {_saveData.HasGlobalFlag(GlobalFlag.FinishedGame)}, actor {tingle.Grounded}, player {_player.Position}.");
        _dialogue.SubmitChoiceForValidation(0);
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(quest.Stage != TingleEventStage.SecretEntry ||
            !_secretEntry.IsActive || !_gameplayPause.IsLeased,
            "Tingle's askforsecret did not acquire MENU_SECRET for index $07.");
        for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
        _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff]);
        FailIf(!_secretEntry.IsActive || _saveData.HasGlobalFlag(record.BeganSecretFlag),
            "Invalid Tingle secret advanced GLOBALFLAG_BEGAN_TINGLE_SECRET $6b.");
        var codec = new LinkedGameNpcDatabase();
        _secretEntry.Submit(codec.GenerateSecretValues(record.SecretIndex, _saveData));
        for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(_secretEntry.IsActive || quest.Stage != TingleEventStage.SecretWait ||
            quest.Counter != 30 || _saveData.HasGlobalFlag(record.BeganSecretFlag),
            "Tingle's MENU_SECRET close did not start source wait $1e.");
        StepGameplayUpdates(29, Vector2.Zero);
        FailIf(_saveData.HasGlobalFlag(record.BeganSecretFlag),
            "Tingle set secret flag $6b before the thirtieth wait update.");
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(quest.Stage != TingleEventStage.UpgradeAcceptedText ||
            !_saveData.HasGlobalFlag(record.BeganSecretFlag) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(source.Text(0x1e0e)),
            "Valid Tingle secret did not show TX_1e0e and set flag $6b.");
        _dialogue.Close();
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(quest.Stage != TingleEventStage.UpgradeAnnouncement ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(source.Text(0x1e0c)),
            "Valid Tingle secret did not enter the Satchel upgrade animation.");
        _dialogue.Close();
        int animation = 0;
        while (quest.Stage != TingleEventStage.UpgradeGlowWait && animation++ < 200)
            StepGameplayUpdates(1, Vector2.Zero);
        FailIf(quest.Stage != TingleEventStage.UpgradeGlowWait || quest.Counter != 120,
            "Tingle secret upgrade did not start its source 120-update glow.");
        StepGameplayUpdates(120, Vector2.Zero);
        GroundTreasurePickup reward = _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(quest.Stage != TingleEventStage.UpgradeReward ||
            reward.Record.TreasureObject != record.SatchelUpgradeObject ||
            _inventory.SeedSatchelLevel != 3 ||
            _saveData.HasGlobalFlag(record.DoneSecretFlag),
            "Tingle secret did not grant level-3 Satchel after glow or set return flag too early.");
        _dialogue.Close();
        StepGameplayUpdates(2, Vector2.Zero);
        FailIf(quest.HasState, "Tingle did not finish after the Satchel reward.");
        ApproachLibraryActor(tingle.Npc, false);
        StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
        FailIf(!_saveData.HasGlobalFlag(record.DoneSecretFlag) ||
            _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x17 ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage.Contains("\\secret1", StringComparison.Ordinal),
            "Tingle's level-3 visit did not generate TINGLE_RETURN_SECRET $17 and TX_1e0f.");
        LoadValidationRoom(0, 0x89);
        FailIf(quest.HasState, "Leaving Tingle's return-secret text retained event state.");

        ReinitializeGameplayForValidation();
        _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
        _inventory.GiveTreasure(record.IslandChartTreasure, 1);
        _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
        _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
        foreach (int treasure in new[] { TreasureId.EmberSeeds,
            TreasureId.EmberSeeds + 1, TreasureId.EmberSeeds + 2 })
            _inventory.GiveTreasure(treasure, 1);
        LoadValidationRoom(0, 0x79);
        StepGameplayUpdates(1, Vector2.Zero);
        tingle = _entities.Entities<TingleRoomEntity>().Single();
        balloon = new Rect2(tingle.Npc.Position - new Vector2(4, 4), new Vector2(8, 8));
        FailIf(!_entities.ApplySwordHit(balloon, tingle.Npc.Position, 1,
                EnemyKnockbackStrength.Normal, itemZ: tingle.CollisionZ),
            "Tingle invalid-secret fixture could not pop PART_TINGLE_BALLOON $44.");
        falling = 0;
        while (!tingle.Grounded && falling++ < 120)
            StepGameplayUpdates(1, Vector2.Zero);
        ApproachLibraryActor(tingle.Npc, false);
        StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
        quest = _roomEvents.Get<TingleEvent>();
        FailIf(quest.Stage != TingleEventStage.PostgamePrompt,
            "Tingle's postgame prompt was unreachable on the invalid-secret path.");
        _dialogue.SubmitChoiceForValidation(0);
        StepGameplayUpdates(1, Vector2.Zero);
        for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
        _secretEntry.Screen!.SelectSecretLowerOption(2);
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(
            pressed: ["attack"], justPressed: ["attack"], movement: Vector2.Zero));
        try { _secretEntry.Update(1.0 / 60.0); }
        finally { Input.EndOriginalUpdate(); }
        for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
        StepGameplayUpdates(30, Vector2.Zero);
        FailIf(quest.Stage != TingleEventStage.SecretWait ||
            _saveData.HasGlobalFlag(record.BeganSecretFlag),
            "Cancelling Tingle's secret menu skipped the source wait or set flag $6b.");
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(quest.Stage != TingleEventStage.EndText ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(source.Text(0x1e0d)) ||
            _inventory.SeedSatchelLevel != 2 ||
            _saveData.HasGlobalFlag(record.BeganSecretFlag),
            "Cancelled Tingle secret did not show TX_1e0d without granting a Satchel.");
        LoadValidationRoom(0, 0x89);
        FailIf(quest.HasState, "Leaving Tingle's invalid-secret text retained event state.");
        GD.Print("Validated Tingle $c8:$00 secret menu, invalid and valid input, wait $1e, level-3 reward, and return index $17 through gameplay updates.");
    }
}
