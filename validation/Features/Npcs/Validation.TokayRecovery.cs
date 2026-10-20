using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayItemRecoveryGameplay()
    {
        // tokay.s:@initSubid06-$0a, tokayIslandStolenItems and
        // scripts.s:tokayHoldingItemScript. Expectations are source literals,
        // independent of the generated holder table.
        (int SubId, int Group, int Room, int Treasure)[] holders =
        [
            (0x06, 5, 0xca, 0x05), (0x07, 1, 0xcb, 0x15),
            (0x08, 1, 0xda, 0x11), (0x09, 5, 0xcc, 0x2e),
            (0x0a, 1, 0xbb, 0x19)
        ];
        foreach (bool batched in new[] { false, true })
        foreach (var row in holders)
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            // Theft clears obtained bits, retaining upgrade levels. Seed
            // satchel return must restore its original level, not increment it.
            _inventory.GiveTreasure(row.Treasure, 1);
            int satchelLevel = _inventory.SeedSatchelLevel;
            _inventory.LoseTreasure(row.Treasure);
            _inventory.SetScriptedEquippedItems(0, 0);
            _saveData.SetRoomFlag(row.Group, row.Room, 0x40, false);
            LoadValidationRoom(row.Group, row.Room);
            var actor = _entities.Entities<TokayHoldingItemCharacter>()
                .Single(npc => npc.Record.SubId == row.SubId);
            string source = $"Tokay $48:${row.SubId:x2} room {row.Group}:{row.Room:x2}, batched={batched}";
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            void Approach()
            {
                foreach (int distance in new[] { 40, 32, 24, 20 })
                foreach (var (direction, action) in new[]
                {
                    (Vector2.Up, "move_up"), (Vector2.Down, "move_down"),
                    (Vector2.Left, "move_left"), (Vector2.Right, "move_right")
                })
                {
                    Vector2 start = actor.Position - direction * distance;
                    bool safe = Enumerable.Range(0, distance - 12).All(i =>
                    {
                        Vector2 point = start + direction * i;
                        return point.X >= 6 && point.Y >= 6 && point.X < _currentRoom.Width - 6 && point.Y < _currentRoom.Height - 6 &&
                            !_collision.Collides(point) &&
                            new[] { new Vector2(-5, -2), new Vector2(5, -2), new Vector2(-5, 5), new Vector2(5, 5) }
                                .All(offset => _currentRoom.GetTerrainInfo(point + offset).Hazard == HazardType.None);
                    });
                    if (!safe) continue;
                    _player.WarpTo(start);
                    StepGameplayUpdates(distance, direction, [action], [action], batched);
                    FailIf(_collision.Collides(_player.Position) || !actor.CanTalkTo(_player),
                        source + ": ordinary collision approach must reach the A-button range.");
                    return;
                }
                string geometry = string.Join("; ", new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right }
                    .Select(direction => $"{direction}:" + string.Join(",", new[] { 12, 16, 20, 24, 32, 40 }
                        .Select(distance => $"{distance}={_collision.Collides(actor.Position + direction * distance)}"))));
                throw new InvalidOperationException(source + $": no safe straight approach declared (actor={actor.Position}, size={_currentRoom.Width}x{_currentRoom.Height}; {geometry}).");
            }
            Approach();
            Press();
            FailIf(!_dialogue.IsOpen || _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0x81 ||
                _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0x80 || _inventory.HasTreasure(row.Treasure),
                source + ": real A input must begin the original held-item conversation.");
            // Replacing the room before the reward must cancel its control
            // and leave both the treasure and completion flag untouched.
            _dialogue.Close();
            LoadValidationRoom(row.Group, row.Room);
            actor = _entities.Entities<TokayHoldingItemCharacter>().Single(npc => npc.Record.SubId == row.SubId);
            FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 || _player.CutsceneControlled || _inventory.HasTreasure(row.Treasure) ||
                _saveData.HasRoomFlag(row.Group, row.Room, 0x40) || !actor.HeldItemVisible,
                source + ": cancelling before return must release control without granting the item.");
            Approach(); Press();
            FailIf(!_dialogue.IsOpen,
                source + ": a cancelled conversation must be reachable again.");
            _dialogue.Close();
            Step(1);
            Step(29);
            FailIf(_inventory.HasTreasure(row.Treasure) ||
                !actor.HeldItemVisible, source + ": wait30 must retain the held item through update29.");
            Step(1);
            FailIf(!actor.HeldItemVisible || _entities.Entities<GroundTreasurePickup>().Count != 0,
                source + ": setanimation yields before writing the accessory-removal signal.");
            Step(1);
            FailIf(actor.HeldItemVisible || _entities.Entities<GroundTreasurePickup>().Count != 0,
                source + ": writeobjectbyte yields before the treasure helper.");
            Step(1);
            FailIf(actor.HeldItemVisible || _entities.Entities<GroundTreasurePickup>().Count != 1 ||
                _saveData.HasRoomFlag(row.Group, row.Room, 0x40),
                source + ": wait30 completion must create the item before setting room flag$40.");
            Step(4);
            FailIf(!_dialogue.IsOpen || !_inventory.HasTreasure(row.Treasure) ||
                (row.SubId == 0x06 ? !_player.IsHoldingItemOneHand : !_player.IsHoldingItemTwoHands),
                source + ": the returned sword uses one hand; the other returned items use two hands.");
            for (int update = 0; update < 240 && !_saveData.HasRoomFlag(row.Group, row.Room, 0x40); update++)
            {
                if (_dialogue.IsOpen) _dialogue.Close();
                Step(1);
            }
            Step(2);
            FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 || _player.CutsceneControlled || _player.IsHoldingItemTwoHands || _player.IsHoldingItemOneHand ||
                !_saveData.HasRoomFlag(row.Group, row.Room, 0x40) ||
                !_inventory.HasTreasure(row.Treasure) || _entities.Entities<GroundTreasurePickup>().Count != 0 ||
                (row.SubId == 0x0a && _inventory.SeedSatchelLevel != satchelLevel),
                source + ": actual gameplay must finish the return, flag and player/item handoff.");
            Approach();
            Press();
            FailIf(!_dialogue.IsOpen ||
                _entities.Entities<GroundTreasurePickup>().Count != 0,
                source + ": repeated A conversation must use the returned-item loop without another reward.");
            _dialogue.Close(); Step(1);
            LoadValidationRoom(row.Group, row.Room);
            actor = _entities.Entities<TokayHoldingItemCharacter>().Single(npc => npc.Record.SubId == row.SubId);
            FailIf(actor.HeldItemVisible, source + ": re-entry must retain the recovered item state.");
        }
        GD.Print("Validated five stolen-item Tokay returns through real collision approach/A input, wait30, player/item completion, repeat and re-entry in split/batched updates.");
    }

    private void ValidateTokayTradingGameplay()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool bracelet in new[] { false, true })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            var shop = new TokayShopDatabase();
            _saveData.SetGlobalFlag(shop.BoughtFeatherFlag, false);
            _saveData.SetGlobalFlag(shop.BoughtBraceletFlag, false);
            _inventory.LoseTreasure(0x17); _inventory.LoseTreasure(0x16);
            _inventory.GiveTreasure(0x15, 1);
            _inventory.LoseTreasure(0x19);
            _inventory.SetScriptedEquippedItems(0, 0);
            LoadValidationRoom(2, 0xe4);
            var owner = _roomEvents.Get<TokayTradingEvent>();
            int original = bracelet ? 1 : 0;
            int treasure = bracelet ? 0x16 : 0x17;
            int boughtFlag = bracelet ? shop.BoughtBraceletFlag : shop.BoughtFeatherFlag;
            string source = $"Tokay shop $81:${original:x2}, batched={batched}";
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            void Approach()
            {
                var stock = _entities.Entities<TokayShopItem>().Single(item => item.OriginalSubId == original);
                Vector2 start = stock.Position + Vector2.Down * 40;
                FailIf(Enumerable.Range(0, 28).Any(i => _collision.Collides(start + Vector2.Up * i)),
                    source + ": the declared trading-hut floor approach must be unobstructed.");
                _player.WarpTo(start);
                StepGameplayUpdates(40, Vector2.Up, ["move_up"], ["move_up"], batched);
                FailIf(_collision.Collides(_player.Position) || !stock.CanInteract(_player),
                    source + ": walking into stock must stop outside its collision and reach A range.");
            }
            void CompleteReward()
            {
                for (int update = 0; update < 240 && owner.HasState; update++)
                {
                    if (_dialogue.IsOpen) _dialogue.Close();
                    Step(1);
                }
                // linkState04 initializes its get-item pose before testing
                // inactive text on the following update; returning state01
                // then yields once more before normal input resumes.
                Step(2);
                FailIf(owner.HasState || _player.CutsceneControlled || _player.IsHoldingItemTwoHands ||
                    _entities.Entities<GroundTreasurePickup>().Count != 0,
                    source + $": reward completion must restore player control and remove its treasure (stage={owner.Stage}, controlled={_player.CutsceneControlled}, held={_player.IsHoldingItemTwoHands}, treasures={_entities.Entities<GroundTreasurePickup>().Count}, normal={_player.NativeNormalStateForInteraction}, getPhase={SomariaPrivate<int>(_player, "_getItemStatePhase")}, text={_dialogue.IsOpen}).");
            }
            Approach(); Press();
            FailIf(owner.Stage != TokayTradingStage.ShopPrompt || !_dialogue.IsOpen,
                source + ": real A input must offer the shovel exchange.");
            _dialogue.SubmitChoiceForValidation(0); Step(1);
            FailIf(!_inventory.HasTreasure(treasure) || _inventory.HasTreasure(0x15) ||
                _saveData.HasGlobalFlag(boughtFlag),
                source + ": borrowing must exchange the shovel without setting the purchase flag.");
            CompleteReward();
            Approach(); Press();
            FailIf(owner.Stage != TokayTradingStage.ShopPrompt, source + ": borrowed item must expose its shovel-return stock.");
            _dialogue.SubmitChoiceForValidation(0); Step(1);
            FailIf(owner.Stage != TokayTradingStage.ShopReturnText || !_inventory.HasTreasure(treasure),
                source + ": return text TX_0a28 must precede losing the borrowed item.");
            _dialogue.Close(); Step(1); CompleteReward();
            FailIf(_inventory.HasTreasure(treasure) || !_inventory.HasTreasure(0x15),
                source + ": returning the borrowed item must restore the shovel.");

            _inventory.GiveTreasure(0x19, 1);
            _inventory.GiveTreasure(bracelet ? 0x21 : 0x24, 1);
            void Seeds(int count)
            {
                if (bracelet) _inventory.SetScentSeedsFromScript(count);
                else _inventory.SetMysterySeedsFromScript(count);
            }
            Seeds(0x09);
            Approach(); Press();
            _dialogue.SubmitChoiceForValidation(0); Step(1);
            FailIf(owner.Stage != TokayTradingStage.ShopResultText || _inventory.HasTreasure(treasure) ||
                _saveData.HasGlobalFlag(boughtFlag), source + ": nine seeds must reject the permanent purchase.");
            _dialogue.Close(); Step(1);
            Seeds(0x10);
            Approach(); Press();
            _dialogue.SubmitChoiceForValidation(0); Step(1);
            CompleteReward();
            FailIf(!_inventory.HasTreasure(treasure) || !_saveData.HasGlobalFlag(boughtFlag) ||
                (bracelet ? _inventory.ScentSeeds : _inventory.MysterySeeds) != 0,
                source + ": ten seeds must purchase the item permanently and consume exactly packed-BCD $10.");
            LoadValidationRoom(2, 0xe4);
            Step(1);
            FailIf(_entities.Entities<TokayShopItem>().Any(item => item.OriginalSubId == original),
                source + ": purchased stock must remain absent after re-entry.");
        }
        GD.Print("Validated actual trading-hut floor approach/A input, both borrowing/return paths, nine/ten-seed boundaries, permanent purchases and re-entry in split/batched updates.");
    }
}
