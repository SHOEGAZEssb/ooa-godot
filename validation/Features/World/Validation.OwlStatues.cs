using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom180OwlStatue()
    {
        var owlDatabase = new OwlStatueDatabase();
        OwlStatueRecord owlRecord = owlDatabase.Record(0x06);
        var seedDatabase = new SeedSatchelDatabase();
        SeedRecord mystery = seedDatabase.Mystery;
        var treeDatabase = new SeedTreeDatabase();
        SeedTreePlacementRecord tree =
            treeDatabase.GetRoomRecords(1, 0x80).Single();
        var enemyDatabase = new EnemyDatabase();
        RoomObjectRecord owlPlacement = enemyDatabase
            .GetRoomObjects(1, 0x80)
            .Single();
        RoomObjectRecord[] cleanUsRoom10e = enemyDatabase
            .GetRoomObjects(1, 0x0e)
            .ToArray();

        FailIf(
            owlDatabase.Count != 0x14 ||
            owlRecord is not
            {
                SubId: 0x06,
                TextId: 0x3906,
                Sprite: "spr_roller_owl_barrier_orb",
                TileBase: 0x0a,
                Palette: 1,
                CollisionMode: 0x82,
                RadiusY: 7,
                RadiusX: 7,
                FloorTile: 0x00,
                FloorCollision: 0x0f,
                MysteryCollision: 0x9a,
                ActivationCounter: 50,
                SpeakingCounter: 30,
                TextCounter: 22
            } ||
            owlRecord.Message !=
                "Do not forget\nto feed me\nMystery Seeds." ||
            !owlRecord.SparkleOffsets.SequenceEqual(
            [
                new Vector2(5, -7),
                new Vector2(-1, 6),
                new Vector2(-6, -4),
                new Vector2(7, 2),
                new Vector2(-6, 0),
                new Vector2(2, -1)
            ]),
            "The imported PART_OWL_STATUE `$13:$06 definition is incomplete.");
        FailIf(
            owlPlacement is not
            {
                Order: 0,
                Kind: RoomObjectKind.ReservingPart,
                Id: 0x13,
                SubId: 0x06,
                PackedPosition: 0x33
            },
            "Room 1:80 is missing ordered PART_OWL_STATUE `$13:$06 at `$33.");
        FailIf(
            tree is not
            {
                Order: 0,
                Id: 0x5a,
                SubId: 0x4d,
                SeedType: 4,
                RefillIndex: 13
            } ||
            treeDatabase.Type(4).TreasureId != 0x24,
            "Room 1:80 is missing ENEMY_SEEDS_ON_TREE `$5a:$4d Mystery data.");
        FailIf(
            cleanUsRoom10e.Length != 3 ||
            cleanUsRoom10e[0] is not
            {
                Kind: RoomObjectKind.ReservingPart,
                Id: 0x13,
                SubId: 0x13,
                PackedPosition: 0x23
            } ||
            cleanUsRoom10e.Any(record =>
                record.Id == 0x13 && record.SubId == 0x09),
            "The clean-US conditional enemy-object stream was not selected.");

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var rooms = new RoomSession(
            1, 0x80, static () => 0, static () => { }, save);
        OracleRoomData room = rooms.CurrentRoom;
        Vector2 owlPosition = new(0x38, 0x38);
        byte originalOwlTile = room.GetMetatile(owlPosition);
        ulong originalOwlTileHash = OracleGraphicsCache.PixelHash(
            room.BuildMimickedMetatileTexture(owlPosition).GetImage());
        ulong logicalFloorTileHash = OracleGraphicsCache.PixelHash(
            room.BuildMimickedMetatileTexture(
                (byte)owlRecord.FloorTile).GetImage());
        var root = new Node { Name = "Room180OwlStatueValidation" };
        AddChild(root);
        using var fixture = RoomEntityValidationFixture.ForRoot(
            root,
            new()
            {
                SaveData = save,
                RuntimeState = new OracleRuntimeState(),
                Inventory = new InventoryState(_treasures, save),
                AnimationTick = static () => 0,
                Treasures = _treasures,
                Rooms = rooms
            });
        RoomEntityManager manager = fixture.Manager;
        var messages = new List<(int TextId, string Message, Vector2 Position)>();
        var sounds = new List<int>();
        bool textActive = false;
        manager.OwlStatueMessageRequested +=
            (textId, message, position) =>
            {
                messages.Add((textId, message, position));
                textActive = true;
            };
        manager.SoundRequested += sounds.Add;
        manager.TextActiveSource = () => textActive;

        OracleRoomData outgoing = rooms.GetRoom(1, 0x81);
        manager.LoadRoom(1, outgoing);
        Vector2 incomingOffset = Vector2.Right * room.Width;
        manager.BeginScreenTransition(1, room, incomingOffset);
        OwlStatueRoomEntity owl =
            manager.Entities<OwlStatueRoomEntity>().Single();
        FailIf(
            owl.Position != owlPosition ||
            owl.State != OwlStatueState.Idle ||
            owl.AnimationIndex != 0 ||
            owl.ZIndex != NpcCharacter.FixedLowPriorityZIndex ||
            !owl.Visible ||
            !owl.TransitionDrawOffset.IsEqualApprox(incomingOffset) ||
            manager.Entities<SeedOnTree>().Count != 3 ||
            room.GetMetatile(owl.Position) != 0x00 ||
            room.GetTerrainInfo(owl.Position).Collision != 0x0f ||
            originalOwlTile == owlRecord.FloorTile ||
            originalOwlTileHash == logicalFloorTileHash ||
            OracleGraphicsCache.PixelHash(
                room.BuildMimickedMetatileTexture(
                    owl.Position).GetImage()) != originalOwlTileHash,
            "Room 1:80 did not preload its Owl Statue with logical solid " +
            "`$00/`$0f state, preserved visible ground, and three Mystery " +
            "Seeds at the destination draw offset.");
        manager.Update(1.0, _player);
        FailIf(
            owl.ElapsedUpdates != 0 ||
            owl.State != OwlStatueState.Idle ||
            !owl.TransitionDrawOffset.IsEqualApprox(incomingOffset),
            "Room 1:80's Owl Statue advanced during the screen transition.");

        manager.FinishScreenTransition();
        manager.Update(1.0 / 60.0, _player);
        var directSpawns = new List<RoomEntitySpawn>();
        FailIf(
            owl.ApplySeedHit(
                owl.CollisionBounds,
                owl.Position,
                0x20,
                directSpawns) != SeedHitResult.None ||
            owl.State != OwlStatueState.Idle,
            "PART_OWL_STATUE accepted an Ember Seed instead of collision `$9a.");

        int randomCallsBefore = manager.RandomCalls;
        EmberSeedEffect projectile = manager.Spawn<EmberSeedEffect>(
            new EmberSeedSpawn(
                owl.Position - (Vector2)mystery.RightOffset,
                Vector2I.Right,
                mystery,
                1));
        manager.Update(1.0 / 60.0, _player);
        FailIf(
            manager.RandomCalls != randomCallsBefore + 1 ||
            projectile.SeedItem != 0x24 ||
            projectile.MysteryEffect is < 0 or > 3 ||
            projectile.State != EmberState.Mystery ||
            projectile.CollisionEnabled ||
            owl.State != OwlStatueState.Activating ||
            owl.Counter != 49 ||
            sounds.Count(sound =>
                sound == OracleSoundEngine.SndMysterySeed) != 1,
            "ITEM_MYSTERY_SEED did not consume one RNG value, activate on " +
            "the Owl Statue, disable collision, and request SND_MYSTERY_SEED.");
        int retainedCounter = owl.Counter;
        FailIf(
            owl.ApplySeedHit(
                owl.CollisionBounds,
                owl.Position,
                mystery.SeedItem,
                directSpawns) != SeedHitResult.Activate ||
            owl.Counter != retainedCounter,
            "An active PART_OWL_STATUE did not terminate a second Mystery " +
            "Seed while retaining its source counter.");

        var sparklePositions = new HashSet<Vector2>();
        for (int update = 0; update < 49; update++)
        {
            manager.Update(1.0 / 60.0, _player);
            foreach (OwlStatueSparkleEffect sparkle in
                manager.Entities<OwlStatueSparkleEffect>())
            {
                sparklePositions.Add(sparkle.Position);
            }
            if (update == 0)
            {
                FailIf(
                    owl.Counter != 48 ||
                    !sparklePositions.SetEquals(
                        [owl.Position + new Vector2(2, -1)]),
                    "The item-before-part pass did not let PART_OWL_STATUE " +
                    "counter `$30 emit the final source-table offset first.");
            }
        }

        HashSet<Vector2> expectedSparkles = owlRecord.SparkleOffsets
            .Select(offset => owl.Position + offset)
            .ToHashSet();
        FailIf(
            owl.State != OwlStatueState.Speaking ||
            owl.Counter != 30 ||
            owl.AnimationIndex != 1 ||
            !sparklePositions.SetEquals(expectedSparkles),
            "PART_OWL_STATUE did not emit all six reversed 8-update sparkle " +
            "offsets or enter its `$1e speaking pose.");

        for (int update = 0; update < 8; update++)
            manager.Update(1.0 / 60.0, _player);
        FailIf(
            owl.Counter != 22 ||
            messages.Count != 1 ||
            messages[0] !=
                (0x3906, owlRecord.Message, owl.Position),
            "Room 1:80 did not show TX_3906 when the speaking counter reached `$16.");

        for (int update = 0; update < 40; update++)
            manager.Update(1.0 / 60.0, _player);
        FailIf(
            owl.State != OwlStatueState.Speaking ||
            owl.Counter != 22 ||
            owl.AnimationIndex != 1 ||
            manager.Entities<OwlStatueSparkleEffect>().Count != 0,
            "TX_3906 did not freeze the Owl Statue at counter `$16 while " +
            "its always-update sparkle children retired.");

        textActive = false;
        for (int update = 0; update < 22; update++)
            manager.Update(1.0 / 60.0, _player);
        FailIf(
            owl.State != OwlStatueState.Idle ||
            owl.Counter != 0 ||
            owl.AnimationIndex != 0 ||
            manager.Entities<EmberSeedEffect>().Count != 0 ||
            manager.Entities<OwlStatueSparkleEffect>().Count != 0,
            "PART_OWL_STATUE did not restore its idle pose or retire its " +
            "Mystery Seed / sparkle children after the source counters.");

        manager.Clear();
        manager.Dispose();
        RemoveChild(root);
        root.QueueFree();

        GD.Print(
            "Validated room 1:80's Mystery Seed tree and shared " +
            "PART_OWL_STATUE `$13:$06: clean-US placement, preserved visible " +
            "ground with logical solid `$00/`$0f state, transition freeze, " +
            "Mystery-only `$9a activation, RNG, six sparkles, 50/30/22 " +
            "counters, TX_3906, poses, and reset.");
    }
}
