using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom184StoneRabbitsAndSoldier()
    {
        const double frame = 1.0 / 60.0;
        var root = new Node { Name = "Room184StoneRabbitValidation" };
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
            1, 0x84, () => tick, () => tick = 0, save);
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
        var database = new StoneRabbitDatabase();

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

        SetEssences(0x00);
        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);
        save.SetRoomFlag(
            4, 0xfc, OracleSaveData.RoomFlag80, value: false);

        manager.LoadRoom(1, rooms.CurrentRoom);
        List<NpcCharacter> actors = manager.Entities<NpcCharacter>();
        FailIf(
            actors.Count != 4 ||
            actors.Take(3).Any(rabbit =>
                rabbit.BaseRecord is not
                {
                    Group: 1,
                    Room: 0x84,
                    Id: 0x4b,
                    SubId: 0x06,
                    Var03: 0x00,
                    TextId: 0x0000,
                    Palette: 2,
                    DefaultAnimation: 0,
                    CanFace: false,
                    Implementation:
                        NpcImplementationClassification.SpecializedNative
                }) ||
            actors[3].BaseRecord is not
            {
                Group: 1,
                Room: 0x84,
                Id: 0x40,
                SubId: 0x01,
                Var03: 0x00,
                TextId: 0x5902,
                Palette: 1,
                DefaultAnimation: 2,
                CanFace: true,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            },
            "Room 1:84 did not retain three source-ordered specialized " +
            "rabbits followed by ordinary soldier $40:$01.");

        List<NpcCharacter> rabbits = actors.Take(3).ToList();
        NpcCharacter soldier = actors[3];
        Vector2[] expectedRabbitPositions =
        {
            new(0x58, 0x28),
            new(0x48, 0x40),
            new(0x68, 0x50)
        };
        FailIf(
            rabbits.Where((rabbit, index) =>
                rabbit.Position != expectedRabbitPositions[index]).Any() ||
            rabbits.Any(rabbit =>
                rabbit.Active ||
                rabbit.Visible ||
                rabbit.Record.Palette != database.Record.Palette ||
                rabbit.TextId != 0 ||
                !string.IsNullOrEmpty(rabbit.Message) ||
                rabbit.CurrentScriptAnimationSource !=
                    database.Record.Animation ||
                rabbit.CurrentAnimationOpaquePixels == 0) ||
            soldier.Position != new Vector2(0x78, 0x48) ||
            !soldier.Active ||
            !soldier.Visible ||
            PlainWords(soldier.Message) !=
                "Ambi's bombs have tremendous power!" ||
            manager.RandomCalls != 256,
            "Room 1:84's initial pre-D7 state did not suppress the three " +
            "stone rabbits, preserve their imported native presentation, " +
            "and expose soldier TX_5902 without extra RNG.");

        _player.WarpTo(
            soldier.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !soldier.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            !dialogue.IsOpen ||
            PlainWords(dialogue.CurrentMessage) !=
                "Ambi's bombs have tremendous power!",
            "Room 1:84's soldier did not run its TX_5902 generic NPC loop.");
        dialogue.Close();
        interactions.Update(frame, _player);

        SetEssences(0x40);
        FailIf(
            rabbits.Any(rabbit =>
                !rabbit.Active ||
                !rabbit.Visible ||
                rabbit.Record.Palette != 0x06 ||
                rabbit.CurrentScriptAnimationSource !=
                    database.Record.Animation ||
                rabbit.CurrentAnimationFrame != 0 ||
                rabbit.CanTalkTo(_player) ||
                !rabbit.CurrentAnimationUsesColor(database.StonePalette[1]) &&
                !rabbit.CurrentAnimationUsesColor(database.StonePalette[2]) &&
                !rabbit.CurrentAnimationUsesColor(database.StonePalette[3])),
            "D7 essence bit $40 did not restore all three room 1:84 rabbits " +
            "with animation $06, palette PALH_a2, and no dialogue.");

        NpcCharacter firstRabbit = rabbits[0];
        FailIf(
            firstRabbit.ObjectCollisionBounds.Size !=
                new Vector2(12.0f, 12.0f) ||
            firstRabbit.LinkBlockingBounds.Size !=
                new Vector2(24.0f, 24.0f) ||
            !manager.BlocksLink(firstRabbit.Position) ||
            manager.BlocksLink(
                firstRabbit.Position + Vector2.Right * 12.0f),
            "Stone rabbit $4b:$06 did not apply objectSetCollideRadius $06 " +
            "with the strict 12-pixel combined Link boundary.");

        _player.WarpTo(firstRabbit.Position, recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            _player.Position !=
                firstRabbit.Position + Vector2.Left * 12.0f ||
            firstRabbit.ZIndex != NpcCharacter.BehindLinkZIndex,
            "interactionPushLinkAwayAndUpdateDrawPriority did not separate " +
            "Link from the first stone rabbit on its horizontal tie.");

        _player.WarpTo(
            firstRabbit.Position + Vector2.Up * 20.0f,
            recordSafe: false);
        manager.Update(frame, _player);
        FailIf(
            firstRabbit.ZIndex != NpcCharacter.InFrontOfLinkZIndex,
            "Room 1:84's stone rabbit did not move in front of Link through " +
            "the native relative-priority helper.");

        ulong rabbitFrameHash = firstRabbit.CurrentAnimationPixelHash;
        int randomCalls = manager.RandomCalls;
        _player.WarpTo(new Vector2(0x18, 0x70), recordSafe: false);
        for (int update = 0; update < 127; update++)
            manager.Update(frame, _player);
        FailIf(
            rabbits.Any(rabbit =>
                rabbit.CurrentAnimationFrame != 0 ||
                rabbit.CurrentAnimationPixelHash != rabbitFrameHash) ||
            manager.RandomCalls != randomCalls,
            "Static stone-rabbit state advanced animation $06 or consumed " +
            "RNG during its native update loop.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        FailIf(
            soldier.Active ||
            soldier.Visible ||
            rabbits.Any(rabbit => !rabbit.Active || !rabbit.Visible),
            "GLOBALFLAG_FINISHEDGAME did not delete only room 1:84's soldier.");
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);

        save.SetRoomFlag(4, 0xfc, 0x7f);
        save.SetRoomFlag(4, 0xfb, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(5, 0xfc, OracleSaveData.RoomFlag80);
        FailIf(
            rabbits.Any(rabbit => !rabbit.Active || !rabbit.Visible) ||
            !soldier.Active,
            "Unrelated room-flag bits, neighboring rooms, or group aliases " +
            "changed room 1:84's living actors.");

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        FailIf(
            rabbits.Any(rabbit =>
                rabbit.Active ||
                rabbit.Visible ||
                manager.BlocksLink(rabbit.Position)) ||
            !soldier.Active,
            "Room 4:fc flag $80 did not delete only the three stone rabbits.");
        save.SetRoomFlag(
            4, 0xfc, OracleSaveData.RoomFlag80, value: false);
        FailIf(
            rabbits.Any(rabbit => !rabbit.Active || !rabbit.Visible),
            "Clearing room 4:fc flag $80 did not restore the stone rabbits live.");

        SetEssences(0x00);
        FailIf(
            rabbits.Any(rabbit => rabbit.Active || rabbit.Visible) ||
            !soldier.Active,
            "Clearing D7 essence bit $40 did not suppress only the rabbits.");
        SetEssences(0x40);

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            soldier.Active ||
            soldier.Visible ||
            rabbits.Any(rabbit => !rabbit.Active || !rabbit.Visible),
            "GLOBALFLAG_0b did not delete room 1:84's var03-$00 soldier " +
            "without affecting the rabbits.");
        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b, value: false);

        save.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
        manager.LoadRoom(1, rooms.CurrentRoom);
        actors = manager.Entities<NpcCharacter>();
        FailIf(
            actors.Count != 4 ||
            actors.Take(3).Any(rabbit =>
                rabbit.Active ||
                rabbit.Visible ||
                rabbit.Position != expectedRabbitPositions[actors.IndexOf(rabbit)]) ||
            !actors[3].Active ||
            actors[3].Position != new Vector2(0x78, 0x48) ||
            manager.RandomCalls != randomCalls + 256,
            "Room 1:84 re-entry did not retain suppressed rabbit placement " +
            "records, the surviving soldier, source order, and one room RNG buffer.");

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print(
            "Validated room 1:84's three D7-to-Veran stone rabbits and " +
            "soldier $40:$01: source order, exact positions, animation $06, " +
            "PALH_a2, $06 collision, static push/priority behavior, TX_5902, " +
            "no rabbit talk/RNG, live predicates, and re-entry suppression.");
    }
}
