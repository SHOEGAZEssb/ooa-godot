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
        static Vector2 WildTilePoint(int packedPosition) => new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
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
            database.GameReturnPosition != 0x57 ||
            database.ShopItemCollisionRadius != 0x06 ||
            database.HeldItem(0x06).Treasure != TreasureDatabase.TreasureSword ||
            database.HeldItem(0x06).GrantParameter != 0x01 ||
            database.HeldItem(0x07).GrantParameter != 0x00 ||
            database.HeldItem(0x0a).Treasure != TreasureDatabase.TreasureSeedSatchel ||
            database.WildPattern(4, 0x0f).Pattern != 7 ||
            database.WildStartTiles.Select(record =>
                (record.Tile, record.PackedPosition)).ToArray() is not
                [(0xef, 0x01), (0xef, 0x08), (0xef, 0x71),
                 (0xef, 0x78), (0x7a, 0x74), (0x7a, 0x75)] ||
            database.WildMeatAccessory(0) is not
                { YOffset: 0, XOffset: -13, Animation: 3 } ||
            database.WildMeatAccessory(3) is not
                { YOffset: -12, XOffset: -1, Animation: 3 } ||
            database.WildMeatAccessory(4) is not
                { YOffset: -12, XOffset: 0, Animation: 3 },
            "Tokay Island source-derived holder, shop, or Wild Tokay tables changed.");

        int[][] sourceWildPatterns =
        [
            [1, 0, 0, 2], [2, 0, 1, 0], [2, 1, 0, 1], [1, 0, 2, 2],
            [1, 1, 2, 2], [2, 2, 1, 1], [2, 3, 1, 0], [1, 3, 2, 2]
        ];
        int[][] sourceWildSelections =
        [
            [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5],
            [0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 6],
            [0, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5, 5, 6, 6, 6, 7],
            [1, 2, 3, 3, 4, 4, 4, 5, 5, 5, 5, 0, 0, 6, 7, 7],
            [3, 4, 4, 4, 5, 5, 5, 5, 5, 2, 1, 0, 6, 7, 7, 7]
        ];
        int[] sourceWildCycleCounts = [5, 5, 5, 6, 7];
        for (int level = 0; level < sourceWildSelections.Length; level++)
        {
            FailIf(
                database.WildCycleCount(level) != sourceWildCycleCounts[level],
                $"Wild Tokay level {level} cycle count changed from source " +
                $"value {sourceWildCycleCounts[level]}.");
            for (int randomIndex = 0; randomIndex < 16; randomIndex++)
            {
                int expectedPattern = sourceWildSelections[level][randomIndex];
                int[] expectedCodes = sourceWildPatterns[expectedPattern];
                WildTokayPatternRecord imported =
                    database.WildPattern(level, randomIndex);
                int[] importedCodes =
                [
                    imported.LeftBlue, imported.LeftRed,
                    imported.RightBlue, imported.RightRed
                ];
                FailIf(
                    imported.Pattern != expectedPattern ||
                    !importedCodes.SequenceEqual(expectedCodes),
                    $"Wild Tokay level {level}, random nibble " +
                    $"${randomIndex:x1} did not preserve source pattern " +
                    $"{expectedPattern}: {string.Join(",", expectedCodes)}.");

                int randomCalls = 0;
                var schedule = new WildTokaySpawnSchedule(
                    database,
                    () =>
                    {
                        randomCalls++;
                        return randomIndex;
                    });
                schedule.Begin(level);
                int emittedTokays = 0;
                int lastOccupiedSlot = Array.FindLastIndex(
                    expectedCodes, code => code != 0);
                for (int cycle = 0; cycle < sourceWildCycleCounts[level]; cycle++)
                {
                    for (int slot = 0; slot < 4; slot++)
                    {
                        WildTokaySpawnInstruction instruction = schedule.Advance();
                        bool expectedFinal =
                            cycle == sourceWildCycleCounts[level] - 1 &&
                            slot == lastOccupiedSlot;
                        FailIf(
                            instruction.Reset ||
                            instruction.Code != expectedCodes[slot] ||
                            instruction.Final != expectedFinal,
                            $"Wild Tokay level {level}, random nibble " +
                            $"${randomIndex:x1}, cycle {cycle}, slot {slot} " +
                            "did not preserve its source code/final marker.");
                        emittedTokays += instruction.Code == 3
                            ? 2
                            : instruction.Code == 0 ? 0 : 1;
                    }
                    WildTokaySpawnInstruction reset = schedule.Advance();
                    FailIf(
                        !reset.Reset || reset.Code != 0 || reset.Final ||
                        schedule.Slot != 0 ||
                        schedule.CyclesRemaining !=
                            sourceWildCycleCounts[level] - cycle - 1,
                        $"Wild Tokay level {level}, random nibble " +
                        $"${randomIndex:x1}, cycle {cycle} skipped its source " +
                        "fifth 60-update reset slot.");
                }
                int tokaysPerPattern = expectedCodes.Sum(code =>
                    code == 3 ? 2 : code == 0 ? 0 : 1);
                FailIf(
                    emittedTokays !=
                        tokaysPerPattern * sourceWildCycleCounts[level] ||
                    randomCalls != sourceWildCycleCounts[level] + 1 ||
                    schedule.Advance() != default,
                    $"Wild Tokay level {level}, random nibble " +
                    $"${randomIndex:x1} changed its total participant count " +
                    "or initial/per-reset/final RNG consumption.");
            }
        }
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
        Vector2 wildTokayResultPosition =
            new(database.GameLinkX, database.GameLinkY);
        int wildTokayRoomMusic = _sound.Data.RoomMusic(2, 0xde);
        FailIf(
            wildTokayRoomMusic != 0x26,
            $"Wild Tokay room 2:de music changed from source value `$26 " +
            $"(actual=${wildTokayRoomMusic:x2}).");
        Dictionary<int, byte> originalWildTiles = database.WildStartTiles
            .ToDictionary(
                record => record.PackedPosition,
                record => _rooms.CurrentRoom.GetMetatile(
                    WildTilePoint(record.PackedPosition)));
        Vector2 originalFadePosition = _warpFade.Position;
        Vector2 originalFadeSize = _warpFade.Size;
        int originalFadeZ = _warpFade.ZIndex;
        Color originalFadeColor = _warpFade.Color;
        NpcCharacter manager = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x48, SubId: 0x0d });
        FailIf(
            !wildTokay.TryInteractNpc(manager),
            "Wild Tokay manager rejected A-button input.");
        _dialogue.Close();
        StepRoomEventFrames(10);
        FailIf(
            _entities.Entities<NpcCharacter>().Any(npc =>
                npc.Record is { Id: 0x63 }) ||
            wildTokay.Stage != WildTokayGameStage.Wait,
            "Wild Tokay raised its prize before the source 10-update wait.");
        StepRoomEventFrames(1);
        WildTokayPrizeRecord firstPrize = database.WildPrize(0);
        NpcCharacter prizeAccessory = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x63 } && npc.Active);
        FailIf(
            prizeAccessory.Record.SubId != firstPrize.AccessorySubId ||
            prizeAccessory.Position != manager.Position + new Vector2(0, -12) ||
            prizeAccessory.CurrentScriptAnimationSource != firstPrize.Animation ||
            prizeAccessory.CurrentAnimationTextureSize == Vector2I.Zero ||
            prizeAccessory.CurrentAnimationOpaquePixels == 0 ||
            manager.CurrentScriptAnimationSource != database.Animation(0x06),
            "tokayGame_createAccessoryForPrize did not raise the imported " +
            "level-0 `$63:$3e prize above `$48:$0d with animation `$06.");
        StepRoomEventFrames(39);
        FailIf(
            !prizeAccessory.Active || wildTokay.Stage != WildTokayGameStage.Wait,
            "Wild Tokay did not hold the prize through the source 40-update wait.");
        StepRoomEventFrames(1);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        FailIf(
            !prizeAccessory.Active || wildTokay.Stage != WildTokayGameStage.Wait ||
            wildTokay.Counter != 20,
            "Wild Tokay lowered the prize before the accepted-play 20-update wait.");
        StepRoomEventFrames(20);
        FailIf(
            prizeAccessory.Active ||
            manager.CurrentScriptAnimationSource != database.Animation(0x02) ||
            wildTokay.Stage != WildTokayGameStage.PastManagerRulesPrompt ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x0a14)),
            "Wild Tokay did not lower the prize with animation `$02 before TX_0a14.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.Close();
        StepRoomEventFrames(21);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.FadeOut ||
            !manager.Active || !Mathf.IsZeroApprox(_warpFade.Color.A) ||
            _warpFade.Position != Vector2.Zero ||
            _warpFade.Size != new Vector2(
                OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight) ||
            _warpFade.ZIndex != _hud.ZIndex + 1 ||
            _sound.PlayRequestsFor(
                OracleSoundEngine.SndCtrlMediumFadeOut) != 1,
            "Wild Tokay did not start the manager-owned full-screen white " +
            "fade and medium music fade after its 20-update intro wait.");
        StepRoomEventFrames(31);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.FadeOut ||
            !manager.Active || !Mathf.IsEqualApprox(_warpFade.Color.A, 1.0f),
            "Wild Tokay did not reach full white on fadeout update 31 while " +
            "keeping `$48:$0d alive through the palette thread.");
        StepRoomEventFrames(1);
        FailIf(
            _inventory.EquippedB != InventoryState.ItemNone ||
            _inventory.EquippedA != InventoryState.ItemBracelet ||
            wildTokay.Stage != WildTokayGameStage.Wait || manager.Active ||
            database.WildStartTiles.Any(record =>
                _rooms.CurrentRoom.GetMetatile(
                    WildTilePoint(record.PackedPosition)) != record.Tile),
            "Wild Tokay did not delete `$48:$0d, save equips, and force " +
            "Bracelet or apply its four `$ef door and two `$7a floor writes " +
            "on fadeout update 32.");
        StepRoomEventFrames(database.GameStartDelay);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.FadeIn ||
            !Mathf.IsEqualApprox(_warpFade.Color.A, 1.0f) ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusMinigame) != 1,
            "Wild Tokay did not hold white for 30 updates before starting " +
            "MUS_MINIGAME and fadeinFromWhite.");
        StepRoomEventFrames(31);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.FadeIn ||
            !Mathf.IsZeroApprox(_warpFade.Color.A),
            "Wild Tokay fadeinFromWhite did not reach palette offset `$00 " +
            "on update 31.");
        StepRoomEventFrames(1);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.Wait ||
            wildTokay.Counter != database.GameFadeInDelay ||
            _warpFade.Position != originalFadePosition ||
            _warpFade.Size != originalFadeSize ||
            _warpFade.ZIndex != originalFadeZ ||
            _warpFade.Color != originalFadeColor,
            "Wild Tokay did not release the shared fade presentation after " +
            "fadein update 32 and begin its 10-update text delay.");
        StepRoomEventFrames(database.GameFadeInDelay);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.StartText ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x0a16)),
            "Wild Tokay did not reach TX_0a16 after its post-fade 10-update delay.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.Playing ||
            _entities.Entities<WildTokayMeat>().Count != 1 || manager.Active ||
            !wildTokay.ScreenTransitionsDisabled ||
            !_roomEvents.ScreenTransitionsDisabled,
            "Wild Tokay did not keep `$48:$0d deleted and create " +
            "INTERAC_TOKAY_MEAT with the source screen-transition lock at game start.");
        (Vector2 Attempt, Vector2 Expected)[] wildBoundaries =
        [
            (new Vector2(4.5f, 64.0f), new Vector2(6.5f, 64.0f)),
            (new Vector2(155.5f, 64.0f), new Vector2(154.5f, 64.0f)),
            (new Vector2(80.0f, 4.25f), new Vector2(80.0f, 6.25f)),
            (new Vector2(80.0f, 122.75f), new Vector2(80.0f, 121.75f))
        ];
        foreach ((Vector2 attempt, Vector2 expected) in wildBoundaries)
        {
            _player.WarpTo(attempt, recordSafe: false);
            _playerWorld.CheckRoomExit(_player);
            FailIf(
                _transitions.IsTransitioning ||
                _rooms.ActiveGroup != 2 || _rooms.CurrentRoom.Id != 0xde ||
                _player.PrecisePosition != expected,
                "Wild Tokay allowed Link through a screen boundary while " +
                $"substate 4 was active: attempted {attempt}, got " +
                $"{_player.PrecisePosition}.");
        }
        WildTokayMeat thrownMeat = _entities.Entities<WildTokayMeat>().Single();
        StepRoomEventFrames(database.GameSpawnDelay - 1);
        FailIf(
            thrownMeat.ZFixed >= 0 ||
            _entities.Entities<NpcCharacter>().Any(npc =>
                npc.Active && npc.Record is { Id: 0x48, SubId: 0x0c }),
            "Wild Tokay meat or participant crossed the source 60-update " +
            "initial fall/spawn boundary one update early.");
        StepRoomEventFrames(1);
        NpcCharacter participant = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x48, SubId: 0x0c } && npc.Active);
        FailIf(
            thrownMeat.ZFixed >= 0 || participant.Position.Y != -8 ||
            participant.Position.X is not (24 or 136) ||
            participant.CurrentScriptAnimationSource !=
                database.Animation(database.ParticipantAnimation),
            "Wild Tokay did not spawn participant `$48:$0c at source update " +
            "60, Y `$f8, facing down while the exact meat fall remained airborne.");
        StepRoomEventFrames(1);
        FailIf(
            thrownMeat.ZFixed != 0 || participant.Position.Y != -7.5f,
            "Wild Tokay meat did not land one update after the first " +
            "participant spawned.");
        _player.WarpTo(thrownMeat.Position + Vector2.Down * 6, recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !_playerWorld.TryUseBracelet(_player, primaryButton: true) ||
            _bracelet.State != BraceletState.LiftingEntity ||
            !thrownMeat.Lifted || !_player.IsCarryingObject,
            "Landed INTERAC_TOKAY_MEAT did not enter Link's shared Bracelet " +
            "entity-lift and carried-object state.");
        StepRoomEventFrames(1);
        FailIf(
            _entities.Entities<WildTokayMeat>().Count != 2 ||
            participant.Position.Y != -7.0f,
            "INTERAC_TOKAY_MEAT state 2 did not create its replacement on " +
            "the update immediately after Link grabbed it, while the " +
            "participant moved down at SPEED_80.");
        var braceletData = new BraceletDatabase().Data;
        int entityLiftUpdates = braceletData.LiftLowFrames +
            braceletData.LiftMidFrames + braceletData.LiftHighFrames;
        for (int update = 0; update < entityLiftUpdates; update++)
        {
            _playerWorld.UpdateBracelet(
                _player,
                Vector2.Zero,
                primaryHeld: false,
                secondaryHeld: false,
                itemButtonJustPressed: false);
        }
        Vector2 heldMeatPosition = thrownMeat.Position;
        int heldMeatZ = thrownMeat.ZFixed;
        FailIf(
            _bracelet.State != BraceletState.Idle ||
            !_player.IsCarryingObject || !thrownMeat.Lifted ||
            !_playerWorld.UpdateBracelet(
                _player,
                Vector2.Left,
                primaryHeld: false,
                secondaryHeld: false,
                itemButtonJustPressed: true) ||
            !thrownMeat.Thrown || thrownMeat.Lifted ||
            _player.IsCarryingObject,
            "Wild Tokay meat did not release through Bracelet state 3 when " +
            "the equipped item button was pressed.");
        int releasedMeatZ = thrownMeat.ZFixed;
        StepRoomEventFrames(1);
        FailIf(
            thrownMeat.Position != heldMeatPosition + Vector2.Left * 3 ||
            thrownMeat.ThrowDirection != Vector2I.Left ||
            thrownMeat.ThrowSpeedRaw != braceletData.SpeedRaw ||
            thrownMeat.ZFixed != releasedMeatZ + braceletData.InitialSpeedZ ||
            thrownMeat.SpeedZ !=
                braceletData.InitialSpeedZ + braceletData.Gravity,
            "Thrown Wild Tokay meat did not apply the source facing offset, " +
            "SPEED_`$3c lateral step, -`$f0 Z speed, and `$1c gravity " +
            $"(position={thrownMeat.Position}, held={heldMeatPosition}, " +
            $"direction={thrownMeat.ThrowDirection}, speed=" +
            $"${thrownMeat.ThrowSpeedRaw:x2}, z={thrownMeat.ZFixed}, " +
            $"held-z={heldMeatZ}, released-z={releasedMeatZ}, " +
            $"speed-z={thrownMeat.SpeedZ}).");
        FailIf(
            participant.Position.Y != -6.5f,
            "Wild Tokay participant `$48:$0c did not move down from source " +
            $"Y `$f8 at SPEED_80 (actual Y={participant.Position.Y}).");
        StepRoomEventFrames(284);
        FailIf(
            !participant.Active || participant.Position.Y != 135.5f ||
            wildTokay.Stage != WildTokayGameStage.Playing,
            "Wild Tokay participant `$48:$0c left before the source " +
            "(yh + `$08) = `$90 boundary.");
        StepRoomEventFrames(1);
        FailIf(
            participant.Active || participant.Position.Y != 136 ||
            wildTokay.Stage != WildTokayGameStage.Wait,
            "Wild Tokay participant `$48:$0c did not leave downward and fail " +
            "the round at source Y `$88.");
        FailIf(
            !thrownMeat.Finished,
            "The first thrown meat did not expire before the losing return boundary.");
        // Real gameplay yields to the scene tree between these updates, so
        // IRoomEntityLifetime has disposed this already-finished meat before
        // FinishGame clears the event's retained source-order list. Force the
        // same lifetime boundary inside this synchronous validation method.
        thrownMeat.Finish();
        if (GodotObject.IsInstanceValid(thrownMeat))
            thrownMeat.Free();
        FailIf(
            GodotObject.IsInstanceValid(thrownMeat),
            "The disposed-meat cleanup probe remained native-instance valid.");
        _player.WarpTo(manager.Position, recordSafe: false);
        int losingRoomMusicRequests =
            _sound.PlayRequestsFor(wildTokayRoomMusic);
        StepRoomEventFrames(30);
        _dialogue.Close();
        StepRoomEventFrames(21);
        FailIf(
            manager.Active ||
            wildTokay.Stage != WildTokayGameStage.ReturnFadeOut ||
            _inventory.EquippedB != TreasureDatabase.TreasureBombs ||
            _inventory.EquippedA != TreasureDatabase.TreasureSword ||
            !_saveData.HasRoomFlag(2, 0xde, OracleSaveData.RoomFlag40) ||
            !Mathf.IsZeroApprox(_warpFade.Color.A) ||
            _warpFade.Position != Vector2.Zero ||
            _warpFade.Size != new Vector2(
                OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight) ||
            _warpFade.ZIndex != _hud.ZIndex + 1,
            "Wild Tokay loss did not restore Link's equipment, set " +
            "ROOMFLAG_40, and begin the source same-room white fade after " +
            "the result text and 20-update delay.");
        StepRoomEventFrames(31);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.ReturnFadeOut ||
            !Mathf.IsEqualApprox(_warpFade.Color.A, 1.0f) || manager.Active ||
            _sound.ActiveMusic != OracleSoundEngine.MusMinigame,
            "Wild Tokay loss did not reach full white before recreating the " +
            "manager or replacing MUS_MINIGAME.");
        StepRoomEventFrames(1);
        FailIf(
            !manager.Active ||
            wildTokay.Stage != WildTokayGameStage.ReturnFadeIn ||
            originalWildTiles.Any(entry =>
                _rooms.CurrentRoom.GetMetatile(
                    WildTilePoint(entry.Key)) != entry.Value) ||
            _saveData.HasRoomFlag(2, 0xde, OracleSaveData.RoomFlag40) ||
            _sound.ActiveMusic != wildTokayRoomMusic ||
            _sound.PlayRequestsFor(wildTokayRoomMusic) !=
                losingRoomMusicRequests + 1 ||
            _player.PrecisePosition != wildTokayResultPosition ||
            _player.FacingVector != Vector2I.Up ||
            wildTokay.ScreenTransitionsDisabled ||
            _roomEvents.ScreenTransitionsDisabled,
            "Wild Tokay loss did not recreate `$48:$0d, restore all six " +
            "arena tiles, apply the manager's `$48,$50 Link override facing " +
            "up, clear " +
            "ROOMFLAG_40, restart room music `$26, and release the " +
            "screen-transition lock at the white warp boundary.");
        StepRoomEventFrames(31);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.ReturnFadeIn ||
            !Mathf.IsZeroApprox(_warpFade.Color.A),
            "Wild Tokay losing return fade did not reach palette offset `$00 " +
            "on fade-in update 31.");
        StepRoomEventFrames(1);
        FailIf(
            !manager.Active || wildTokay.Stage != WildTokayGameStage.LossPrompt ||
            !_dialogue.IsOpen ||
            _warpFade.Position != originalFadePosition ||
            _warpFade.Size != originalFadeSize ||
            _warpFade.ZIndex != originalFadeZ ||
            _warpFade.Color != originalFadeColor,
            "Wild Tokay did not release the shared fade presentation and " +
            "open its losing replay prompt after return fade-in update 32.");
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.Close();
        StepRoomEventFrames(21);
        FailIf(
            !manager.Active || wildTokay.Stage != WildTokayGameStage.FadeOut ||
            _inventory.EquippedB != TreasureDatabase.TreasureBombs ||
            _inventory.EquippedA != TreasureDatabase.TreasureSword,
            "Wild Tokay retry did not preserve the manager and restored equips " +
            "while entering its start fade.");
        StepRoomEventFrames((int)RoomTransitionController.WarpFadeFrames);
        FailIf(
            manager.Active || wildTokay.Stage != WildTokayGameStage.Wait ||
            _inventory.EquippedB != InventoryState.ItemNone ||
            _inventory.EquippedA != InventoryState.ItemBracelet ||
            database.WildStartTiles.Any(record =>
                _rooms.CurrentRoom.GetMetatile(
                    WildTilePoint(record.PackedPosition)) != record.Tile),
            "Wild Tokay retry did not re-enter the hidden-manager, " +
            "forced-Bracelet, open-arena state after fadeout update 32.");
        StepRoomEventFrames(database.GameStartDelay);
        StepRoomEventFrames((int)RoomTransitionController.WarpFadeFrames);
        StepRoomEventFrames(database.GameFadeInDelay);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.StartText ||
            !_dialogue.IsOpen,
            "Wild Tokay retry did not reach its post-fade start text.");
        _dialogue.Close();
        StepRoomEventFrames(1);

        WildTokayMeat catchMeat = _entities.Entities<WildTokayMeat>()
            .Single(meat => !meat.Finished);
        StepRoomEventFrames(database.GameSpawnDelay);
        StepRoomEventFrames(1);
        _player.WarpTo(catchMeat.Position + Vector2.Down * 6, recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !_playerWorld.TryUseBracelet(_player, primaryButton: true),
            "Wild Tokay retry meat could not be lifted for the catch path.");
        StepRoomEventFrames(1);
        for (int update = 0; update < entityLiftUpdates; update++)
        {
            _playerWorld.UpdateBracelet(
                _player,
                Vector2.Zero,
                primaryHeld: false,
                secondaryHeld: false,
                itemButtonJustPressed: false);
        }
        NpcCharacter catchingParticipant = _entities.Entities<NpcCharacter>()
            .Single(npc =>
                npc.Record is { Id: 0x48, SubId: 0x0c } && npc.Active);
        while (catchingParticipant.Position.Y < 45)
            StepRoomEventFrames(1);
        Vector2I catchThrowDirection =
            catchingParticipant.Position.X < _player.Position.X
                ? Vector2I.Left
                : Vector2I.Right;
        _player.Face(catchThrowDirection);
        FailIf(
            !_playerWorld.UpdateBracelet(
                _player,
                catchThrowDirection,
                primaryHeld: false,
                secondaryHeld: false,
                itemButtonJustPressed: true) ||
            catchMeat.ThrowDirection != catchThrowDirection ||
            catchMeat.ThrowSpeedRaw != braceletData.SpeedRaw,
            "Wild Tokay retry meat did not begin its horizontal source throw " +
            "from Link's center position toward the runner lane " +
            $"(bracelet={_bracelet.State}, carrying={_player.IsCarryingObject}, " +
            $"lifted={catchMeat.Lifted}, thrown={catchMeat.Thrown}, " +
            $"direction={catchMeat.ThrowDirection}, speed=" +
            $"${catchMeat.ThrowSpeedRaw:x2}).");

        for (int update = 0; update < 40 && catchMeat.BounceCount == 0; update++)
            StepRoomEventFrames(1);
        BombRecord throwingData = new BombDatabase().Data;
        Vector2 stoppedFront = catchMeat.Position +
            new Vector2(catchThrowDirection.X * 3, catchThrowDirection.Y * 3);
        byte stoppedTile = _rooms.CurrentRoom.GetMetatile(stoppedFront);
        FailIf(
            catchMeat.BounceCount != 1 || !catchMeat.Thrown ||
            catchMeat.SpeedZ >= 0 ||
            catchMeat.ThrowSpeedRaw !=
                throwingData.ReducedBounceSpeed(braceletData.SpeedRaw),
            "Wild Tokay meat stopped at its first ground contact instead of " +
            "using itemBounce to continue across the runner-lane border " +
            $"(bounces={catchMeat.BounceCount}, thrown={catchMeat.Thrown}, " +
            $"position={catchMeat.Position}, direction={catchMeat.ThrowDirection}, " +
            $"collision-set={_rooms.CurrentRoom.ActiveCollisions}, " +
            $"tile=${stoppedTile:x2}, passable=" +
            $"{throwingData.CanPassSolidTile(_rooms.CurrentRoom, stoppedFront)}, " +
            $"speed=${catchMeat.ThrowSpeedRaw:x2}, speed-z={catchMeat.SpeedZ}).");

        NpcCharacter? meatAccessory = null;
        for (int update = 0; update < 30 && meatAccessory is null; update++)
        {
            StepRoomEventFrames(1);
            meatAccessory = _entities.Entities<NpcCharacter>().FirstOrDefault(npc =>
                npc.Active && npc.Record is { Id: 0x63, SubId: 0x73 });
        }
        FailIf(
            meatAccessory is null || !catchMeat.Finished ||
            catchingParticipant.CurrentScriptAnimationSource != database.Animation(
                catchingParticipant.Position.X == database.ParticipantRightX ? 8 : 7),
            "Wild Tokay did not catch and delete the airborne meat, select " +
            "animation `$07/`$08, and create INTERAC_ACCESSORY `$63:$73.");
        WildTokayMeatAccessoryRecord caughtVisual = database.WildMeatAccessory(
            catchingParticipant.CurrentAnimationParameter);
        FailIf(
            meatAccessory!.Position != catchingParticipant.Position +
                new Vector2(caughtVisual.XOffset, caughtVisual.YOffset) ||
            meatAccessory.CurrentScriptAnimationSource !=
                caughtVisual.EncodedAnimation ||
            meatAccessory.CurrentAnimationOpaquePixels == 0,
            "The caught Tokay meat accessory did not use the parent animation " +
            "parameter's source offset and `$63:$73 graphic.");
        Vector2 catchPosition = catchingParticipant.Position;
        StepRoomEventFrames(6);
        caughtVisual = database.WildMeatAccessory(
            catchingParticipant.CurrentAnimationParameter);
        FailIf(
            catchingParticipant.Position != catchPosition ||
            meatAccessory.Position != catchingParticipant.Position +
                new Vector2(caughtVisual.XOffset, caughtVisual.YOffset),
            "Wild Tokay did not preserve the six-update catch pause while " +
            "keeping the meat accessory attached to its animation parameter.");
        StepRoomEventFrames(1);
        FailIf(
            catchingParticipant.Position.Y != catchPosition.Y + 0.5f,
            "Wild Tokay did not resume downward movement after the six-update " +
            "caught-meat pause.");

        System.Reflection.MethodInfo endRound =
            typeof(WildTokayGameEvent).GetMethod(
                "EndRound",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "Wild Tokay validation could not resolve EndRound.");
        int winningRoomMusicRequests =
            _sound.PlayRequestsFor(wildTokayRoomMusic);
        int successRequests = _sound.PlayRequestsFor(database.SoundSuccess);
        _player.WarpTo(manager.Position, recordSafe: false);
        endRound.Invoke(wildTokay, new object[] { true });
        FailIf(
            wildTokay.Stage != WildTokayGameStage.Wait ||
            wildTokay.Counter != 30 || !_player.CutsceneControlled ||
            _sound.PlayRequestsFor(database.SoundSuccess) != successRequests + 1,
            "Wild Tokay win did not play SND_FILLED_HEART_CONTAINER and begin " +
            "the source 30-update result-text delay.");
        StepRoomEventFrames(30);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.ResultText ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                database.Text(0x0a18)),
            "Wild Tokay win did not show TX_0a18 after the source 30-update delay.");
        _dialogue.Close();
        StepRoomEventFrames(21);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.ReturnFadeOut ||
            manager.Active ||
            _inventory.EquippedB != TreasureDatabase.TreasureBombs ||
            _inventory.EquippedA != TreasureDatabase.TreasureSword ||
            !_saveData.HasRoomFlag(2, 0xde, OracleSaveData.RoomFlag40) ||
            !Mathf.IsZeroApprox(_warpFade.Color.A),
            "Wild Tokay win did not restore equipment and enter the source " +
            "same-room white fade after the past-era 20-update result delay.");
        StepRoomEventFrames(31);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.ReturnFadeOut ||
            !Mathf.IsEqualApprox(_warpFade.Color.A, 1.0f) || manager.Active ||
            _sound.ActiveMusic != OracleSoundEngine.MusMinigame,
            "Wild Tokay win replaced MUS_MINIGAME or recreated its manager " +
            "before the return fade reached full white.");
        StepRoomEventFrames(1);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.ReturnFadeIn ||
            !manager.Active || meatAccessory.Active ||
            _saveData.HasRoomFlag(2, 0xde, OracleSaveData.RoomFlag40) ||
            _sound.ActiveMusic != wildTokayRoomMusic ||
            _sound.PlayRequestsFor(wildTokayRoomMusic) !=
                winningRoomMusicRequests + 1 ||
            _player.PrecisePosition != wildTokayResultPosition ||
            _player.FacingVector != Vector2I.Up ||
            database.WildStartTiles.Any(record =>
                _rooms.CurrentRoom.GetMetatile(
                    WildTilePoint(record.PackedPosition)) !=
                    originalWildTiles[record.PackedPosition]),
            "Wild Tokay win did not remove game entities, recreate the " +
            "manager, restore arena tiles, apply its `$48,$50 Link override " +
            "facing up, and restart room music `$26 at the fully-white same-room " +
            "boundary.");
        StepRoomEventFrames(31);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.ReturnFadeIn ||
            !Mathf.IsZeroApprox(_warpFade.Color.A),
            "Wild Tokay winning return fade did not reach palette offset `$00 " +
            "on fade-in update 31.");
        StepRoomEventFrames(1);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.Wait ||
            wildTokay.Counter != 30 ||
            _warpFade.Position != originalFadePosition ||
            _warpFade.Size != originalFadeSize ||
            _warpFade.ZIndex != originalFadeZ ||
            _warpFade.Color != originalFadeColor,
            "Wild Tokay win did not release the fade presentation and begin " +
            "the recreated past manager's 30-update prize delay.");
        StepRoomEventFrames(29);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.Wait ||
            wildTokay.Counter != 1 ||
            _entities.Entities<GroundTreasurePickup>().Any(reward =>
                !reward.Finished),
            "Wild Tokay spawned its winning prize before the past manager's " +
            "30-update post-warp delay completed.");
        StepRoomEventFrames(1);
        FailIf(
            wildTokay.Stage != WildTokayGameStage.Prize ||
            !_entities.Entities<GroundTreasurePickup>().Any(reward =>
                !reward.Finished),
            "Wild Tokay did not hand the winning prize to Link after the " +
            "source post-warp manager delay.");
        wildTokay.Cancel();
        _dialogue.Close();
        FailIf(
            _inventory.EquippedB != TreasureDatabase.TreasureBombs ||
            _inventory.EquippedA != TreasureDatabase.TreasureSword ||
            _player.CutsceneControlled || meatAccessory.Active ||
            database.WildStartTiles.Any(record =>
                _rooms.CurrentRoom.GetMetatile(
                    WildTilePoint(record.PackedPosition)) !=
                    originalWildTiles[record.PackedPosition]) ||
            wildTokay.ScreenTransitionsDisabled ||
            _roomEvents.ScreenTransitionsDisabled,
            "Cancelling Wild Tokay did not remove the caught-meat accessory, " +
            "restore Link's equips/control and arena tiles, or release exits.");

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
            "five-slot spawn cadence/order/count/RNG, start and throw path, " +
            "prize raise/hold/lower timing, full white " +
            "start fade, manager lifecycle, downward-facing participant " +
            "boundaries, four-door arena writes, exit confinement, exact " +
            "fixed-point throws and source bounce, caught-meat attachment/pause, " +
            "same-room result fades, manager-owned `$48,$50 Link return, room-music " +
            "restoration, delayed winning prize handoff, and restoration.");
    }
}
