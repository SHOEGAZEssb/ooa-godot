using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHiddenShopInteractions()
    {
        var database = new LynnaShopDatabase(hidden: true);
        FailIf(database.Item(0).TreasureId != 0x2c || database.Item(0).Parameter != 2 ||
            database.Item(0x14).Parameter != 3 || database.Item(2).Price != 300 ||
            database.Item(6).Price != 500 || database.Item(0x15).TreasureId != 0x2b ||
            database.Item(0x15).Price != 500 || database.Item(5).Parameter != 3 ||
            database.Item(5).Price != 300,
            "$2:$7e lost source shopItemTreasureToGive/prices for $00/$02/$05/$06/$14/$15.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.WriteWramByte(0xc642, 0);
            _saveData.WriteWramByte(0xc643, 0);
            SetTreasure(_saveData, 0x2c, false);
            LoadValidationRoom(2, 0x7e);
            LynnaShopEvent shop = _roomEvents.Get<LynnaShopEvent>();
            NpcCharacter keeper = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x46);
            var stock = _entities.Entities<LynnaShopItem>();
            FailIf(!stock.Select(i => i.Record.SubId).SequenceEqual(new[] { 0, 2, 0x15 }) ||
                !stock.Select(i => i.Position).SequenceEqual(new[] {
                    new Vector2(64,40), new Vector2(96,40), new Vector2(128,40) }) ||
                keeper.Position != new Vector2(24,88) || shop.ChestGame,
                "$2:$7e must retain $47:$00/$02/$15 then $46:$01 source order and placement.");

            void Step(int n) => StepGameplayUpdates(n, Vector2.Zero, batched: batched);
            void Walk(Vector2 point)
            {
                for (int axis = 0; axis < 2; axis++)
                {
                    int limit = 180;
                    while (Mathf.Abs(axis == 0 ? _player.Position.X-point.X : _player.Position.Y-point.Y) > 1)
                    {
                        if (--limit == 0)
                            throw new InvalidOperationException($"Hidden shop collision blocked {_player.Position} -> {point}.");
                        Vector2 direction = axis == 0
                            ? new Vector2(Mathf.Sign(point.X-_player.Position.X),0)
                            : new Vector2(0,Mathf.Sign(point.Y-_player.Position.Y));
                        string action = direction == Vector2.Left ? "move_left" : direction == Vector2.Right
                            ? "move_right" : direction == Vector2.Up ? "move_up" : "move_down";
                        StepGameplayUpdates(1,direction,[action],[action]);
                    }
                }
                Step(1);
                FailIf(_rooms.CurrentRoom.IsSolid(_player.Position),
                    $"Hidden shop approach ended inside a solid tile at {_player.Position}.");
            }
            void PressA()
            {
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
                Step(1);
            }
            void Text(int id) => FailIf(!_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(id)),
                $"$2:$7e expected TX_{id:x4}, stage {shop.Stage}: {_dialogue.CurrentMessage}");
            void Close() { _dialogue.Close(); Step(2); }
            void Merchant()
            {
                Walk(new Vector2(64,72)); Walk(new Vector2(52,88));
                _player.Face(Vector2I.Left); PressA();
            }
            void Shelf(int x)
            {
                Walk(new Vector2(52,72)); Walk(new Vector2(x,58));
                _player.Face(Vector2I.Up); PressA();
            }

            _player.WarpTo(new Vector2(64,104));
            Shelf(64);
            FailIf(!stock[0].Held || !_player.IsCarryingObject, "$47:$00 was not reachable from the real shelf.");
            Merchant(); Text(0x0e0b); Close();
            FailIf(stock[0].Held || shop.HasState, "No-ring-box rejection retained stock or input.");
            Shelf(96);
            Walk(new Vector2(96,72)); Walk(new Vector2(24,56));
            StepGameplayUpdates(40,Vector2.Up,["move_up"],["move_up"],batched);
            Text(0x0e07);
            FailIf(_player.Position.Y != 0x27 || keeper.Position != new Vector2(24,58) ||
                !_player.CutsceneControlled || !stock[1].Held,
                "Hidden theft must clamp Y=$27 and move up $0f SPEED_200 updates.");
            _dialogue.Close(); Step(18);
            FailIf(shop.Stage != LynnaShopEventStage.Holding || _player.CutsceneControlled ||
                keeper.Position != new Vector2(24,88),
                "Hidden theft return failed to restore the keeper and held-item control.");
            Walk(new Vector2(24,72)); Shelf(96);
            FailIf(stock[1].Held, "Stock could not be returned after the theft script.");
            _inventory.GiveTreasure(0x2c,1);
            _inventory.AddRupees(999-_inventory.Rupees);
            Shelf(64); Merchant();
            FailIf(!_dialogue.CurrentMessage.Contains("300",StringComparison.Ordinal), "$00 price was not 300.");
            _dialogue.SubmitChoiceForValidation(1); Step(2);
            FailIf(_inventory.Rupees != 999 || _saveData.ReadWramByte(0xc642) != 0 || stock[0].Held,
                "Declining hidden shop purchase changed inventory/flags or failed to return stock.");
            Shelf(64); Merchant();
            _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0058);
            FailIf(_inventory.RingBoxLevel != 2 || _inventory.Rupees != 699 ||
                _saveData.ReadWramByte(0xc642) != 1, "$47:$00 failed its L2 upgrade/300 rupee/bit0 transaction.");
            Step(1);
            FailIf(!_player.IsHoldingItemTwoHands || _player.IsCarryingObject || !stock[0].Purchasing,
                "shopItemState3 did not drop the carried pose and enter Link state04/$01.");
            Close();
            FailIf(!stock[0].Removed || _player.IsCarryingObject || shop.HasState,
                "Hidden purchase completion left held stock or gameplay ownership.");

            _inventory.AddRupees(-_inventory.Rupees);
            Shelf(96); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2);
            Text(0x0e06); Close();
            FailIf(_saveData.ReadWramByte(0xc642) != 1 || stock[1].Held,
                "Insufficient rupees consumed hidden Gasha stock or failed to return it.");
            _inventory.AddRupees(999);
            Shelf(96); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2);
            Text(0x004b); Close();
            FailIf(_saveData.ReadWramByte(0xc642) != 3 || _inventory.Rupees != 699,
                "First hidden Gasha seed did not cost 300 and set bit1.");

            // Source replacements occur at initialization, never immediately on purchase.
            _saveData.WriteWramByte(0xc642,2);
            _saveData.WriteWramByte(0xc643,0x40);
            LoadValidationRoom(2,0x7e);
            FailIf(!_entities.Entities<LynnaShopItem>().Select(i=>i.Record.SubId)
                .SequenceEqual(new[]{0x14,6,5}),
                "$2:$7e re-entry must select L3 ring box, second Gasha seed, tier3 ring.");
            _player.WarpTo(new Vector2(64,104));
            _inventory.AddRupees(999-_inventory.Rupees);
            Shelf(128); Merchant();
            int ringCalls=_random.Calls;
            int ringCount=_inventory.UnappraisedRingCount;
            SetDiggingRoll(_random,0);
            _dialogue.SubmitChoiceForValidation(0); Step(2); Text(0x0054);
            FailIf(_random.Calls != ringCalls+1 || _inventory.UnappraisedRingCount != ringCount+1 ||
                _inventory.UnappraisedRingAt(ringCount) != (0x0a|0x40) ||
                _inventory.Rupees != 699 || _saveData.ReadWramByte(0xc642) != 0x0a,
                "$47:$05 must buy tier3 CURSED_RING for RNG index0, cost300 and set bit3.");
            Close();

            Shelf(64); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2);
            Text(0x0059); Close();
            FailIf(_inventory.RingBoxLevel != 3 || _saveData.ReadWramByte(0xc642) != 0x0b,
                "$47:$14 did not grant the L3 box and set the shared upgrade bit.");
            _inventory.AddRupees(999-_inventory.Rupees);
            Shelf(96); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2);
            Text(0x004b); Close();
            FailIf(_inventory.Rupees != 499 || _saveData.ReadWramByte(0xc642) != 0x0f || shop.ChestGame,
                "$47:$06 must cost500/set bit2 without switching to chest mode during the visit.");

            _saveData.WriteWramByte(0xc642,0);
            _saveData.WriteWramByte(0xc643,0);
            LoadValidationRoom(2,0x7e);
            _player.WarpTo(new Vector2(64,104));
            for(int piece=0; piece<3; piece++) _inventory.GiveTreasure(0x2b,1);
            _inventory.AddRupees(999-_inventory.Rupees);
            int healthBefore=_inventory.MaxHealthQuarters;
            Shelf(128); Merchant(); _dialogue.SubmitChoiceForValidation(0); Step(2);
            Text(0x0017);
            for(int frame=0; frame<600 && _inventory.MaxHealthQuarters==healthBefore; frame++)
            {
                Step(1); _dialogue.AdvanceOrClose();
            }
            Text(0x0049);
            FailIf(_inventory.HeartPieces != 0 || _inventory.MaxHealthQuarters != healthBefore+4 ||
                _inventory.Rupees != 499 || (_saveData.ReadWramByte(0xc643)&0x40)==0,
                "$47:$15 lost the fourth-piece presentation, heart-container handoff, price or sold flag.");
            Close();

            _saveData.WriteWramByte(0xc642,0x0f);
            LoadValidationRoom(2,0x7e);
            keeper = _entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x46);
            FailIf(!shop.ChestGame || _entities.Entities<LynnaShopItem>().Count != 0 ||
                _rooms.CurrentRoom.GetMetatile(new Vector2(88,40)) != 0xf1 ||
                _rooms.CurrentRoom.GetMetatile(new Vector2(120,40)) != 0xf1,
                "Hidden shop did not switch to two chest tiles at source positions $25/$27 on re-entry.");
            _player.WarpTo(new Vector2(64,104));
            Merchant(); Text(0x0e0d);
            _dialogue.SubmitChoiceForValidation(1); Step(2); Text(0x0e11); Close();
            FailIf((_saveData.ReadWramByte(0xc642)&0x80)==0, "First chest-game explanation did not persist bit7.");
            _player.Face(Vector2I.Left); PressA(); Text(0x0e0e);
            int before = _inventory.Rupees;
            _dialogue.SubmitChoiceForValidation(0); Step(1);
            FailIf(_inventory.Rupees != before-10, "Chest game entry did not charge exactly 10 rupees.");
            int guard=200;
            while(shop.Stage != LynnaShopEventStage.ChestPrepareRight && --guard>0) Step(1);
            FailIf(guard==0 || keeper.Position != new Vector2(104,38) || shop.ChestWait != 60,
                $"Chest-game approach lost source counter2 zero updates: {keeper.Position}, {shop.Stage}.");
            Step(59);
            FailIf(shop.ChestWait != 1 || shop.Stage != LynnaShopEventStage.ChestPrepareRight,
                "Chest-game right wait ended before update 60.");
            Step(1); Step(59);
            FailIf(shop.ChestWait != 1 || shop.Stage != LynnaShopEventStage.ChestPrepareLeft,
                "Chest-game left wait ended before update 60.");
            Step(1); Text(0x0e10);
            SetDiggingRoll(_random, 1);
            int randomCalls = _random.Calls;
            Close();
            FailIf(_random.Calls != randomCalls+1, "Chest selection must consume one shared RNG call.");
            FailIf(shop.BlocksGameplay || shop.Stage != LynnaShopEventStage.ChestSelection,
                "Chest-game instructions did not release Link to choose a chest.");
            // Both choices are reachable through the cleared source floor. The
            // shared signal must not grant normal chest treasure or ROOMFLAG_ITEM.
            Walk(new Vector2(88,54)); _player.Face(Vector2I.Up); PressA();
            FailIf(_saveData.HasRoomFlag(2,0x7e,OracleSaveData.RoomFlagItem) ||
                _interactions.ChestRewardActive || !_dialogue.IsOpen ||
                shop.Stage is not (LynnaShopEventStage.ChestWrong or LynnaShopEventStage.ChestCorrect),
                $"Chest game bypassed its $cca2 signal or leaked normal chest rewards: {shop.Stage}, Link={_player.Position}, signal=${_entities.RuntimeState.ReadWramByte(0xcca2):x2}.");
            FailIf(shop.Stage != LynnaShopEventStage.ChestCorrect || shop.ChestRound != 1,
                "Source random bit1 must select left chest $25.");
            for (int round=2; round<=5; round++)
            {
                // Continue past the optional round3/4 payouts to the tier1 prize.
                Step(32);
                if (round >= 4) _dialogue.SubmitChoiceForValidation(0);
                else _dialogue.Close();
                Step(1); Step(120); Text(0x0e18);
                SetDiggingRoll(_random, 1); Close();
                _player.Face(Vector2I.Up); PressA();
                FailIf(shop.ChestRound != round || shop.Stage != LynnaShopEventStage.ChestCorrect,
                    $"Chest-game round {round} did not close/reopen the left chest and advance once.");
            }
            Text(0x0e16); Step(32);
            int ringsBefore = _inventory.UnappraisedRingCount;
            SetDiggingRoll(_random,0);
            Close();
            guard=200;
            while(shop.Stage != LynnaShopEventStage.ChestRingText && --guard>0) Step(1);
            Step(1); // checkLinkForceState selects state04, then returns.
            FailIf(_player.IsHoldingItemOneHand, "Ring grant dispatched Link state04 on its force-state update.");
            Step(1); // The following update initializes the one-handed item pose.
            FailIf(guard==0 || keeper.Position != new Vector2(24,88) ||
                _inventory.UnappraisedRingCount != ringsBefore+1 ||
                !_player.IsHoldingItemOneHand ||
                !_entities.Entities<GroundTreasurePickup>().Any(t => t.Held && t.Record.TreasureObject=="TREASURE_OBJECT_RING_00"),
                $"Round5 reward handoff: guard={guard}, keeper={keeper.Position}, rings={_inventory.UnappraisedRingCount}/{ringsBefore+1}, oneHand={_player.IsHoldingItemOneHand}, twoHands={_player.IsHoldingItemTwoHands}, held={_entities.Entities<GroundTreasurePickup>().Count(t=>t.Held)}, stage={shop.Stage}.");
            Text(0x0054); Close();
            FailIf(shop.HasState || _player.CutsceneControlled, "Ring prize left Link or the shopkeeper locked.");
            // Repeat after completion, lose, and reject replay.
            Walk(new Vector2(64,72)); Merchant(); Text(0x0e0e);
            _dialogue.SubmitChoiceForValidation(0); Step(1);
            guard=300;
            while(!_dialogue.IsOpen && --guard>0) Step(1);
            Text(0x0e10); SetDiggingRoll(_random,0); Close();
            Walk(new Vector2(88,54)); _player.Face(Vector2I.Up); PressA(); Text(0x0e17);
            _dialogue.SubmitChoiceForValidation(1); Step(1);
            guard=200;
            while(shop.HasState && --guard>0) Step(1);
            FailIf(guard==0 || keeper.Position != new Vector2(24,88),
                "Declining replay did not return the keeper and restore retail collision.");
            foreach(int stopRound in new[]{3,4})
            {
                Walk(new Vector2(64,72)); Merchant(); Text(0x0e0e);
                _dialogue.SubmitChoiceForValidation(0); Step(1);
                guard=300;
                while(!_dialogue.IsOpen && --guard>0) Step(1);
                Text(0x0e10); SetDiggingRoll(_random,1); Close();
                Walk(new Vector2(88,54));
                for(int round=1; round<=stopRound; round++)
                {
                    _player.Face(Vector2I.Up); PressA(); Step(32);
                    FailIf(shop.ChestRound!=round, $"Optional payout round {round} failed.");
                    if(round==stopRound) break;
                    if(round==3) _dialogue.SubmitChoiceForValidation(0);
                    else _dialogue.Close();
                    Step(1); Step(120); Text(0x0e18);
                    SetDiggingRoll(_random,1); Close();
                }
                _dialogue.SubmitChoiceForValidation(1); Step(1); Text(0x0e14);
                int prizeIndex=_inventory.UnappraisedRingCount;
                SetDiggingRoll(_random,0); Close();
                guard=200;
                while(shop.Stage!=LynnaShopEventStage.ChestRingText && --guard>0) Step(1);
                FailIf(guard==0 || _inventory.UnappraisedRingAt(prizeIndex) !=
                    ((stopRound==3 ? 0x0a : 0x0f)|0x40),
                    $"Round{stopRound} must grant source tier{6-stopRound} ring index0.");
                Close();
            }
            shop.Cancel();
            FailIf(shop.HasState || _player.CutsceneControlled || _player.IsCarryingObject,
                "Cancelling hidden shop retained gameplay ownership.");
        }
    }
}
