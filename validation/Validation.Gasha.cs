using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGashaSpots()
    {
        var database = new GashaSpotDatabase();
        int[] expectedRanks =
            [1,2,2,1,4,1,1,0,3,2,2,1,4,3,1,0];
        if (database.Count != 16 || Enumerable.Range(0, 16).Any(index =>
                database.GetSpot(index).SubId != index ||
                database.GetSpot(index).Rank != expectedRanks[index]))
        {
            throw new InvalidOperationException(
                "The 16 imported Ages Gasha placements/ranks are incomplete or reordered.");
        }
        for (int rank = 0; rank < 5; rank++)
        for (int maturityClass = 0; maturityClass < 5; maturityClass++)
        for (int random = 0; random < 256; random++)
        {
            int reward = database.SelectRewardType(rank, maturityClass, (byte)random);
            if (reward is < 0 or >= 10)
                throw new InvalidOperationException("A Gasha distribution left the reward table.");
        }
        if (database.MaturityClass(0) != 4 || database.MaturityClass(39) != 4 ||
            database.MaturityClass(40) != 3 || database.MaturityClass(119) != 3 ||
            database.MaturityClass(120) != 2 || database.MaturityClass(199) != 2 ||
            database.MaturityClass(200) != 1 || database.MaturityClass(299) != 1 ||
            database.MaturityClass(300) != 0 || database.MaturityClass(0xffff) != 0)
        {
            throw new InvalidOperationException("Gasha maturity class boundaries changed.");
        }

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var treasures = new TreasureDatabase();
        var inventory = new InventoryState(treasures, save, () => -1);
        long tick = 0;
        var rooms = new RoomSession(0, 0x7b, () => tick, () => { }, save);
        GashaSpotDatabase.SpotRecord spot = database.GetSpot(3);
        if (save.GashaMaturity != 5 ||
            rooms.CurrentRoom.GetMetatile(spot.Position) != database.SoftSoilTile)
        {
            var soilPositions = new List<string>();
            for (int y = 0; y < rooms.CurrentRoom.HeightInTiles; y++)
            for (int x = 0; x < rooms.CurrentRoom.WidthInTiles; x++)
            {
                Vector2 point = new(x * 16 + 8, y * 16 + 8);
                if (rooms.CurrentRoom.GetMetatile(point) == database.SoftSoilTile)
                    soilPositions.Add($"${(y << 4 | x):x2}");
            }
            throw new InvalidOperationException(
                $"Entering a Gasha room did not add five maturity or retain its soft soil " +
                $"(maturity={save.GashaMaturity}, tile=${rooms.CurrentRoom.GetMetatile(spot.Position):x2}, " +
                $"soil={string.Join(',', soilPositions)}). ");
        }

        inventory.GiveTreasure(treasures.GetObject("TREASURE_OBJECT_RING_BOX_00"));
        inventory.GrantAppraisedRingForDebug((int)RingId.Discovery);
        if (!inventory.SetRingBoxSlotFromList(0, (int)RingId.Discovery) ||
            !inventory.EquipRingAt(0))
        {
            throw new InvalidOperationException("Could not equip the Discovery Ring for Gasha validation.");
        }
        var sounds = new List<int>();
        int roomRedraws = 0;
        var plantingRoot = new Node { Name = "GashaPlantingValidation" };
        var plantingWorld = new Node { Name = "World" };
        var plantingInterface = new Node { Name = "Interface" };
        var plantingView = new RoomView { Name = "RoomView" };
        var plantingDialogue = new DialogueBox { Name = "Dialogue" };
        plantingRoot.AddChild(plantingWorld);
        plantingRoot.AddChild(plantingInterface);
        plantingRoot.AddChild(plantingView);
        plantingRoot.AddChild(plantingDialogue);
        AddChild(plantingRoot);
        save.SetRoomFlag(0, 0x7b, OracleSaveData.RoomFlag40);
        var plantingManager = new RoomEntityManager(
            plantingWorld, new NpcDatabase(), new EnemyDatabase(),
            new ItemDropDatabase(), new TimePortalDatabase(), new OracleRandom(),
            save, inventory: inventory, animationTick: () => tick);
        plantingManager.SoundRequested += sounds.Add;
        plantingManager.RoomTileChanged += () => roomRedraws++;
        var plantingInteractions = new InteractionController(
            rooms, plantingManager, new SignDatabase(), new ChestDatabase(),
            treasures, plantingDialogue, plantingWorld, plantingView,
            static position => position, () => tick, inventory,
            plantingInterface, sounds.Add);
        plantingManager.LoadRoom(0, rooms.CurrentRoom);
        _player.WarpTo(spot.Position + new Vector2(0, 15), recordSafe: false);
        plantingManager.Update(1.0 / 60.0, _player);
        if (sounds.Count(sound => sound == OracleSoundEngine.SndCompass) != 1 ||
            !plantingInteractions.TryInteract(_player) ||
            DialogueBox.PlainText(plantingDialogue.CurrentMessage) !=
                DialogueBox.PlainText(database.Text(0x3509)))
        {
            throw new InvalidOperationException(
                "Discovery detection or the no-Gasha-Seeds interaction failed.");
        }
        plantingDialogue.Close();
        plantingInteractions.Update(1.0 / 60.0, _player);

        inventory.GiveTreasure(treasures.GetObject("TREASURE_OBJECT_GASHA_SEED_00"));
        if (!plantingInteractions.TryInteract(_player) ||
            !plantingDialogue.ChoiceActive ||
            DialogueBox.PlainText(plantingDialogue.CurrentMessage) !=
                DialogueBox.PlainText(database.Text(0x3500)))
        {
            throw new InvalidOperationException("Gasha planting did not open its Yes/No prompt.");
        }
        plantingDialogue.SubmitChoiceForValidation(1);
        plantingInteractions.Update(1.0 / 60.0, _player);
        if (inventory.GashaSeeds != 1 || save.IsGashaSpotPlanted(spot.SubId))
            throw new InvalidOperationException("No planted a Gasha Seed or consumed its BCD count.");

        if (!plantingInteractions.TryInteract(_player))
            throw new InvalidOperationException("A declined Gasha spot did not become interactive again.");
        plantingDialogue.SubmitChoiceForValidation(0);
        plantingInteractions.Update(1.0 / 60.0, _player);
        if (inventory.GashaSeeds != 0 ||
            !save.IsGashaSpotPlanted(spot.SubId) ||
            save.GetGashaSpotKillCounter(spot.SubId) != 0 ||
            rooms.CurrentRoom.GetMetatile(spot.Position) != database.PlantedSoilTile ||
            sounds.Count(sound => sound == OracleSoundEngine.SndGetSeed) != 1 ||
            roomRedraws != 1)
        {
            throw new InvalidOperationException(
                "Discovery detection or the Gasha plant/consume/persist transition failed.");
        }
        plantingManager.Clear();
        RemoveChild(plantingRoot);
        plantingRoot.QueueFree();

        save.SetGashaSpotKillCounter(spot.SubId, 19);
        OracleRoomData sproutRoom = rooms.GetRoom(spot.Group, spot.Room);
        if (sproutRoom.GetMetatile(spot.Position) != database.PlantedSoilTile ||
            sproutRoom.IsSolid(spot.Position))
        {
            throw new InvalidOperationException("A 19-kill Gasha seed did not remain a walkable sprout.");
        }
        ulong sproutChecksum = sproutRoom.GetAnimationChecksum(tick);
        save.SetGashaSpotKillCounter(spot.SubId, 20);
        OracleRoomData treeRoom = rooms.GetRoom(spot.Group, spot.Room);
        if (treeRoom.GetMetatile(spot.TreeTopLeft) != database.TreeTopLeftTile ||
            !treeRoom.IsSolid(spot.TreeTopLeft) ||
            treeRoom.GetAnimationChecksum(tick) == sproutChecksum)
        {
            throw new InvalidOperationException(
                $"A 20-kill Gasha sprout did not become the imported solid 2x2 tree " +
                $"(top-left=${treeRoom.GetMetatile(spot.TreeTopLeft):x2}, " +
                $"spot=${treeRoom.GetMetatile(spot.Position):x2}, " +
                $"solid={treeRoom.IsSolid(spot.TreeTopLeft)}, " +
                $"tree-checksum={treeRoom.GetAnimationChecksum(tick)}, " +
                $"sprout-checksum={sproutChecksum}). ");
        }

        save.SetGashaSpotKillCounter(spot.SubId, 40);
        OracleRoomData nutRoom = rooms.GetRoom(spot.Group, spot.Room);
        bool nutCaught = false;
        var harvest = new GashaSpotInteraction();
        harvest.Initialize(
            database, spot, nutRoom, save, inventory,
            (_, _) => throw new InvalidOperationException("A mature Gasha tree requested planting."),
            (_, _) => nutCaught = true,
            sounds.Add, () => roomRedraws++, () => tick);
        harvest.UpdateFrame(_player);
        _player.WarpTo(harvest.Position + new Vector2(0, 11), recordSafe: false);
        Rect2 slash = new(harvest.Position - new Vector2(5, 5), new Vector2(10, 10));
        var harvestAdapter = new GashaSpotRoomEntity(harvest);
        if (!harvest.ApplySwordHit(slash, _player.Position) ||
            harvest.State != GashaSpotInteraction.InteractionState.NutAirborne ||
            !harvest.RestrictsPlayer || !harvestAdapter.DisablesMenus)
        {
            throw new InvalidOperationException(
                "The 40-kill Gasha nut was not slashable or did not disable Link/menus.");
        }
        harvest.UpdateFrame(_player);
        if (!nutCaught || harvest.State !=
            GashaSpotInteraction.InteractionState.AwaitingNutText)
        {
            throw new InvalidOperationException("The launched Gasha nut did not collide with Link.");
        }

        int firstRandomCalls = 0;
        GashaRewardResolver.Result first = GashaRewardResolver.Give(
            database, spot, save, inventory,
            () => { firstRandomCalls++; return 0; });
        if (first.RewardType != 4 || first.Parameter != (int)RingId.Cursed ||
            firstRandomCalls != 1 || !save.HasHarvestedFirstGashaNut ||
            save.GashaMaturity != 5 || inventory.UnappraisedRingCount != 1)
        {
            throw new InvalidOperationException(
                "The first Gasha nut did not force a class-1 ring with one tier RNG call and no maturity debit.");
        }
        harvest.BeginReward(first.RewardType, first.Reward, _player);
        if (!_player.IsHoldingItemTwoHands ||
            harvest.State != GashaSpotInteraction.InteractionState.RewardHeld ||
            sounds.Count(sound => sound == OracleSoundEngine.SndGetItem) != 1)
        {
            throw new InvalidOperationException("Gasha reward presentation did not use Link's two-hand pose.");
        }
        harvest.BeginDisappearance();
        if (_player.IsHoldingItemTwoHands ||
            sounds.Count(sound => sound == OracleSoundEngine.SndFairyCutscene) != 1)
        {
            throw new InvalidOperationException("Gasha tree disappearance did not release Link and play its cue.");
        }
        var disappearanceChecksums = new HashSet<ulong>
        {
            nutRoom.GetAnimationChecksum(tick)
        };
        for (int frame = 0; frame < 79; frame++)
        {
            tick++;
            harvest.UpdateFrame(_player);
            int expectedPhase = frame < 6 ? 0 : 1 + (frame - 6) / 8;
            if (harvest.DisappearancePhase != expectedPhase)
                throw new InvalidOperationException(
                    $"Gasha disappearance reached phase {harvest.DisappearancePhase} " +
                    $"instead of {expectedPhase} on update {frame + 1}.");
            if (expectedPhase <= database.DisappearancePhases)
                disappearanceChecksums.Add(nutRoom.GetAnimationChecksum(tick));
        }
        if (!harvest.Finished || save.IsGashaSpotPlanted(spot.SubId) ||
            nutRoom.GetMetatile(spot.TreeTopLeft) != spot.ReplacementTile ||
            nutRoom.GetMetatile(spot.TreeTopLeft + new Vector2(16, 16)) !=
                spot.ReplacementTile || disappearanceChecksums.Count < 7)
        {
            throw new InvalidOperationException(
                "The nine Gasha shrink frames did not end on the spot-specific 2x2 replacement.");
        }
        harvest.Free();

        OracleRoomData reusableRoom = rooms.Load(spot.Group, spot.Room);
        if (reusableRoom.GetMetatile(spot.Position) != database.SoftSoilTile ||
            save.IsGashaSpotPlanted(spot.SubId))
        {
            throw new InvalidOperationException(
                "A harvested Gasha spot did not restore reusable soft soil on re-entry.");
        }

        OracleSaveData rewardSave = OracleSaveData.CreateStandardGame();
        var rewardInventory = new InventoryState(treasures, rewardSave, () => -1);
        rewardSave.SetGashaHarvestFlag(0);
        rewardSave.AddGashaMaturity(136);
        rewardSave.SetGashaHarvestFlag(1);
        int repeatedCalls = 0;
        GashaRewardResolver.Result repeatedHeart = GashaRewardResolver.Give(
            database, database.GetSpot(7), rewardSave, rewardInventory,
            () => { repeatedCalls++; return 0; });
        if (repeatedHeart.RewardType != 1 ||
            repeatedHeart.Parameter != (int)RingId.Experts || repeatedCalls != 2 ||
            rewardSave.GashaMaturity != 0)
        {
            throw new InvalidOperationException(
                "A repeated Gasha Heart Piece did not become a tier-0 ring with two RNG calls and clamped maturity.");
        }

        rewardInventory.GiveTreasure(TreasureDatabase.TreasurePotion, 1);
        rewardInventory.ApplyDamage(4);
        int potionCalls = 0;
        GashaRewardResolver.Result potion = GashaRewardResolver.Give(
            database, database.GetSpot(7), rewardSave, rewardInventory,
            () => { potionCalls++; return 64; });
        if (potion.RewardType != 6 || potionCalls != 1 ||
            rewardInventory.HealthQuarters != rewardInventory.MaxHealthQuarters)
        {
            throw new InvalidOperationException(
                "An already-owned Gasha potion did not refill health without changing RNG use.");
        }

        OracleSaveData maturitySave = OracleSaveData.CreateStandardGame();
        var maturityInventory = new InventoryState(treasures, maturitySave, () => -1);
        maturityInventory.GiveTreasure(TreasureDatabase.TreasureEssence, 1);
        maturityInventory.GiveTreasure(TreasureDatabase.TreasureHeartPiece, 1);
        maturityInventory.GiveTreasure(TreasureDatabase.TreasureTradeItem, 1);
        maturityInventory.GiveTreasure(TreasureDatabase.TreasureHeartRefill, 0x18);
        if (maturitySave.GashaMaturity != 150 + 36 + 100 + 0x18)
        {
            throw new InvalidOperationException(
                "Essence, Heart Piece, trade-item, and heart-refill maturity sources diverged from giveTreasure.");
        }

        GashaSpotDatabase.SpotRecord mapSpot = database.GetSpot(0);
        bool mapSpotWasPlanted = _saveData.IsGashaSpotPlanted(mapSpot.SubId);
        _saveData.SetGashaSpotPlanted(mapSpot.SubId, false);
        int hiddenPopup = _mapScreen.ResolvePopupTypeForValidation(
            9, mapSpot.Group, mapSpot.Room);
        _saveData.SetGashaSpotPlanted(mapSpot.SubId, true);
        int plantedPopup = _mapScreen.ResolvePopupTypeForValidation(
            9, mapSpot.Group, mapSpot.Room);
        _saveData.SetGashaSpotPlanted(mapSpot.SubId, mapSpotWasPlanted);
        if (hiddenPopup != 0 || plantedPopup != 9)
            throw new InvalidOperationException("Gasha map popup did not follow the planted bit.");

        GD.Print("Validated all 16 Gasha spots, Discovery Ring cue, no-seed/Yes/No dialogue, " +
            "BCD planting, 20/40-kill growth, slash/launch/catch and menu lock, first and repeated " +
            "reward RNG, potion/Heart Piece exceptions, maturity sources/debit, two-hand " +
            "presentation, nine timed shrink frames, ground-sheet rendering, reusable re-entry, " +
            "and map popup state.");
    }
}
