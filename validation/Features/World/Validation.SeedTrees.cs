using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSeedTrees()
    {
        var database = new SeedTreeDatabase();
        SeedTreePlacementRecord canonical =
            database.GetRoomRecords(0, 0x78).Single();
        FailIf(
            database.PlacementCount != 10 ||
            canonical is not
                { Order: 0, Id: 0x5a, SubId: 0x06,
                  SeedType: 0, RefillIndex: 6 } ||
            database.Type(0) is not
                { TreasureId: TreasureDatabase.TreasureEmberSeeds,
                  TileBase: 0x12, Palette: 2, IntroTextId: 0x0029 },
            "The imported 0:78 Ember Seed tree record is incomplete.");

        var runtime = new OracleRuntimeState();
        FailIf(
            runtime.ReadWramByte(
                OracleRuntimeState.SeedTreeRefilledBitsetAddress) != 0xf0 ||
            runtime.ReadWramByte(
                OracleRuntimeState.SeedTreeRefilledBitsetAddress + 1) != 0xff ||
            !database.IsRefilled(runtime, canonical.RefillIndex),
            "initializeSeedTreeRefillData did not restore Ages' `$f0,$ff bitset.");

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var treasures = new TreasureDatabase();
        var rooms = new RoomSession(0, 0x78, static () => 0, static () => { }, save);
        OracleRoomData room = rooms.CurrentRoom;
        FailIf(
            !database.TryFindTreeCenter(room, out Vector2 center) ||
            room.GetMetatile(center - new Vector2(16, 16)) !=
                database.TreeTopLeftTile,
            "Room 0:78 does not contain the imported mystical-tree top-left metatile.");

        var noSatchelInventory = new InventoryState(treasures);
        var noSatchelRoot = new Node { Name = "SeedTreeNoSatchelValidation" };
        AddChild(noSatchelRoot);
        using var noSatchelFixture = RoomEntityValidationFixture.ForRoot(
            noSatchelRoot, new()
            {
                SaveData = save,
                RuntimeState = runtime,
                Inventory = noSatchelInventory,
                AnimationTick = static () => 0,
                Treasures = treasures,
                Rooms = rooms
            });
        RoomEntityManager noSatchelManager = noSatchelFixture.Manager;
        var messages = new List<(int TextId, string Message, Vector2 Position)>();
        noSatchelManager.SeedTreeMessageRequested +=
            (textId, message, position) =>
                messages.Add((textId, message, position));
        noSatchelManager.LoadRoom(0, rooms.GetRoom(0, 0x00));
        Vector2 incomingOffset = Vector2.Left * room.Width;
        noSatchelManager.BeginScreenTransition(0, room, incomingOffset);

        List<SeedOnTree> perched = noSatchelManager.Entities<SeedOnTree>();
        Vector2[] expectedPositions =
        [
            center + new Vector2(0, -8),
            center + new Vector2(-8, 0),
            center + new Vector2(8, 0)
        ];
        FailIf(
            perched.Count != 3 ||
            !perched.Select(seed => seed.Position).SequenceEqual(expectedPositions) ||
            perched.Any(seed => seed.State != SeedOnTreeState.Perched ||
                !seed.CollisionEnabled || !seed.Visible ||
                !seed.TransitionDrawOffset.IsEqualApprox(incomingOffset)),
            "Room 0:78 did not preload three visible seed parts at the incoming scroll offset.");
        noSatchelManager.Update(1.0, _player);
        FailIf(
            perched.Any(seed =>
            seed.State != SeedOnTreeState.Perched ||
            !seed.CollisionEnabled ||
            !seed.TransitionDrawOffset.IsEqualApprox(incomingOffset)),
            "Destination seed parts advanced while the screen transition was active.");
        noSatchelManager.FinishScreenTransition();
        _player.WarpTo(center + new Vector2(0, 24), recordSafe: false);
        noSatchelManager.Update(1.0 / 60.0, _player);

        SeedOnTree blockedSeed = perched[0];
        FailIf(
            noSatchelManager.ApplySwordHit(
                blockedSeed.CollisionBounds,
                _player.Position) ||
            messages.Count != 1 ||
            messages[0].TextId != database.NoSatchelTextId ||
            messages[0].Message != database.Visual.NoSatchelMessage ||
            blockedSeed.CollisionEnabled ||
            blockedSeed.State != SeedOnTreeState.Perched ||
            !database.IsRefilled(runtime, canonical.RefillIndex),
            "A no-satchel seed slash did not show TX_0035 while retaining the tree bit.");
        noSatchelManager.Clear();
        noSatchelManager.Dispose();
        RemoveChild(noSatchelRoot);
        noSatchelRoot.QueueFree();

        var collectionRuntime = new OracleRuntimeState();
        var collectionInventory = new InventoryState(treasures);
        collectionInventory.GiveTreasure(
            TreasureDatabase.TreasureSeedSatchel, 1);
        for (int index = 0; index < 14; index++)
        {
            FailIf(
                !collectionInventory.TryConsumeSelectedSatchelSeed(out _),
                "Could not prepare a partially depleted Ember Seed count.");
        }
        FailIf(
            collectionInventory.EmberSeeds != 0x06,
            "Seed-tree validation did not begin with six Ember Seeds.");

        var collectionRoot = new Node { Name = "SeedTreeCollectionValidation" };
        AddChild(collectionRoot);
        using var collectionFixture = RoomEntityValidationFixture.ForRoot(
            collectionRoot, new()
            {
                SaveData = save,
                RuntimeState = collectionRuntime,
                Inventory = collectionInventory,
                AnimationTick = static () => 0,
                Treasures = treasures,
                Rooms = rooms
            });
        RoomEntityManager collectionManager = collectionFixture.Manager;
        var sounds = new List<int>();
        collectionManager.SoundRequested += sounds.Add;
        collectionManager.LoadRoom(0, room);
        _player.WarpTo(center + new Vector2(0, 30), recordSafe: false);
        collectionManager.Update(1.0 / 60.0, _player);
        SeedOnTree falling = collectionManager.Entities<SeedOnTree>()[0];
        collectionManager.ApplySwordHit(
            falling.CollisionBounds,
            _player.Position);
        FailIf(
            falling.State != SeedOnTreeState.Fallen ||
            falling.SpeedZ != database.InitialSpeedZ ||
            falling.Angle != OracleObjectMath.AngleToward(
                falling.Position, _player.Position),
            "A satchel-owned seed did not begin its traced knock-off motion.");

        for (int update = 0;
             update < 90 &&
             database.IsRefilled(collectionRuntime, canonical.RefillIndex);
             update++)
        {
            collectionManager.Update(1.0 / 60.0, _player);
        }
        FailIf(
            database.IsRefilled(collectionRuntime, canonical.RefillIndex) ||
            collectionInventory.EmberSeeds != 0x12 ||
            sounds.Count(sound =>
                sound == OracleSoundEngine.SndGetSeed) != 1 ||
            collectionManager.Entities<SeedOnTree>().Count != 2,
            "A collected 0:78 seed did not grant BCD six, play SND_GETSEED, " +
            "notify its controller, and leave its siblings active.");

        collectionManager.LoadRoom(0, room);
        collectionManager.Update(1.0 / 60.0, _player);
        FailIf(
            collectionManager.Entities<SeedOnTree>().Count != 0,
            "A depleted seed tree respawned its seed parts on re-entry.");
        collectionManager.Clear();
        collectionManager.Dispose();
        RemoveChild(collectionRoot);
        collectionRoot.QueueFree();

        // Duplicates do not fill the eight-entry history, and entering the
        // tree screen clears an incomplete history.
        for (int roomId = 1; roomId <= 7; roomId++)
            database.UpdateRefillState(collectionRuntime, 2, roomId);
        database.UpdateRefillState(collectionRuntime, 3, 1);
        database.UpdateRefillState(collectionRuntime, 0, 0x78);
        FailIf(
            database.IsRefilled(collectionRuntime, canonical.RefillIndex),
            "A duplicate room incorrectly completed the seed-tree refill history.");

        for (int roomId = 1; roomId <= 8; roomId++)
            database.UpdateRefillState(collectionRuntime, 2, roomId);
        FailIf(
            database.IsRefilled(collectionRuntime, canonical.RefillIndex),
            "The seed-tree refill bit was set before revisiting its screen.");
        database.UpdateRefillState(collectionRuntime, 0, 0x78);
        FailIf(
            !database.IsRefilled(collectionRuntime, canonical.RefillIndex) ||
            Enumerable.Range(
                0, OracleRuntimeState.SeedTreeRefillRoomsPerLocation).Any(
                slot => collectionRuntime.ReadSeedTreeRefillRoom(
                    canonical.RefillIndex, slot) != 0),
            "Eight unique room bytes did not refill 0:78 and clear its history.");

        GD.Print(
            "Validated room 0:78's ENEMY_SEEDS_ON_TREE `$5a:$06 controller, " +
            "three transition-preloaded PART_SEED_ON_TREE offsets/visuals, " +
            "TX_0035 no-satchel branch, " +
            "satchel knock-off/bounce, BCD-six Ember grant, sibling lifetime, " +
            "depletion, and eight-unique-room outdoor refill state.");
    }
}
