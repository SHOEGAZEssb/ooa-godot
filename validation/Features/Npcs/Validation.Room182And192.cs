using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRooms182And192NpcInteractions()
    {
        const double frame = 1.0 / 60.0;
        var root = new Node { Name = "Rooms182And192NpcValidation" };
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
            1, 0x82, () => tick, () => tick = 0, save);
        var treasures = new TreasureDatabase();
        var inventory = new InventoryState(
            treasures, save, () => rooms.CurrentDungeonIndex);
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

        void SetEssences(byte value)
        {
            if (save.WriteWramByte(0xc6bf, value))
                save.CommitInventoryChange();
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);
        manager.LoadRoom(1, rooms.CurrentRoom);

        List<NpcCharacter> actors = manager.Entities<NpcCharacter>();
        FailIf(
            actors.Count != 2 ||
            actors[0].BaseRecord is not
            {
                Group: 1,
                Room: 0x82,
                Id: 0x44,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x1620,
                TileBase: 8,
                Palette: 0,
                DefaultAnimation: 4,
                CanFace: false,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            actors[1].BaseRecord is not
            {
                Group: 1,
                Room: 0x82,
                Id: 0x3f,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x2910,
                TileBase: 0,
                Palette: 0,
                DefaultAnimation: 2,
                CanFace: true,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            },
            "Room 1:82 did not retain source-ordered misc man 2 $44:$00 " +
            "followed by boy 2 $3f:$00.");

        NpcCharacter hobo = actors[0];
        NpcCharacter boy = actors[1];
        FailIf(
            hobo.Position != new Vector2(0x58, 0x38) ||
            !hobo.Active ||
            !hobo.Visible ||
            hobo.TextId != 0x1620 ||
            PlainWords(hobo.Message) !=
                "What did the Queen want? Puzzle Seeds? Enigma Seeds? " +
                "It's a mystery to me!" ||
            hobo.FacingVector != Vector2I.Down ||
            hobo.CurrentAnimationOpaquePixels == 0 ||
            boy.Position != new Vector2(0x38, 0x48) ||
            !boy.Active ||
            !boy.Visible ||
            boy.TextId != 0x2910 ||
            boy.FacingVector != Vector2I.Down ||
            boy.CurrentAnimationOpaquePixels == 0 ||
            manager.RandomCalls != 256,
            "Room 1:82 did not load both ordinary NPCs with their exact " +
            "positions, initial visuals, texts, and one room RNG buffer.");

        FailIf(
            hobo.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            hobo.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            boy.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            boy.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            !manager.BlocksLink(hobo.Position) ||
            !manager.BlocksLink(boy.Position) ||
            manager.BlocksLink(hobo.Position + Vector2.Down * 12.0f) ||
            manager.BlocksLink(boy.Position + Vector2.Right * 12.0f),
            "Room 1:82's NPCs did not retain ordinary $06/$06 collision " +
            "and the strict 12-pixel Link boundary.");

        ulong hoboFirstPose = hobo.CurrentAnimationPixelHash;
        _player.WarpTo(new Vector2(0x18, 0x70), recordSafe: false);
        for (int update = 0; update < 15; update++)
            manager.Update(frame, _player);
        FailIf(
            hobo.CurrentAnimationFrame != 0 ||
            hobo.FacingVector != Vector2I.Down,
            "Room 1:82's misc man advanced animation $04 early or faced Link.");
        manager.Update(frame, _player);
        FailIf(
            hobo.CurrentAnimationFrame != 1 ||
            hobo.CurrentAnimationPixelHash == hoboFirstPose ||
            hobo.FacingVector != Vector2I.Down,
            "Room 1:82's misc man did not reach animation $04's second " +
            "fixed pose after exactly 16 updates.");
        for (int update = 0; update < 16; update++)
            manager.Update(frame, _player);
        FailIf(
            hobo.CurrentAnimationFrame != 0 ||
            hobo.CurrentAnimationPixelHash != hoboFirstPose ||
            manager.RandomCalls != 256,
            "Room 1:82's misc-man animation did not loop at 32 updates or " +
            "ordinary NPC updates consumed RNG.");

        _player.WarpTo(
            boy.Position + Vector2.Left * 20.0f,
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            boy.FacingVector != Vector2I.Left,
            "Room 1:82's boy did not face Link to his left.");
        _player.WarpTo(
            boy.Position + Vector2.Right * 20.0f,
            recordSafe: false);
        for (int update = 0; update < 29; update++)
            manager.Update(frame, _player);
        FailIf(
            boy.FacingVector != Vector2I.Left,
            "Room 1:82's boy changed direction before the source " +
            "$1d-to-$00 facing cooldown elapsed.");
        manager.Update(frame, _player);
        FailIf(
            boy.FacingVector != Vector2I.Right,
            "Room 1:82's boy did not face Link to his right on the 30th " +
            "following update.");

        _player.WarpTo(
            hobo.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !hobo.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            !dialogue.IsOpen ||
            PlainWords(dialogue.CurrentMessage) !=
                "What did the Queen want? Puzzle Seeds? Enigma Seeds? " +
                "It's a mystery to me!",
            "Room 1:82's misc man did not run the pre-flag TX_1620 loop.");
        int frozenFrame = hobo.CurrentAnimationFrame;
        manager.Update(16.0 / 60.0, _player);
        FailIf(
            hobo.CurrentAnimationFrame != frozenFrame,
            "Gameplay text did not freeze room 1:82's misc-man animation.");
        dialogue.Close();
        interactions.Update(frame, _player);

        _player.WarpTo(
            boy.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !boy.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            PlainWords(dialogue.CurrentMessage) !=
                "Deep in the woods are magical seeds and an owl statue. " +
                "When the seeds are placed on the statue, it moves! The " +
                "first time I saw it, it startled me, but I now find it " +
                "amusing! But I'm not supposed to go into the woods, so " +
                "don't tell the adults!",
            "Room 1:82's boy did not run his TX_2910 A-button loop.");
        dialogue.Close();
        interactions.Update(frame, _player);

        SetEssences(0xff);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        FailIf(
            !hobo.Active ||
            hobo.TextId != 0x1620 ||
            !boy.Active ||
            boy.TextId != 0x2910,
            "Unrelated essence, global, or room flags changed room 1:82.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            !hobo.Active ||
            !hobo.Visible ||
            hobo.TextId != 0x1621 ||
            hobo.BaseRecord.TextId != 0x1620 ||
            PlainWords(hobo.Message) !=
                "That's it! The Queen wanted them Puzzle Seeds! I bet " +
                "she was happy!" ||
            boy.Active ||
            boy.Visible ||
            boy.CanTalkTo(_player) ||
            manager.BlocksLink(boy.Position),
            "GLOBALFLAG_0b did not select misc-man TX_1621 and delete " +
            "room 1:82's boy live.");

        _player.WarpTo(
            hobo.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !interactions.TryInteract(_player) ||
            PlainWords(dialogue.CurrentMessage) !=
                "That's it! The Queen wanted them Puzzle Seeds! I bet " +
                "she was happy!",
            "Room 1:82's misc man did not run post-flag TX_1621.");
        dialogue.Close();
        interactions.Update(frame, _player);

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        FailIf(
            !hobo.Active ||
            hobo.TextId != 0x1620 ||
            !boy.Active ||
            boy.TextId != 0x2910,
            "Clearing GLOBALFLAG_0b did not restore room 1:82's first phase.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        FailIf(
            hobo.Active ||
            hobo.Visible ||
            boy.Active ||
            boy.Visible ||
            manager.BlocksLink(hobo.Position) ||
            manager.BlocksLink(boy.Position),
            "GLOBALFLAG_FINISHEDGAME did not delete both room 1:82 NPCs.");
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);
        FailIf(
            !hobo.Active ||
            hobo.TextId != 0x1620 ||
            !boy.Active ||
            boy.TextId != 0x2910,
            "Clearing GLOBALFLAG_FINISHEDGAME did not restore room 1:82.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x92));
        NpcCharacter pastGuy = manager.Entities<NpcCharacter>().Single();
        FailIf(
            pastGuy.BaseRecord is not
            {
                Group: 1,
                Room: 0x92,
                Id: 0x43,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x1710,
                TileBase: 0x0c,
                Palette: 2,
                DefaultAnimation: 4,
                CanFace: false,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            pastGuy.Position != new Vector2(0x58, 0x28) ||
            !pastGuy.Active ||
            !pastGuy.Visible ||
            pastGuy.TextId != 0x1710 ||
            PlainWords(pastGuy.Message) !=
                "I'm gonna find something that Queen Ambi desires so I " +
                "don't have to work at Ambi's Tower." ||
            pastGuy.FacingVector != Vector2I.Down ||
            pastGuy.CurrentAnimationOpaquePixels == 0 ||
            manager.RandomCalls != 512,
            "Room 1:92 did not load past guy $43:$00 var03 $00 at " +
            "$28,$58 with fixed animation $04 and TX_1710.");

        ulong pastGuyFirstPose = pastGuy.CurrentAnimationPixelHash;
        _player.WarpTo(new Vector2(0x18, 0x70), recordSafe: false);
        for (int update = 0; update < 16; update++)
            manager.Update(frame, _player);
        FailIf(
            pastGuy.CurrentAnimationFrame != 1 ||
            pastGuy.CurrentAnimationPixelHash == pastGuyFirstPose ||
            pastGuy.FacingVector != Vector2I.Down ||
            manager.RandomCalls != 512,
            "Room 1:92's past guy did not run fixed animation $04 at the " +
            "16-update cadence without RNG.");

        _player.WarpTo(
            pastGuy.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !pastGuy.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            PlainWords(dialogue.CurrentMessage) !=
                "I'm gonna find something that Queen Ambi desires so I " +
                "don't have to work at Ambi's Tower.",
            "Room 1:92's past guy did not run his TX_1710 A-button loop.");
        dialogue.Close();
        interactions.Update(frame, _player);

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            pastGuy.Active ||
            pastGuy.Visible ||
            pastGuy.CanTalkTo(_player) ||
            manager.BlocksLink(pastGuy.Position) ||
            pastGuy.TextId != 0x1711 ||
            pastGuy.BaseRecord.TextId != 0x1710,
            "GLOBALFLAG_0b did not live-delete room 1:92's var03-$00 " +
            "past guy while selecting immutable-base TX_1711.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        FailIf(
            !pastGuy.Active ||
            !pastGuy.Visible ||
            pastGuy.TextId != 0x1710,
            "Clearing GLOBALFLAG_0b did not restore room 1:92 TX_1710.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        manager.LoadRoom(1, _world.LoadRoom(1, 0x92));
        pastGuy = manager.Entities<NpcCharacter>().Single();
        FailIf(
            pastGuy.Active ||
            pastGuy.Visible ||
            pastGuy.Position != new Vector2(0x58, 0x28) ||
            pastGuy.BaseRecord.TextId != 0x1710 ||
            manager.RandomCalls != 768,
            "Room 1:92 finished-game re-entry did not retain the suppressed " +
            "past-guy placement record and one room-parse RNG buffer.");

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print(
            "Validated rooms 1:82/1:92: source order, exact positions and " +
            "visuals, TX_1620/TX_1621, TX_2910, TX_1710/TX_1711, fixed and " +
            "Link-facing animation, ordinary collision/talkability, textbox " +
            "freeze, RNG neutrality, live story flags, and finished-game " +
            "re-entry suppression.");
    }
}
