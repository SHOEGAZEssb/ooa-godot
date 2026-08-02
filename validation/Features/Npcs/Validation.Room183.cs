using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom183MiscManAndDrops()
    {
        const double frame = 1.0 / 60.0;
        var root = new Node { Name = "Room183MiscManAndDropsValidation" };
        var worldRoot = new Node { Name = "World" };
        var interfaceLayer = new Node { Name = "Interface" };
        var roomView = new RoomView { Name = "RoomView" };
        var dialogue = new DialogueBox { Name = "Dialogue" };
        root.AddChild(worldRoot);
        root.AddChild(interfaceLayer);
        root.AddChild(roomView);
        root.AddChild(dialogue);
        AddChild(root);

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        long tick = 0;
        var rooms = new RoomSession(
            1, 0x83, () => tick, () => tick = 0, save);
        var treasures = new TreasureDatabase();
        var inventory = new InventoryState(
            treasures, save, () => rooms.CurrentDungeonIndex);
        inventory.GiveTreasure(TreasureDatabase.TreasureBombs, 0x04);
        inventory.GiveTreasure(
            TreasureDatabase.TreasureEmberSeeds +
                ItemDropDatabase.MysterySeeds -
                ItemDropDatabase.EmberSeeds,
            0x20);
        var sounds = new List<int>();
        using var fixture = RoomEntityValidationFixture.ForRoot(
            worldRoot, new()
            {
                SaveData = save,
                Inventory = inventory,
                Treasures = treasures,
                Rooms = rooms
            });
        RoomEntityManager manager = fixture.Manager;
        manager.SoundRequested += sounds.Add;
        var interactions = new InteractionController(
            rooms, manager, new SignDatabase(), new ChestDatabase(),
            treasures, dialogue, worldRoot, roomView,
            static position => position, () => tick, inventory,
            interfaceLayer, sounds.Add);

        static string PlainWords(string message) => string.Join(
            " ",
            DialogueBox.PlainText(message).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        var enemyData = new EnemyDatabase();
        IReadOnlyList<RoomObjectRecord> objects =
            enemyData.GetRoomObjects(1, 0x83);
        FailIf(
            objects.Count != 4 ||
            objects[0] is not
                {
                    Order: 0, Kind: RoomObjectKind.ItemDrop, Id: 0x57,
                    SubId: ItemDropDatabase.Heart, PackedPosition: 0x22
                } ||
            objects[1] is not
                {
                    Order: 1, Kind: RoomObjectKind.ItemDrop, Id: 0x57,
                    SubId: ItemDropDatabase.MysterySeeds,
                    PackedPosition: 0x12
                } ||
            objects[2] is not
                {
                    Order: 2, Kind: RoomObjectKind.ItemDrop, Id: 0x57,
                    SubId: ItemDropDatabase.Heart, PackedPosition: 0x18
                } ||
            objects[3] is not
                {
                    Order: 3, Kind: RoomObjectKind.ItemDrop, Id: 0x57,
                    SubId: ItemDropDatabase.Bombs, PackedPosition: 0x28
                },
            "Room 1:83 did not retain its source-ordered heart, Mystery " +
            "Seed, heart, and Bomb drop producers.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);
        manager.LoadRoom(1, rooms.CurrentRoom);

        NpcCharacter man = manager.Entities<NpcCharacter>().Single();
        Vector2[] expectedProducerPositions =
        [
            new(0x28, 0x28),
            new(0x28, 0x18),
            new(0x88, 0x18),
            new(0x88, 0x28)
        ];
        List<ItemDropProducer> producers =
            manager.Entities<ItemDropProducer>();
        FailIf(
            man.BaseRecord is not
            {
                Group: 1,
                Room: 0x83,
                Id: 0x41,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x2606,
                SpriteName: "spr_hobos",
                TileBase: 0,
                Palette: 0,
                DefaultAnimation: 2,
                CanFace: true,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            man.Position != new Vector2(0x4e, 0x38) ||
            !man.Active ||
            !man.Visible ||
            man.FacingVector != Vector2I.Down ||
            man.CurrentAnimationOpaquePixels == 0 ||
            PlainWords(man.Message) !=
                "I'm lookin' at this cave thinkin' there's treasure " +
                "inside, but I can't get in with this rock here. If I " +
                "could just blast it away..." ||
            producers.Count != 4 ||
            producers.Where((producer, index) =>
                producer.Position != expectedProducerPositions[index]).Any() ||
            producers.Any(producer => producer.Initialized) ||
            manager.RandomCalls != 256,
            "Room 1:83 did not load misc man $41:$00 and its four hidden " +
            "drop producers with exact source positions, visuals, and TX_2606.");

        List<ItemDropProducer> outgoingProducers = producers;
        manager.BeginScreenTransition(
            1,
            rooms.CurrentRoom,
            Vector2.Right * rooms.CurrentRoom.Width);
        man = manager.Entities<NpcCharacter>().Single();
        producers = manager.Entities<ItemDropProducer>();
        FailIf(
            !manager.ScreenTransitionActive ||
            producers.Count != 4 ||
            producers.Where((producer, index) =>
                producer.Position != expectedProducerPositions[index]).Any() ||
            producers.Any(producer =>
                !producer.Initialized || producer.Visible) ||
            manager.Entities<ItemDropEffect>().Count != 0 ||
            !manager.OutgoingEntities<ItemDropProducer>()
                .SequenceEqual(outgoingProducers) ||
            outgoingProducers.Any(producer => producer.Initialized) ||
            manager.RandomCalls != 512,
            "Room 1:83 scrolling preload did not capture the four item-drop " +
            "producer tiles invisibly in source state 0 while freezing the " +
            "outgoing entities.");
        manager.FinishScreenTransition();
        FailIf(
            manager.ScreenTransitionActive ||
            manager.OutgoingEntities<ItemDropProducer>().Count != 0 ||
            producers.Any(producer => !producer.Initialized || producer.Visible),
            "Room 1:83 item-drop producers did not remain initialized and " +
            "hidden when scrolling completed.");

        FailIf(
            man.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            man.ObjectCollisionBounds.GetCenter() != man.Position ||
            man.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            man.LinkBlockingBounds.GetCenter() != man.Position ||
            !manager.BlocksLink(man.Position) ||
            manager.BlocksLink(man.Position + Vector2.Down * 12.0f),
            "Room 1:83's misc man did not retain ordinary $06/$06 solid " +
            "collision and the strict 12-pixel Link boundary.");

        ulong initialFrameHash = man.CurrentAnimationPixelHash;
        _player.WarpTo(
            man.Position + Vector2.Down * 40.0f,
            recordSafe: false);
        for (int update = 0; update < 15; update++)
            manager.Update(frame, _player);
        FailIf(
            man.CurrentAnimationFrame != 0 ||
            man.FacingVector != Vector2I.Down ||
            producers.Any(producer => !producer.Initialized),
            "Room 1:83's misc man advanced animation $02 early or its " +
            "drop producers failed to capture their source metatiles.");
        manager.Update(frame, _player);
        FailIf(
            man.CurrentAnimationFrame != 1 ||
            man.CurrentAnimationPixelHash == initialFrameHash ||
            man.FacingVector != Vector2I.Down,
            "Room 1:83's misc man did not reach animation $02's second " +
            "pose after exactly 16 updates.");
        for (int update = 0; update < 16; update++)
            manager.Update(frame, _player);
        FailIf(
            man.CurrentAnimationFrame != 0 ||
            man.CurrentAnimationPixelHash != initialFrameHash ||
            manager.RandomCalls != 512,
            "Room 1:83's two-pose animation $02 did not loop after 32 " +
            "updates or consumed room RNG.");

        _player.WarpTo(
            man.Position + Vector2.Left * 20.0f,
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            man.FacingVector != Vector2I.Left,
            "Room 1:83's misc man did not face Link to his left.");
        _player.WarpTo(
            man.Position + Vector2.Right * 20.0f,
            recordSafe: false);
        for (int update = 0; update < 29; update++)
            manager.Update(frame, _player);
        FailIf(
            man.FacingVector != Vector2I.Left,
            "Room 1:83's misc man changed direction before the source " +
            "$1d-to-$00 facing cooldown elapsed.");
        manager.Update(frame, _player);
        FailIf(
            man.FacingVector != Vector2I.Right,
            "Room 1:83's misc man did not face Link to his right on the " +
            "30th following update.");

        _player.WarpTo(
            man.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !man.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            !dialogue.IsOpen ||
            PlainWords(dialogue.CurrentMessage) !=
                "I'm lookin' at this cave thinkin' there's treasure " +
                "inside, but I can't get in with this rock here. If I " +
                "could just blast it away...",
            "Room 1:83's misc man did not run the TX_2606 A-button loop.");
        int frozenFrame = man.CurrentAnimationFrame;
        manager.Update(16.0 / 60.0, _player);
        FailIf(
            man.CurrentAnimationFrame != frozenFrame,
            "Gameplay text did not freeze room 1:83's misc-man animation.");
        dialogue.Close();
        interactions.Update(frame, _player);

        foreach (ItemDropProducer producer in producers)
        {
            byte tile = rooms.CurrentRoom.GetMetatile(producer.Position);
            rooms.CurrentRoom.SetPositionTileAndCollision(
                producer.Position, (byte)(tile ^ 1), null, tick);
        }
        manager.Update(frame, _player);
        List<ItemDropEffect> drops = manager.Entities<ItemDropEffect>();
        int[] expectedDropSubIds =
        [
            ItemDropDatabase.Heart,
            ItemDropDatabase.MysterySeeds,
            ItemDropDatabase.Heart,
            ItemDropDatabase.Bombs
        ];
        FailIf(
            manager.Entities<ItemDropProducer>().Count != 0 ||
            drops.Count != 4 ||
            drops.Where((drop, index) =>
                drop.SubId != expectedDropSubIds[index] ||
                drop.Position != expectedProducerPositions[index] ||
                drop.ElapsedFrames != 1).Any(),
            "Room 1:83's four source-ordered tile changes did not create " +
            "the exact immediately updated item drops.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            man.Active ||
            man.Visible ||
            man.CanTalkTo(_player) ||
            manager.BlocksLink(man.Position),
            "GLOBALFLAG_0b did not live-delete room 1:83's misc man.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        FailIf(
            !man.Active ||
            !man.Visible ||
            man.TextId != 0x2606,
            "Clearing GLOBALFLAG_0b did not restore room 1:83's misc man.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        FailIf(
            man.Active ||
            man.Visible ||
            man.CanTalkTo(_player) ||
            manager.BlocksLink(man.Position),
            "GLOBALFLAG_FINISHEDGAME did not live-delete room 1:83's misc man.");
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);
        FailIf(
            !man.Active ||
            !man.Visible ||
            man.TextId != 0x2606,
            "Clearing GLOBALFLAG_FINISHEDGAME did not restore room 1:83.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        manager.ClearRecentEnemyDefeats();
        manager.LoadRoom(1, rooms.CurrentRoom);
        man = manager.Entities<NpcCharacter>().Single();
        FailIf(
            man.Active ||
            man.Visible ||
            man.Position != new Vector2(0x4e, 0x38) ||
            man.BaseRecord.TextId != 0x2606 ||
            manager.Entities<ItemDropProducer>().Count != 4 ||
            manager.RandomCalls != 768,
            "Room 1:83 post-palace re-entry did not retain its suppressed " +
            "NPC record, four producers, and one room-parse RNG buffer.");

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print(
            "Validated room 1:83 misc man $41:$00 placement, TX_2606, " +
            "Link-facing animation $02, collision/talkability, textbox " +
            "freeze, palace/endgame suppression, and four ordered tile-change " +
            "item drops with hidden scrolling preload.");
    }
}
