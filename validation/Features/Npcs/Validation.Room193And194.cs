using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRooms193And194NpcInteractions()
    {
        const double frame = 1.0 / 60.0;
        var root = new Node { Name = "Rooms193And194NpcValidation" };
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
            1, 0x93, () => tick, () => tick = 0, save);
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
                Room: 0x93,
                Id: 0x42,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x0f00,
                TileBase: 0x1c,
                Palette: 1,
                DefaultAnimation: 4,
                CanFace: false,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            actors[1].BaseRecord is not
            {
                Group: 1,
                Room: 0x93,
                Id: 0x40,
                SubId: 0x01,
                Var03: 0x01,
                TextId: 0x5902,
                TileBase: 0,
                Palette: 1,
                DefaultAnimation: 2,
                CanFace: true,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            },
            "Room 1:93 did not retain the source-ordered mustache man " +
            "$42:$00 followed by soldier $40:$01 var03 $01.");

        NpcCharacter mustacheMan = actors[0];
        NpcCharacter soldier = actors[1];
        FailIf(
            mustacheMan.Position != new Vector2(0x58, 0x38) ||
            !mustacheMan.Active ||
            !mustacheMan.Visible ||
            mustacheMan.TextId != 0x0f00 ||
            PlainWords(mustacheMan.Message) !=
                "Somewhere in the woods is a tree that bears very special seeds." ||
            mustacheMan.FacingVector != Vector2I.Down ||
            mustacheMan.CurrentAnimationOpaquePixels == 0 ||
            soldier.Position != new Vector2(0x38, 0x38) ||
            soldier.Active ||
            soldier.Visible ||
            manager.RandomCalls != 256,
            "Room 1:93's pre-GLOBALFLAG_0b phase did not expose only " +
            "mustache-man TX_0f00 at $38,$58 without extra RNG.");

        FailIf(
            mustacheMan.ObjectCollisionBounds.Size !=
                new Vector2(12.0f, 12.0f) ||
            mustacheMan.LinkBlockingBounds.Size !=
                new Vector2(24.0f, 24.0f) ||
            !manager.BlocksLink(mustacheMan.Position) ||
            manager.BlocksLink(
                mustacheMan.Position + Vector2.Down * 12.0f),
            "Room 1:93's mustache man did not retain ordinary $06/$06 " +
            "collision and its strict 12-pixel Link boundary.");

        ulong mustacheFirstPose = mustacheMan.CurrentAnimationPixelHash;
        _player.WarpTo(new Vector2(0x18, 0x70), recordSafe: false);
        for (int update = 0; update < 15; update++)
            manager.Update(frame, _player);
        FailIf(
            mustacheMan.CurrentAnimationFrame != 0 ||
            mustacheMan.FacingVector != Vector2I.Down,
            "Mustache-man animation $04 advanced early or faced Link.");
        manager.Update(frame, _player);
        FailIf(
            mustacheMan.CurrentAnimationFrame != 1 ||
            mustacheMan.CurrentAnimationPixelHash == mustacheFirstPose ||
            mustacheMan.FacingVector != Vector2I.Down,
            "Mustache-man animation $04 did not reach its second fixed pose " +
            "after exactly 16 updates.");
        for (int update = 0; update < 16; update++)
            manager.Update(frame, _player);
        FailIf(
            mustacheMan.CurrentAnimationFrame != 0 ||
            mustacheMan.CurrentAnimationPixelHash != mustacheFirstPose ||
            manager.RandomCalls != 256,
            "Mustache-man animation $04 did not loop at 32 updates or " +
            "its ordinary update consumed RNG.");

        _player.WarpTo(
            mustacheMan.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !mustacheMan.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            !dialogue.IsOpen ||
            PlainWords(dialogue.CurrentMessage) !=
                "Somewhere in the woods is a tree that bears very special seeds.",
            "Room 1:93's mustache man did not run the TX_0f00 A-button loop.");
        int frozenFrame = mustacheMan.CurrentAnimationFrame;
        manager.Update(16.0 / 60.0, _player);
        FailIf(
            mustacheMan.CurrentAnimationFrame != frozenFrame,
            "Gameplay text did not freeze mustache-man animation $04.");
        dialogue.Close();
        interactions.Update(frame, _player);

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            !mustacheMan.Active ||
            mustacheMan.TextId != 0x0f01 ||
            PlainWords(mustacheMan.Message) !=
                "There are trees in other places that bear Mystical Seeds." ||
            mustacheMan.BaseRecord.TextId != 0x0f00 ||
            !soldier.Active ||
            !soldier.Visible ||
            soldier.TextId != 0x5901 ||
            PlainWords(soldier.Message) != "Did you find what Ambi desires?" ||
            soldier.BaseRecord.TextId != 0x5902,
            "GLOBALFLAG_0b did not live-select mustache-man TX_0f01 and " +
            "restore room 1:93's soldier with TX_5901.");

        _player.WarpTo(
            soldier.Position + Vector2.Left * 20.0f,
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            soldier.FacingVector != Vector2I.Left ||
            !manager.BlocksLink(soldier.Position),
            "Room 1:93's soldier did not run npcFaceLinkAndAnimate with " +
            "ordinary solidity.");
        _player.WarpTo(
            soldier.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !soldier.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            PlainWords(dialogue.CurrentMessage) !=
                "Did you find what Ambi desires?",
            "Room 1:93's late soldier did not run its TX_5901 dialogue.");
        dialogue.Close();
        interactions.Update(frame, _player);

        SetEssences(0xff);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        FailIf(
            !mustacheMan.Active ||
            mustacheMan.TextId != 0x0f01 ||
            !soldier.Active ||
            soldier.TextId != 0x5901,
            "Unrelated essence, global, or room flags changed room 1:93.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        FailIf(
            mustacheMan.Active ||
            mustacheMan.Visible ||
            soldier.Active ||
            soldier.Visible ||
            manager.BlocksLink(mustacheMan.Position) ||
            manager.BlocksLink(soldier.Position),
            "GLOBALFLAG_FINISHEDGAME did not delete both room 1:93 actors.");
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);
        FailIf(
            !mustacheMan.Active ||
            mustacheMan.TextId != 0x0f01 ||
            !soldier.Active ||
            soldier.TextId != 0x5901,
            "Clearing GLOBALFLAG_FINISHEDGAME did not restore room 1:93 live.");

        manager.LoadRoom(1, _world.LoadRoom(1, 0x94));
        actors = manager.Entities<NpcCharacter>();
        NpcCharacter pastGuy = actors.Single();
        FailIf(
            pastGuy.BaseRecord is not
            {
                Group: 1,
                Room: 0x94,
                Id: 0x43,
                SubId: 0x00,
                Var03: 0x01,
                TextId: 0x1710,
                TileBase: 0x0c,
                Palette: 2,
                DefaultAnimation: 4,
                CanFace: false,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            pastGuy.Position != new Vector2(0x68, 0x28) ||
            !pastGuy.Active ||
            !pastGuy.Visible ||
            pastGuy.TextId != 0x1711 ||
            PlainWords(pastGuy.Message) !=
                "I wonder what Queen Ambi will want next..." ||
            pastGuy.FacingVector != Vector2I.Down ||
            manager.RandomCalls != 512,
            "Room 1:94 did not load past guy $43:$00 var03 $01 at " +
            "$28,$68 with immutable TX_1710 and live TX_1711.");

        ulong pastGuyFirstPose = pastGuy.CurrentAnimationPixelHash;
        _player.WarpTo(new Vector2(0x18, 0x70), recordSafe: false);
        for (int update = 0; update < 16; update++)
            manager.Update(frame, _player);
        FailIf(
            pastGuy.CurrentAnimationFrame != 1 ||
            pastGuy.CurrentAnimationPixelHash == pastGuyFirstPose ||
            pastGuy.FacingVector != Vector2I.Down ||
            manager.RandomCalls != 512,
            "Room 1:94's past guy did not run fixed animation $04 at the " +
            "16-update cadence without RNG.");

        _player.WarpTo(
            pastGuy.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !pastGuy.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            PlainWords(dialogue.CurrentMessage) !=
                "I wonder what Queen Ambi will want next...",
            "Room 1:94's past guy did not run its TX_1711 A-button loop.");
        dialogue.Close();
        interactions.Update(frame, _player);

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        FailIf(
            pastGuy.Active ||
            pastGuy.Visible ||
            pastGuy.CanTalkTo(_player) ||
            manager.BlocksLink(pastGuy.Position),
            "Clearing GLOBALFLAG_0b did not live-delete room 1:94's " +
            "var03-$01 past guy.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            !pastGuy.Active ||
            pastGuy.TextId != 0x1711 ||
            pastGuy.BaseRecord.TextId != 0x1710,
            "Restoring GLOBALFLAG_0b did not reselect room 1:94 TX_1711.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        manager.LoadRoom(1, _world.LoadRoom(1, 0x94));
        pastGuy = manager.Entities<NpcCharacter>().Single();
        FailIf(
            pastGuy.Active ||
            pastGuy.Visible ||
            pastGuy.Position != new Vector2(0x68, 0x28) ||
            pastGuy.BaseRecord.TextId != 0x1710 ||
            manager.RandomCalls != 768,
            "Room 1:94 finished-game re-entry did not retain the suppressed " +
            "past-guy record and one room-parse RNG buffer.");

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print(
            "Validated rooms 1:93/1:94: source-ordered mustache man, soldier, " +
            "and past guy; exact positions/visuals; TX_0f00/TX_0f01, " +
            "TX_5901, and TX_1711; fixed animation $04; facing, collision, " +
            "talkability, textbox freeze, RNG neutrality, live flags, and " +
            "finished-game re-entry suppression.");
    }
}
