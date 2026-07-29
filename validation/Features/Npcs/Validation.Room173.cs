using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom173SoldierPair()
    {
        const double frame = 1.0 / 60.0;
        var root = new Node { Name = "Room173SoldierValidation" };
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
            1, 0x73, () => tick, () => tick = 0, save);
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

        manager.LoadRoom(1, _world.LoadRoom(1, 0x72));
        NpcCharacter earlySoldier =
            manager.Entities<NpcCharacter>().Single();
        FailIf(
            earlySoldier.BaseRecord is not
            {
                Group: 1,
                Room: 0x72,
                Id: 0x40,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x5900,
                Palette: 1,
                DefaultAnimation: 2,
                CanFace: true,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            earlySoldier.Position != new Vector2(0x28, 0x58) ||
            !earlySoldier.Active ||
            !earlySoldier.Visible ||
            earlySoldier.CurrentAnimationOpaquePixels == 0 ||
            PlainWords(earlySoldier.Message) !=
                "Are you also searching for that which Ambi desires? " +
                "You should not go further! There are hordes of terrible beasts!",
            "Room 1:72 did not load the pre-GLOBALFLAG_0b soldier " +
            "$40:$00 var03 $00 at $58,$28 with TX_5900.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            earlySoldier.Active ||
            earlySoldier.Visible ||
            earlySoldier.CanTalkTo(_player) ||
            manager.BlocksLink(earlySoldier.Position),
            "GLOBALFLAG_0b did not live-delete room 1:72's var03-$00 soldier.");

        manager.LoadRoom(1, rooms.CurrentRoom);
        NpcCharacter lateSoldier =
            manager.Entities<NpcCharacter>().Single();
        FailIf(
            lateSoldier.BaseRecord is not
            {
                Group: 1,
                Room: 0x73,
                Id: 0x40,
                SubId: 0x00,
                Var03: 0x01,
                TextId: 0x5900,
                Palette: 1,
                DefaultAnimation: 2,
                CanFace: true,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            lateSoldier.Position != new Vector2(0x18, 0x18) ||
            !lateSoldier.Active ||
            !lateSoldier.Visible ||
            lateSoldier.TextId != 0x5901 ||
            PlainWords(lateSoldier.Message) !=
                "Did you find what Ambi desires?",
            "Room 1:73 did not select the post-GLOBALFLAG_0b soldier " +
            "$40:$00 var03 $01 at $18,$18 with TX_5901.");

        _player.WarpTo(
            lateSoldier.Position + Vector2.Left * 20.0f,
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            lateSoldier.FacingVector != Vector2I.Left,
            "Room 1:73's soldier did not face nearby Link.");

        _player.WarpTo(
            lateSoldier.Position + Vector2.Right * 20.0f,
            recordSafe: false);
        for (int update = 0; update < 29; update++)
            manager.Update(frame, _player);
        FailIf(
            lateSoldier.FacingVector != Vector2I.Left,
            "Room 1:73's soldier changed direction before the source " +
            "$1d-to-$00 facing cooldown elapsed.");
        manager.Update(frame, _player);
        FailIf(
            lateSoldier.FacingVector != Vector2I.Right,
            "Room 1:73's soldier did not change direction on the 30th " +
            "following npcFaceLinkAndAnimate update.");

        _player.WarpTo(
            lateSoldier.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !manager.BlocksLink(lateSoldier.Position) ||
            !lateSoldier.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            !dialogue.IsOpen ||
            PlainWords(dialogue.CurrentMessage) !=
                "Did you find what Ambi desires?",
            "Room 1:73's soldier was not solid and A-button sensitive " +
            "with TX_5901.");
        dialogue.Close();
        interactions.Update(frame, _player);

        save.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        FailIf(
            !lateSoldier.Active ||
            lateSoldier.TextId != 0x5901,
            "An unrelated global flag hid room 1:73's soldier or changed its text.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        FailIf(
            lateSoldier.Active ||
            lateSoldier.Visible ||
            lateSoldier.CanTalkTo(_player) ||
            manager.BlocksLink(lateSoldier.Position),
            "Clearing GLOBALFLAG_0b did not live-delete room 1:73's " +
            "var03-$01 soldier.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            !lateSoldier.Active ||
            !lateSoldier.Visible ||
            lateSoldier.TextId != 0x5901,
            "Restoring GLOBALFLAG_0b did not live-restore room 1:73's " +
            "soldier with TX_5901.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        FailIf(
            lateSoldier.Active ||
            lateSoldier.Visible ||
            lateSoldier.CanTalkTo(_player) ||
            manager.BlocksLink(lateSoldier.Position),
            "GLOBALFLAG_FINISHEDGAME did not delete room 1:73's soldier.");

        manager.LoadRoom(1, rooms.CurrentRoom);
        lateSoldier = manager.Entities<NpcCharacter>().Single();
        FailIf(
            lateSoldier.Active ||
            lateSoldier.Visible ||
            lateSoldier.BaseRecord.Var03 != 0x01 ||
            lateSoldier.Position != new Vector2(0x18, 0x18),
            "Room 1:73 finished-game re-entry did not retain the suppressed " +
            "var03-$01 soldier record.");

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print(
            "Validated rooms 1:72/1:73 paired soldier $40:$00 placements, " +
            "GLOBALFLAG_0b swap, TX_5900/TX_5901 dialogue, solidity, talkability, " +
            "30-update facing cooldown, live refresh, unrelated flags, and " +
            "finished-game re-entry suppression.");
    }
}
