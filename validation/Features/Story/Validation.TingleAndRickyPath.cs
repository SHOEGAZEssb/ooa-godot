using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRooms079And089Interactions()
    {
        var tingleDatabase = new TingleDatabase();
        TingleRecord tingleRecord = tingleDatabase.Record;
        var tutorialDatabase = new CompanionTutorialDatabase();
        CompanionTutorialRecord room079Tutorial =
            tutorialDatabase.GetRoomRecords(0, 0x79).Single();
        CompanionTutorialRecord room089Tutorial =
            tutorialDatabase.GetRoomRecords(0, 0x89).Single();
        var barrierDatabase = new CompanionBarrierDatabase();
        if (!barrierDatabase.TryGet(0, 0x89, out CompanionBarrierRecord barrierRecord))
            throw new InvalidOperationException("Room 0:89 lost its companion barrier record.");

        CompanionRuntimeState.Clear(
            _runtimeState, CompanionRuntimeState.RickyId);
        CompanionRuntimeState.Remember(
            _runtimeState,
            CompanionRuntimeState.RickyId,
            0,
            0x79,
            new Vector2(0x88, 0x68));
        LoadValidationRoom(0, 0x79);
        _player.WarpTo(new Vector2(0x18, 0x70), recordSafe: false);

        TingleRoomEntity tingleEntity =
            _entities.Entities<TingleRoomEntity>().Single();
        RickyCompanionRoomEntity room079Ricky =
            _entities.Entities<RickyCompanionRoomEntity>().Single();
        CompanionTutorialRoomEntity tutorial079 =
            _entities.Entities<CompanionTutorialRoomEntity>().Single();
        FailIf(
            tingleEntity.Npc.Record is not
            {
                Group: 0, Room: 0x79, Id: 0xc8, SubId: 0x00,
                SpriteName: "spr_gorondance_tingle_write",
                TileBase: 4, Palette: 0,
                Implementation: NpcImplementationClassification.SpecializedNative
            } ||
            tingleEntity.State != 0 || !tingleEntity.BalloonActive ||
            tingleEntity.ZFixed != tingleRecord.InitialZ << 8 ||
            tingleEntity.BalloonCounter != tingleRecord.BalloonCounter ||
            tingleEntity.BalloonSpeedZ != tingleRecord.BalloonSpeedZ ||
            tingleEntity.Grounded || tingleEntity.HasEnoughSeedTypes ||
            tutorial079.Record != room079Tutorial ||
            room079Ricky.Phase != RickyCompanionPhase.Waiting,
            "Room 0:79 did not instantiate source-typed Tingle, balloon, " +
            "Ricky tutorial, and remembered dismounted Ricky state.");

        StepRoomEventFrames(1);
        FailIf(
            tingleEntity.State != 1 || tutorial079.State != 1 ||
            _dialogue.IsOpen,
            "Room 0:79 state-0 objects did not consume exactly one initial update.");
        StepRoomEventFrames(55);
        FailIf(
            tingleEntity.BalloonCounter != 1 ||
            tingleEntity.BalloonSpeedZ != -0x10,
            "PART_TINGLE_BALLOON reversed before its source $38-update counter.");
        StepRoomEventFrames(1);
        FailIf(
            tingleEntity.BalloonCounter != 0x38 ||
            tingleEntity.BalloonSpeedZ != 0x10,
            "PART_TINGLE_BALLOON did not reverse speedZ on update $38.");

        Rect2 balloonHitbox = new(
            tingleEntity.Npc.Position - new Vector2(4, 4),
            new Vector2(8, 8));
        bool groundedBalloonHit = _entities.ApplySwordHit(
            balloonHitbox,
            tingleEntity.Npc.Position,
            damage: 1,
            EnemyKnockbackStrength.Normal,
            itemZ: -2);
        FailIf(
            groundedBalloonHit || tingleEntity.State != 1 ||
            !tingleEntity.BalloonActive ||
            tingleEntity.CollisionZ != tingleEntity.ZFixed >> 8,
            "A grounded Link sword reached PART_TINGLE_BALLOON despite the " +
            "source Object.zh collision window.");

        int explosionCount = _entities.Entities<PuzzlePuffEffect>().Count;
        bool balloonHit = _entities.ApplySwordHit(
            balloonHitbox,
            tingleEntity.Npc.Position,
            damage: 1,
            EnemyKnockbackStrength.Normal,
            itemZ: tingleEntity.CollisionZ);
        FailIf(
            !balloonHit || tingleEntity.State != 2 ||
            tingleEntity.BalloonActive ||
            _entities.Entities<PuzzlePuffEffect>().Count != explosionCount + 1,
            "An airborne Link sword at the balloon's live Z did not increment " +
            "Tingle to state 2 and create the source-positioned explosion.");

        StepRoomEventFrames(1);
        FailIf(
            tingleEntity.State != 3 ||
            tingleEntity.BalloonCounter != tingleRecord.FallWait ||
            tingleEntity.Npc.CurrentScriptAnimationSource !=
                tingleDatabase.Animation("tingle", 2),
            "Tingle's popped-balloon state did not install animation $02/wait 15.");
        StepRoomEventFrames(tingleRecord.FallWait);
        FailIf(
            tingleEntity.Grounded || tingleEntity.BalloonCounter != 0,
            "Tingle began falling before all 15 source wait updates elapsed.");
        int fallUpdates = 0;
        while (!tingleEntity.Grounded && fallUpdates < 100)
        {
            StepRoomEventFrames(1);
            fallUpdates++;
        }
        FailIf(
            fallUpdates != 25 || !tingleEntity.Grounded ||
            tingleEntity.ZFixed != 0 ||
            tingleEntity.Npc.CurrentScriptAnimationSource !=
                tingleDatabase.Animation("tingle", 1),
            $"Tingle's $10-gravity fall landed after {fallUpdates} updates instead of 25.");

        TingleEvent tingleEvent = _roomEvents.Tingle;
        FailIf(
            !ReferenceEquals(tingleEvent.Actor, tingleEntity) ||
            !tingleEvent.TryInteractNpc(tingleEntity.Npc) ||
            !_dialogue.ChoiceActive ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e00)) ||
            !_saveData.HasGlobalFlag(tingleRecord.MetFlag),
            "Tingle's first A-button interaction did not set GLOBALFLAG_MET_TINGLE " +
            "and open TX_1e00's friendship choice.");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(1);
        FailIf(
            tingleEvent.Stage != TingleEventStage.EndText ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e03)),
            "Declining Tingle's friendship did not select TX_1e03.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            tingleEvent.Stage != TingleEventStage.KoolooText ||
            !tingleEntity.KoolooActive || !_dialogue.IsOpen ||
            !_dialogue.CurrentMessage.StartsWith(
                "Tingle, Tingle!\nKooloo-Limpah!", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains(
                "These are the\nmagic words", StringComparison.Ordinal),
            "TX_1e05 did not expand its source TX_1e0c call or start animation $03.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        int koolooUpdates = 0;
        while (tingleEvent.Stage != TingleEventStage.Inactive && koolooUpdates < 200)
        {
            StepRoomEventFrames(1);
            koolooUpdates++;
        }
        FailIf(
            tingleEvent.Stage != TingleEventStage.Inactive ||
            tingleEntity.KoolooActive || !tingleEntity.KoolooComplete,
            "The declined-friend kooloo-limpah animation did not return to Tingle's loop.");

        FailIf(
            !tingleEvent.TryInteractNpc(tingleEntity.Npc) ||
            !_dialogue.ChoiceActive ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e01)),
            "Met Tingle did not use TX_1e01 for the repeated friendship offer.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        FailIf(
            tingleEvent.Stage != TingleEventStage.FriendAcceptedText ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e02)),
            "Accepting Tingle's friendship did not show TX_1e02.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        GroundTreasurePickup chart =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            tingleEvent.Stage != TingleEventStage.ChartReward ||
            !_inventory.HasTreasure(tingleRecord.IslandChartTreasure) ||
            !chart.Held || !_dialogue.IsOpen,
            "tingleScript did not grant and hold the Island Chart after TX_1e02.");

        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(1);
        FailIf(
            tingleEvent.Stage != TingleEventStage.PostChartText ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e04)),
            "Closing the Island Chart reward did not advance to TX_1e04.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            tingleEvent.Stage != TingleEventStage.KoolooText ||
            !_dialogue.IsOpen || !tingleEntity.KoolooActive,
            "The chart path did not call Tingle's kooloo-limpah sequence.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        int chartKoolooUpdates = 0;
        while (tingleEvent.Stage != TingleEventStage.PostChartWait &&
            chartKoolooUpdates < 200)
        {
            StepRoomEventFrames(1);
            chartKoolooUpdates++;
        }
        FailIf(
            tingleEvent.Stage != TingleEventStage.PostChartWait ||
            tingleEvent.Counter != tingleRecord.PostChartWait,
            "The post-chart animation did not install the exact wait 60.");
        StepRoomEventFrames(tingleRecord.PostChartWait - 1);
        FailIf(
            tingleEvent.Counter != 1 ||
            room079Ricky.Phase != RickyCompanionPhase.Waiting,
            "tingleScript retired Ricky before all 60 post-chart updates elapsed.");
        StepRoomEventFrames(1);
        FailIf(
            tingleEvent.Stage != TingleEventStage.Inactive ||
            room079Ricky.Phase != RickyCompanionPhase.TingleDeparture,
            "tingleScript did not write Ricky's departure state after wait 60.");
        StepRoomEventFrames(1);
        FailIf(
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x2006)),
            "Ricky's Tingle departure did not show imported TX_2006.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        int departureUpdates = 0;
        while (!room079Ricky.Finished && departureUpdates < 300)
        {
            StepRoomEventFrames(1);
            departureUpdates++;
        }
        FailIf(
            !room079Ricky.Finished ||
            (_saveData.ReadWramByte(0xc646) & 0x40) == 0 ||
            CompanionRuntimeState.AnyActive(_runtimeState) ||
            CompanionRuntimeState.ReadRemembered(_runtimeState).Id != 0,
            "Ricky's room 0:79 departure did not set wRickyState bit $40 and " +
            "clear live/remembered companion ownership.");

        _dialogue.Close();
        using (_saveData.BeginMutation())
        {
            _saveData.WriteWramByte(
                room089Tutorial.FlagAddress,
                (byte)(_saveData.ReadWramByte(room089Tutorial.FlagAddress) &
                    ~(1 << room089Tutorial.FlagBit)));
            _saveData.WriteWramByte(
                0xc646,
                (byte)(_saveData.ReadWramByte(0xc646) & ~0xc0));
        }
        CompanionRuntimeState.Begin(
            _runtimeState,
            CompanionRuntimeState.RickyId,
            0x89,
            new Vector2(0x38, 0x70),
            direction: 2);
        LoadValidationRoom(0, 0x89);
        RickyCompanionRoomEntity room089Ricky =
            _entities.Entities<RickyCompanionRoomEntity>().Single();
        CompanionTutorialRoomEntity tutorial089 =
            _entities.Entities<CompanionTutorialRoomEntity>().Single();
        CompanionBarrierRoomEntity barrier089 =
            _entities.Entities<CompanionBarrierRoomEntity>().Single();
        FailIf(
            tutorial089.Record != room089Tutorial ||
            barrier089.Record is not
            {
                Group: 0, Room: 0x89, Order: 1,
                Id: 0x71, SubId: 0x02, Y: 0x6d, X: 0x38
            } ||
            tutorial089.Record.Order >= barrier089.Record.Order ||
            room089Ricky.Phase != RickyCompanionPhase.Riding,
            "Room 0:89 lost companion -> `$d0:$00 -> `$71:$02 source ordering.");
        StepRoomEventFrames(1);
        FailIf(
            tutorial089.State != 1 || barrier089.State != 1 ||
            _dialogue.IsOpen,
            "Room 0:89 state-0 tutorial/barrier initialization did not share " +
            "the first mounted update.");
        StepRoomEventFrames(1);
        FailIf(
            tutorial089.State != 2 || !tutorial089.TextShown ||
            room089Ricky.PrecisePosition.Y != barrierRecord.Y ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                barrierRecord.Message(CompanionRuntimeState.RickyId)),
            "Room 0:89 did not show ordered TX_2008 then TX_2007 and clamp " +
            "mounted Ricky to the lower-Y boundary in the same interaction pass.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            !tutorial089.Finished ||
            (_saveData.ReadWramByte(room089Tutorial.FlagAddress) &
                (1 << room089Tutorial.FlagBit)) == 0 ||
            barrier089.Finished,
            "Ricky crossing below room 0:89's tutorial marker did not set " +
            "wCompanionTutorialTextShown bit $00 while retaining the barrier.");

        _dialogue.Close();
        CompanionRuntimeState.Clear(
            _runtimeState, CompanionRuntimeState.RickyId);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSeedSatchel, 1);
        _inventory.GiveTreasure(TreasureDatabase.TreasureEmberSeeds + 1, 1);
        _inventory.GiveTreasure(TreasureDatabase.TreasureEmberSeeds + 2, 1);
        LoadValidationRoom(0, 0x79);
        TingleRoomEntity upgradeTingle =
            _entities.Entities<TingleRoomEntity>().Single();
        StepRoomEventFrames(1);
        var upgradeHitSpawns = new List<RoomEntitySpawn>();
        upgradeTingle.ApplySwordHit(
            new Rect2(
                upgradeTingle.Npc.Position - new Vector2(4, 4),
                new Vector2(8, 8)),
            upgradeTingle.Npc.Position,
            1,
            EnemyKnockbackStrength.Normal,
            upgradeHitSpawns);
        StepRoomEventFrames(1 + tingleRecord.FallWait);
        int upgradeFallUpdates = 0;
        while (!upgradeTingle.Grounded && upgradeFallUpdates < 100)
        {
            StepRoomEventFrames(1);
            upgradeFallUpdates++;
        }
        FailIf(
            !upgradeTingle.Grounded || !upgradeTingle.HasEnoughSeedTypes ||
            _inventory.SeedSatchelLevel != 1,
            "Tingle did not snapshot three obtained seed types with a level-1 Satchel.");

        TingleEvent upgradeEvent = _roomEvents.Tingle;
        FailIf(
            !upgradeEvent.TryInteractNpc(upgradeTingle.Npc) ||
            upgradeEvent.Stage != TingleEventStage.SatchelPrompt ||
            !_dialogue.ChoiceActive ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e06)),
            "The chart-owned, three-seed-type path did not open TX_1e06.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        FailIf(
            upgradeEvent.Stage != TingleEventStage.UpgradeAcceptedText ||
            !_saveData.HasGlobalFlag(tingleRecord.UpgradeFlag) ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e07)),
            "Accepting Tingle's Satchel offer did not set global flag $46 and show TX_1e07.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            upgradeEvent.Stage != TingleEventStage.UpgradeAnnouncement ||
            !upgradeTingle.KoolooActive ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(tingleDatabase.Text(0x1e0c)),
            "The Satchel path did not pair animation $03 with TX_1e0c.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        int upgradeKoolooUpdates = 0;
        while (upgradeEvent.Stage != TingleEventStage.UpgradeGlowWait &&
            upgradeKoolooUpdates < 200)
        {
            StepRoomEventFrames(1);
            upgradeKoolooUpdates++;
        }
        FailIf(
            upgradeEvent.Stage != TingleEventStage.UpgradeGlowWait ||
            upgradeEvent.Counter != tingleRecord.UpgradeGlowWait,
            "The Satchel animation did not install the exact wait 120.");
        StepRoomEventFrames(tingleRecord.UpgradeGlowWait - 1);
        FailIf(
            upgradeEvent.Counter != 1 || _inventory.SeedSatchelLevel != 1,
            "Tingle granted the Satchel upgrade before all 120 glow updates elapsed.");
        StepRoomEventFrames(1);
        GroundTreasurePickup satchelUpgrade =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            upgradeEvent.Stage != TingleEventStage.UpgradeReward ||
            _inventory.SeedSatchelLevel != 2 || !satchelUpgrade.Held ||
            !_dialogue.IsOpen,
            "Tingle did not grant TREASURE_OBJECT_SEED_SATCHEL_UPGRADE after wait 120.");
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(1);
        FailIf(
            upgradeEvent.Stage != TingleEventStage.Inactive ||
            _inventory.EmberSeeds != 0x50 ||
            _inventory.ScentSeeds != 0x50 ||
            _inventory.PegasusSeeds != 0x50,
            "refillSeedSatchel did not refill each obtained type to level-2 capacity.");

        GD.Print(
            "Validated rooms 0:79/0:89 Tingle `$c8:$00 + balloon `$44, " +
            "friend/chart/Satchel/kooloo/Ricky-departure script paths, companion " +
            "tutorials `$d0:$01/$00, and source-ordered `$71:$02 lower-Y barrier.");
    }
}
