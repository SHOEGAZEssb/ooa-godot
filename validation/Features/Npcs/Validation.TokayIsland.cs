using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayIslandWorldObjects()
    {
        var eyes = new TokayEntranceEyeDatabase();
        var enemies = new EnemyDatabase();
        FailIf(
            enemies.RoomObjectRecordCount != 1145 ||
            eyes.Eyes.Count != 2 ||
            enemies.GetRoomObjects(1, 0xba).Single(record =>
                record.Id == 0x62) is not
                { Kind: RoomObjectKind.FixedEnemy, SubId: 0x02, Order: 0 } ||
            enemies.EnemyHandlers.ResolveHandler(
                enemies.GetRoomObjects(1, 0xba).Single(record =>
                    record.Id == 0x62)).Handler != EnemyHandlerKind.VineSprout,
            "Direct mainData ENEMY_VINE_SPROUT or Tokay entrance-eye imports changed.");

        _saveData.SetRoomFlag(1, 0xba, OracleSaveData.RoomFlag80, value: false);
        for (int subId = 0; subId < 6; subId++)
            _saveData.WriteWramByte(VineSproutDatabase.PositionAddress + subId, 0);
        _inventory.LoseTreasure(0x4f);
        LoadValidationRoom(1, 0xba);
        FailIf(
            _entities.Entities<TokayEntranceEyeRoomEntity>() is not
                [{ Record.SubId: 0x05 }] ||
            _entities.Entities<TokayEyeballSlotRoomEntity>().Count != 1,
            "Room 1:ba did not create only the unconditional `$80:$05 eye " +
            "and the invisible `$c4:$04 socket while room flag `$80 was clear.");

        Vector2 defaultVinePosition = new(0x68, 0x18);
        byte originalVineTile =
            _rooms.CurrentRoom.GetMetatile(defaultVinePosition);
        ulong originalVineGroundHash = OracleGraphicsCache.PixelHash(
            _rooms.CurrentRoom.BuildMimickedMetatileTexture(
                defaultVinePosition).GetImage());
        ulong logicalTile00Hash = OracleGraphicsCache.PixelHash(
            _rooms.CurrentRoom.BuildMimickedMetatileTexture(0x00).GetImage());
        _entities.Update(1.0 / 60.0, _player);
        VineSproutRoomEntity vine =
            _entities.Entities<VineSproutRoomEntity>().Single();
        Vector2 sourcePosition = vine.Position;
        FailIf(
            sourcePosition != new Vector2(0x68, 0x18) ||
            _rooms.CurrentRoom.GetMetatile(sourcePosition) != 0x00 ||
            _rooms.CurrentRoom.GetTerrainInfo(sourcePosition).Collision != 0x0f ||
            vine.PersistedPosition != 0x16 || originalVineTile == 0x00 ||
            originalVineGroundHash == logicalTile00Hash ||
            OracleGraphicsCache.PixelHash(
                _rooms.CurrentRoom.BuildMimickedMetatileTexture(
                    sourcePosition).GetImage()) != originalVineGroundHash,
            "Room 1:ba vine did not resolve default packed position `$16 and " +
            "own logical `$00/`$0f state while preserving its normal rendered " +
            "ground metatile on state 1.");

        Vector2I[] directions =
        [Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left];
        Vector2I pushDirection = directions.First(direction =>
            _rooms.CurrentRoom.GetTerrainInfo(
                sourcePosition + (Vector2)direction *
                    OracleRoomData.MetatileSize).Collision == 0);
        Vector2 destination = sourcePosition + (Vector2)pushDirection * 16.0f;
        ulong destinationGroundHash = OracleGraphicsCache.PixelHash(
            _rooms.CurrentRoom.BuildMimickedMetatileTexture(
                destination).GetImage());
        Vector2 resolvedLinkPosition = sourcePosition -
            (Vector2)pushDirection * OracleRoomData.MetatileSize;
        for (int update = 0; update < OracleRoomData.MetatileSize; update++)
        {
            Vector2 movement = _collision.ResolveMovement(
                resolvedLinkPosition, pushDirection, allowWallSlide: false);
            if (movement == Vector2.Zero)
                break;
            resolvedLinkPosition += movement;
        }
        _player.WarpTo(resolvedLinkPosition, recordSafe: false);
        _player.Face(pushDirection);
        _player.UpdatePushingState(pushDirection);
        FailIf(
            !_player.IsPushing,
            "ENEMY_VINE_SPROUT entity collision stopped Link before its `$0f " +
            "resting-tile collision could select the pushing animation.");

        Vector2 linkPosition = sourcePosition - (Vector2)pushDirection * 12.0f;
        for (int update = 0; update < 19; update++)
            vine.UpdatePushAttempt(linkPosition, pushDirection, pushDirection);
        FailIf(
            vine.Moving || vine.PushCounter != 1,
            "ENEMY_VINE_SPROUT moved before 20 continuous valid push updates.");
        _sound.ClearPlayRequestAudit();
        vine.UpdatePushAttempt(linkPosition, pushDirection, pushDirection);
        FailIf(
            !vine.Moving || vine.MoveCounter != 22 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 1 ||
            _rooms.CurrentRoom.GetTerrainInfo(sourcePosition).Collision != 0,
            "ENEMY_VINE_SPROUT did not begin its `$16-update SPEED_c0 move " +
            "and restore the source collision on push update 20.");
        for (int update = 0; update < 21; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(
            !vine.Moving || vine.MoveCounter != 1,
            "ENEMY_VINE_SPROUT completed before its `$16 movement updates.");
        _entities.Update(1.0 / 60.0, _player);
        int expectedPacked = _rooms.CurrentRoom.GetPackedPosition(destination);
        FailIf(
            vine.Moving || vine.Position != destination ||
            vine.PersistedPosition != expectedPacked ||
            _rooms.CurrentRoom.GetMetatile(destination) != 0x00 ||
            _rooms.CurrentRoom.GetTerrainInfo(destination).Collision != 0x0f ||
            OracleGraphicsCache.PixelHash(
                _rooms.CurrentRoom.BuildMimickedMetatileTexture(
                    destination).GetImage()) != destinationGroundHash,
            "ENEMY_VINE_SPROUT did not center, occupy, and persist its " +
            "destination with its normal rendered ground after exactly `$16 " +
            "updates.");

        LoadValidationRoom(1, 0xba);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _entities.Entities<VineSproutRoomEntity>().Single().Position != destination,
            "Room 1:ba did not reload ENEMY_VINE_SPROUT from wVinePositions.");

        _inventory.GiveTreasure(0x4f, 0);
        TokayEyeballSlotRoomEntity slot =
            _entities.Entities<TokayEyeballSlotRoomEntity>().Single();
        Vector2 socketLinkPosition = slot.Position + Vector2.Down * 12.0f;
        for (int update = 0; update < 9; update++)
            slot.UpdatePushAttempt(socketLinkPosition, Vector2I.Up, Vector2I.Up);
        FailIf(
            slot.State != TokayEyeballSlotState.Waiting || slot.PushCounter != 1,
            "Tokay Eyeball socket activated before ten upward push updates.");
        slot.UpdatePushAttempt(socketLinkPosition, Vector2I.Up, Vector2I.Up);
        FailIf(slot.State != TokayEyeballSlotState.BeginInsert,
            "Tokay Eyeball socket rejected the tenth upward push with treasure `$4f.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            slot.State != TokayEyeballSlotState.EyeWait || slot.Counter != 60 ||
            !_saveData.HasRoomFlag(1, 0xba, OracleSaveData.RoomFlag80) ||
            _entities.Entities<TokayEntranceEyeRoomEntity>().Count != 2 ||
            !_entities.PlayerMovementDisabled || !_entities.PlayerMenusDisabled,
            "Tokay Eyeball insertion did not set room flag `$80, spawn `$80:$06, " +
            "and lock Link on its first script update.");
        for (int update = 0; update < 60; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(
            slot.State != TokayEyeballSlotState.ShakeWait ||
            slot.Counter != 120 || _entities.ScreenShakeCounter != 159,
            "Tokay Eyeball insertion did not start its 160-update shake after wait 60.");
        for (int update = 0; update < 120; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(
            slot.State != TokayEyeballSlotState.OpenWait || slot.Counter != 60 ||
            _rooms.CurrentRoom.GetMetatile(new Vector2(0x48, 0x58)) != 0xa2 ||
            _rooms.CurrentRoom.GetMetatile(new Vector2(0x58, 0x58)) != 0xef ||
            _rooms.CurrentRoom.GetMetatile(new Vector2(0x68, 0x58)) != 0xa4,
            "pirate_openEyeballCave did not write `$a2/`$ef/`$a4 after wait 120.");
        for (int update = 0; update < 60; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _inventory.HasTreasure(0x4f) ||
            _entities.Entities<TokayEyeballSlotRoomEntity>().Count != 0 ||
            _entities.PlayerMovementDisabled || _entities.PlayerMenusDisabled,
            "Tokay Eyeball insertion did not consume treasure `$4f and release " +
            "the socket's input/menu restriction after the final wait 60.");

        LoadValidationRoom(1, 0xba);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _entities.Entities<TokayEntranceEyeRoomEntity>().Count != 2 ||
            _entities.Entities<TokayEyeballSlotRoomEntity>().Count != 0 ||
            _rooms.CurrentRoom.GetMetatile(new Vector2(0x58, 0x58)) != 0xef,
            "Room 1:ba re-entry did not retain both eyes, suppress the socket, " +
            "and apply the room-flag `$80 entrance tile changes.");

        GD.Print(
            "Validated five direct ENEMY_VINE_SPROUT placements, default/live " +
            "position persistence, preserved normal ground, Link push animation, " +
            "20+$16 push boundaries, both room 1:ba entrance eyes, and the " +
            "independent `$c4:$04 insertion sequence.");
    }

    private void ValidateTokayIslandInteractions()
    {
        var database = new TokayIslandDatabase();
        var npcDatabase = new NpcDatabase();
        IReadOnlyList<NpcRecord> islandRecords = npcDatabase.AllRecords
            .Where(record =>
                (record.Id == 0x48 && record.SubId >= 0x05) ||
                record is { Group: 1, Room: 0xcb, Id: 0x68, SubId: 0x00 })
            .ToArray();
        FailIf(
            islandRecords.Count != 27 ||
            islandRecords.Count(record => record.Implementation ==
                NpcImplementationClassification.OrdinaryGeneric) != 8 ||
            islandRecords.Count(record => record.Implementation ==
                NpcImplementationClassification.SpecializedNative) != 19 ||
            islandRecords.Any(record => record.Implementation ==
                NpcImplementationClassification.DeliberatelyUnsupported),
            "The 27 non-dungeon Tokay Island NPC rows did not classify as " +
            "eight ordinary and 19 specialized records.");

        NpcRecord[] room1adRecords = islandRecords.Where(record =>
            record is { Group: 1, Room: 0xad, Id: 0x48, SubId: 0x15 })
            .ToArray();
        LoadValidationRoom(1, 0xad);
        NpcCharacter[] room1adTokays = _entities.Entities<NpcCharacter>()
            .Where(npc => npc.Record is { Id: 0x48, SubId: 0x15 })
            .ToArray();
        FailIf(
            room1adRecords.Length != 1 ||
            room1adRecords[0].Y != 0x56 || room1adRecords[0].X != 0x68 ||
            room1adTokays.Length != 1 ||
            room1adTokays[0].Position != new Vector2(0x68, 0x56),
            "Room 1:ad did not select exactly the clean-US `$48:$15 Tokay " +
            "placement at `$56,$68.");

        FailIf(
            database.PastGameRoom != 0xde || database.PresentGameRoom != 0xe5 ||
            database.ShopRoom != 0xe4 || database.ShopPlacements.Count != 3 ||
            database.ShopItemCollisionRadius != 0x06 ||
            database.HeldItem(0x06).Treasure != TreasureDatabase.TreasureSword ||
            database.HeldItem(0x06).GrantParameter != 0x01 ||
            database.HeldItem(0x07).GrantParameter != 0x00 ||
            database.HeldItem(0x0a).Treasure != TreasureDatabase.TreasureSeedSatchel ||
            database.WildPattern(4, 0x0f).Pattern != 7,
            "Tokay Island source-derived holder, shop, or Wild Tokay tables changed.");
        var treasures = new TreasureDatabase();
        (string Object, int Treasure, int SubId, int ObjectParameter)[]
            shopRewards =
            [
                ("TREASURE_OBJECT_FEATHER_02", 0x17, 0x02, 0x01),
                ("TREASURE_OBJECT_BRACELET_03", 0x16, 0x03, 0x01),
                ("TREASURE_OBJECT_SHOVEL_02", 0x15, 0x02, 0x00),
                ("TREASURE_OBJECT_SHIELD_00", 0x01, 0x00, 0x01),
                ("TREASURE_OBJECT_SHIELD_01", 0x01, 0x01, 0x02),
                ("TREASURE_OBJECT_SHIELD_02", 0x01, 0x02, 0x03)
            ];
        FailIf(
            shopRewards.Any(expected =>
            {
                TreasureObjectRecord actual = treasures.GetObject(expected.Object);
                return actual.TreasureId != expected.Treasure ||
                    actual.SubId != expected.SubId ||
                    actual.Parameter != expected.ObjectParameter ||
                    TokayTradingEvent.ShopRewardObjectParameter(
                        expected.Treasure, expected.SubId) !=
                        expected.ObjectParameter;
            }),
            "tokayShopItemScript reward IDs, subids, or treasure-object " +
            "parameters changed.");
        foreach (int subId in Enumerable.Range(0x06, 5))
        {
            TokayHeldItemRecord item = database.HeldItem(subId);
            _ = new GroundTreasureGrantRequest(
                0, 0, 0, 0x40, 0x50, item.GrantObject,
                "validation:tokayGiveItemToLink")
            {
                ExpectedTreasureId = item.Treasure,
                ExpectedSubId = item.GrantSubId,
                ExpectedObjectParameter = item.GrantParameter
            }.Resolve(treasures);
        }

        TokayHoldingItemEvent holdingItem = _roomEvents.TokayHoldingItem;
        RosaShovelEvent rosaShovel = _roomEvents.RosaShovel;
        TokayTradingEvent trading = _roomEvents.TokayTrading;
        WildTokayGameEvent wildTokay = _roomEvents.WildTokayGame;

        // Returned-item predicates and the source held-accessory animation.
        _saveData.SetRoomFlag(5, 0xca, OracleSaveData.RoomFlag40, value: false);
        _inventory.LoseTreasure(TreasureDatabase.TreasureSword);
        LoadValidationRoom(5, 0xca);
        TokayHoldingItemCharacter swordHolder = _entities
            .Entities<NpcCharacter>()
            .OfType<TokayHoldingItemCharacter>()
            .Single(npc => npc.Record is { Id: 0x48, SubId: 0x06 });
        FailIf(
            swordHolder.CurrentScriptAnimationSource != database.Animation(0x06) ||
            !swordHolder.HeldItemVisible ||
            swordHolder.HeldItemPosition != swordHolder.Position + new Vector2(0, -12) ||
            swordHolder.HeldItemTextureSize == Vector2I.Zero ||
            swordHolder.HeldItemOpaquePixels == 0 ||
            !holdingItem.TryInteractNpc(swordHolder) ||
            holdingItem.Stage != TokayHoldingItemStage.Intro ||
            !_player.CutsceneControlled ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x0a0b)),
            "Room 5:ca did not initialize and enter the stolen-sword holder script.");
        _dialogue.Close();
        StepRoomEventFrames(31);
        FailIf(
            holdingItem.Stage != TokayHoldingItemStage.Reward ||
            swordHolder.HeldItemVisible ||
            swordHolder.CurrentScriptAnimationSource != database.Animation(0x02),
            "tokayHoldingItemScript did not remove its related accessory and " +
            "create the source-addressed sword treasure after 30 updates.");
        holdingItem.Cancel();
        _dialogue.Close();

        _saveData.SetRoomFlag(5, 0xca, OracleSaveData.RoomFlag40);
        LoadValidationRoom(5, 0xca);
        swordHolder = _entities.Entities<NpcCharacter>()
            .OfType<TokayHoldingItemCharacter>()
            .Single(npc => npc.Record is { Id: 0x48, SubId: 0x06 });
        FailIf(
            swordHolder.CurrentScriptAnimationSource != database.Animation(0x02) ||
            swordHolder.HeldItemVisible ||
            !holdingItem.TryInteractNpc(swordHolder) ||
            holdingItem.Stage != TokayHoldingItemStage.DialogueOnly,
            "Room bit $40 did not select the returned-item Tokay loop.");
        _dialogue.Close();
        StepRoomEventFrames(1);

        // The two source-ordered trading-hut items and their ordinary decline
        // route are driven through the same A-button player path as gameplay.
        _saveData.SetGlobalFlag(database.BoughtFeatherFlag, value: false);
        _saveData.SetGlobalFlag(database.BoughtBraceletFlag, value: false);
        _inventory.LoseTreasure(TreasureDatabase.TreasureFeather);
        _inventory.LoseTreasure(TreasureDatabase.TreasureBracelet);
        _inventory.LoseTreasure(TreasureDatabase.TreasureShovel);
        LoadValidationRoom(2, 0xe4);
        List<TokayShopItem> stock = _entities.Entities<TokayShopItem>();
        NpcCharacter shopkeeper = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x48, SubId: 0x0e });
        FailIf(
            stock.Count != 2 || stock[0].OriginalSubId != 0 || stock[0].SubId != 0 ||
            stock[0].Position != new Vector2(0x40, 0x40) ||
            stock[1].OriginalSubId != 1 || stock[1].SubId != 1 ||
            stock[1].Position != new Vector2(0x60, 0x40) ||
            stock.Any(item => item.CollisionRadius != 0x06) ||
            !_entities.BlocksLink(stock[0].Position + Vector2.Down * 11) ||
            _entities.BlocksLink(stock[0].Position + Vector2.Down * 12) ||
            _collision.ResolveMovement(
                stock[0].Position + Vector2.Down * 13,
                Vector2.Up * 2,
                allowWallSlide: false) != Vector2.Zero ||
            !trading.TryInteractNpc(shopkeeper) ||
            trading.Stage != TokayTradingStage.DialogueOnly,
            "Room 2:e4 did not instantiate source-ordered `$81 stock with " +
            "its `$06 collision radii and `$48:$0e shopkeeper.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        _player.WarpTo(stock[0].Position + Vector2.Down * 10, recordSafe: false);
        _player.Face(Vector2I.Up);
        bool selectedStock = trading.TryInteractPlayer(_player);
        FailIf(
            !selectedStock ||
            trading.Stage != TokayTradingStage.ShopPrompt ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x0a27)),
            "The feather stock did not offer its source bracelet return branch " +
            $"(selected={selectedStock}, stage={trading.Stage}, text='{_dialogue.CurrentMessage}').");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(1);
        FailIf(
            trading.Stage != TokayTradingStage.ShopResultText ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x0a29)),
            "The Tokay trading hut did not preserve the decline result.");
        _dialogue.Close();
        StepRoomEventFrames(1);

        // Returning an equipped Bracelet through the left Feather offer must
        // update the separate right-hand shovel stock. Every source `$81`
        // object rechecks the shared inventory on the next object update.
        _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
        _inventory.EquipB(InventoryState.ItemBracelet);
        LoadValidationRoom(2, 0xe4);
        stock = _entities.Entities<TokayShopItem>();
        TokayShopItem featherStock = stock.Single(item =>
            item.OriginalSubId == 0);
        TokayShopItem braceletStock = stock.Single(item =>
            item.OriginalSubId == 1);
        FailIf(
            featherStock.SubId != 0 || braceletStock.SubId != 3 ||
            braceletStock.Treasure != TreasureDatabase.TreasureShovel ||
            _inventory.EquippedB != InventoryState.ItemBracelet,
            "Room 2:e4 did not reproduce the equipped-Bracelet state with " +
            "left Feather `$81:$00` and right shovel `$81:$03` stock.");
        _player.WarpTo(
            featherStock.Position + Vector2.Down * 10, recordSafe: false);
        _player.Face(Vector2I.Up);
        selectedStock = trading.TryInteractPlayer(_player);
        FailIf(
            !selectedStock || trading.Stage != TokayTradingStage.ShopPrompt ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(database.Text(0x0a27)),
            "The left Feather stock did not offer the equipped Bracelet's " +
            "source return branch.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        FailIf(
            trading.Stage != TokayTradingStage.ShopReward ||
            !_inventory.HasTreasure(TreasureDatabase.TreasureShovel) ||
            _inventory.HasTreasure(TreasureDatabase.TreasureBracelet) ||
            _inventory.EquippedB != InventoryState.ItemShovel ||
            featherStock.SubId != 0 || braceletStock.SubId != 3,
            "tokayShopItem_giveShovelAndLoseBracelet did not preserve its " +
            "same-update equipped inventory and pre-transform stock boundary.");
        StepRoomEventFrames(1);
        TokayShopPlacementRecord braceletVisual = database.ShopVisual(1);
        FailIf(
            featherStock.SubId != 0 ||
            featherStock.Treasure != TreasureDatabase.TreasureFeather ||
            braceletStock.SubId != 1 ||
            braceletStock.Treasure != TreasureDatabase.TreasureBracelet ||
            braceletStock.Placement.TileBase != braceletVisual.TileBase ||
            braceletStock.Placement.Palette != braceletVisual.Palette ||
            !_dialogue.IsOpen,
            "The update after returning the equipped Bracelet through the " +
            "left Feather offer did not restore the separate right stock to " +
            "Bracelet `$81:$01` while shovel reward text remained active " +
            $"(subid=${braceletStock.SubId:x2}, treasure=" +
            $"${braceletStock.Treasure:x2}, tile=" +
            $"${braceletStock.Placement.TileBase:x2}/" +
            $"${braceletVisual.TileBase:x2}, palette=" +
            $"${braceletStock.Placement.Palette:x2}/" +
            $"${braceletVisual.Palette:x2}, text={_dialogue.IsOpen}).");
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(1);
        FailIf(
            trading.Stage != TokayTradingStage.Inactive ||
            _player.CutsceneControlled || _player.IsHoldingItemTwoHands,
            "The equipped Bracelet-for-shovel exchange did not finish and " +
            "release Link.");

        _inventory.LoseTreasure(TreasureDatabase.TreasureShovel);
        LoadValidationRoom(2, 0xe4);
        stock = _entities.Entities<TokayShopItem>();
        _player.WarpTo(
            stock[0].Position + Vector2.Down * 10, recordSafe: false);
        _player.Face(Vector2I.Up);
        _inventory.SetMysterySeedsFromScript(0x10);
        selectedStock = trading.TryInteractPlayer(_player);
        FailIf(
            !selectedStock || trading.Stage != TokayTradingStage.ShopPrompt ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(database.Text(0x0a2b)),
            "The feather stock did not offer its ten-Mystery-Seed purchase.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        GroundTreasurePickup featherReward =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            trading.Stage != TokayTradingStage.ShopReward ||
            featherReward.Record.TreasureObject !=
                "TREASURE_OBJECT_FEATHER_02" ||
            !featherReward.Held || !_player.IsHoldingItemTwoHands ||
            _inventory.MysterySeeds != 0 || _inventory.FeatherLevel != 1 ||
            !_saveData.HasGlobalFlag(database.BoughtFeatherFlag) ||
            !stock[0].Removed || !_dialogue.IsOpen ||
            _entities.BlocksLink(stock[0].Position) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                treasures.GetObject("TREASURE_OBJECT_FEATHER_02").Message),
            "Accepting the Tokay feather trade did not consume ten Mystery " +
            "Seeds and grant `$17:$02 with treasure-object parameter `$01.");
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(1);
        FailIf(
            trading.Stage != TokayTradingStage.Inactive ||
            _player.CutsceneControlled || _player.IsHoldingItemTwoHands ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "The accepted Tokay feather trade did not finish its reward and " +
            "release Link.");

        // Linked visibility swaps the shovel holder for Rosa in room 1:cb.
        _saveData.SetLinkedGame(false);
        LoadValidationRoom(1, 0xcb);
        FailIf(
            !_entities.Entities<NpcCharacter>().Single(npc =>
                npc.Record is { Id: 0x48, SubId: 0x07 }).Active ||
            _entities.Entities<NpcCharacter>().Single(npc =>
                npc.Record is { Id: 0x68, SubId: 0x00 }).Active,
            "Unlinked room 1:cb did not retain only the shovel Tokay.");
        _saveData.SetLinkedGame(true);
        LoadValidationRoom(1, 0xcb);
        NpcCharacter rosa = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x68, SubId: 0x00 });
        FailIf(
            _entities.Entities<NpcCharacter>().Single(npc =>
                npc.Record is { Id: 0x48, SubId: 0x07 }).Active ||
            !rosa.Active || !rosaShovel.TryInteractNpc(rosa) ||
            rosaShovel.Stage != RosaShovelStage.FirstText,
            "Linked room 1:cb did not replace the shovel Tokay with Rosa's shovel event.");
        rosaShovel.Cancel();
        _dialogue.Close();

        // The Wild Tokay manager must save both equipped bytes, force the
        // Bracelet, instantiate the falling meat, and restore equips on any
        // exit (including cancellation and room transitions).
        _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
        _inventory.AddRupees(20);
        _inventory.SetScriptedEquippedItems(
            TreasureDatabase.TreasureBombs,
            TreasureDatabase.TreasureSword);
        LoadValidationRoom(2, 0xde);
        NpcCharacter manager = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x48, SubId: 0x0d });
        FailIf(
            !wildTokay.TryInteractNpc(manager),
            "Wild Tokay manager rejected A-button input.");
        _dialogue.Close();
        StepRoomEventFrames(51);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.Close();
        StepRoomEventFrames(21);
        FailIf(
            _inventory.EquippedB != InventoryState.ItemNone ||
            _inventory.EquippedA != InventoryState.ItemBracelet ||
            wildTokay.Stage != WildTokayGameStage.Wait,
            "Wild Tokay did not save equips and force Bracelet on its source start boundary.");
        StepRoomEventFrames(database.GameStartDelay);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.StartText ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x0a16)),
            "Wild Tokay did not reach TX_0a16 after its 30-update start delay.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.Playing ||
            _entities.Entities<WildTokayMeat>().Count != 1,
            "Wild Tokay did not create INTERAC_TOKAY_MEAT at game start.");
        wildTokay.Cancel();
        FailIf(
            _inventory.EquippedB != TreasureDatabase.TreasureBombs ||
            _inventory.EquippedA != TreasureDatabase.TreasureSword ||
            _player.CutsceneControlled,
            "Cancelling Wild Tokay did not restore Link's saved equipped bytes and control.");

        int maxBombsBefore = _inventory.MaxBombs;
        _inventory.ApplyTokayBombCapacityUpgrade();
        int expectedBombCapacity = (maxBombsBefore + 0x20) & 0xff;
        FailIf(
            _inventory.MaxBombs != expectedBombCapacity ||
            _inventory.Bombs != expectedBombCapacity,
            "tokayGiveBombUpgrade did not add packed-BCD $20 and refill bombs.");

        GD.Print(
            "Validated all 27 non-dungeon Tokay Island NPC records, clean-US " +
            "room 1:ad placement, held accessories, " +
            "source-addressed item grants, holder/re-entry state, trading-hut " +
            "stock collision, decline, accepted Feather grant, and live " +
            "cross-stock Bracelet-for-shovel refresh, linked " +
            "Rosa visibility, imported Wild Tokay tables, forced Bracelet/meat " +
            "start, and equip restoration.");
    }
}
