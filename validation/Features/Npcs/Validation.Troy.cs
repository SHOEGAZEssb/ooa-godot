using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTroyHouseRooms()
    {
        var database = new TroyHouseDatabase();
        TroyHouseRecord record = database.Record;
        string firstChoiceMessage = database.ComposeMessage(
            firstTalk: true, choice: 0);
        if (firstChoiceMessage.Contains("\\n", StringComparison.Ordinal) ||
            !firstChoiceMessage.Contains(
                "hear me speak.\nJust between us,", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Troy's TX_2c11 fallthrough did not resolve its trailing newline command.");
        }
        var warps = new WarpDatabase();
        OracleRoomData exterior = _world.LoadRoom(0, 0x45);
        OracleRoomData interior = _world.LoadRoom(3, 0xfb);
        Vector2 exteriorDoor = PackedPosition(0x32);

        if (exterior.GetMetatile(exteriorDoor) != 0xdf ||
            !warps.TryGetTileWarp(0, 0x45, 0x32, 0xdf, out Warp entry) ||
            entry is not
            {
                SourcePosition: -1, EdgeMask: 0, SourceTransition: 4,
                DestinationGroup: 3, DestinationRoom: 0xfb,
                DestinationPosition: 0xff, DestinationParameter: 9,
                DestinationTransition: 3
            } ||
            !warps.TryGetEdgeWarp(
                3, 0xfb, Vector2I.Down, new Vector2(0x50, interior.Height + 2),
                new Vector2(interior.Width, interior.Height), out Warp exit) ||
            exit is not
            {
                SourcePosition: -1, EdgeMask: 4, SourceTransition: 3,
                DestinationGroup: 0, DestinationRoom: 0x45,
                DestinationPosition: 0x52, DestinationParameter: 0,
                DestinationTransition: 1
            })
        {
            throw new InvalidOperationException(
                "Rooms 0:45/3:fb did not retain their wildcard waterfall entry and left-half bottom exit.");
        }

        var root = new Node { Name = "TroyHouseRoomValidation" };
        AddChild(root);
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        using var managerFixture = RoomEntityValidationFixture.ForRoot(
            root, new() { SaveData = save });
        RoomEntityManager manager = managerFixture.Manager;
        var referenceRandom = new OracleRandom();

        referenceRandom.BeginRoomParse();
        manager.LoadRoom(0, exterior);
        NpcCharacter boy = manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x3f, SubId: 0x01 });
        if (boy.Active ||
            boy.Position != new Vector2(0x48, 0x58) ||
            boy.TextId != 0x2903 ||
            manager.RandomCalls != 256)
        {
            throw new InvalidOperationException(
                "Room 0:45 did not load its dormant $3f:$01/TX_2903 placement without extra RNG.");
        }

        if (save.WriteWramByte(0xc6bf, 0x40))
            save.CommitInventoryChange();
        _player.WarpTo(boy.Position + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        if (!boy.Active || manager.FindTalkTarget(_player) != boy)
        {
            throw new InvalidOperationException(
                "getGameProgress_1 state $03 did not reveal room 0:45's talkable boy.");
        }
        save.SetGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame);
        if (boy.Active)
        {
            throw new InvalidOperationException(
                "getGameProgress_1 state $04 did not retire room 0:45's $3f:$01 boy.");
        }
        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);

        referenceRandom.BeginRoomParse();
        manager.LoadRoom(3, interior);
        NpcCharacter troy = manager.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0xca, SubId: 0x01 });
        _player.WarpTo(troy.Position + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        if (!troy.Active ||
            troy.Position != new Vector2(0x28, 0x38) ||
            troy.Record.DefaultAnimation != 4 ||
            troy.Record.CanFace ||
            manager.FindTalkTarget(_player) != troy ||
            manager.RandomCalls != 512)
        {
            throw new InvalidOperationException(
                "Room 3:fb did not initialize Troy $ca:$01 as a solid script-sensitive NPC.");
        }

        int firstChoice = referenceRandom.Next().Value & record.RandomMask;
        if (!manager.BeginNpcTalk(troy) ||
            troy.TextId != record.FirstTextId ||
            troy.Message != database.ComposeMessage(firstTalk: true, firstChoice) ||
            manager.RandomCalls != 513 ||
            save.HasRoomFlag(3, 0xfb, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Troy's first talk did not consume one shared RNG value and defer room flag $40.");
        }
        manager.EndNpcTalk(troy);
        if (!save.HasRoomFlag(3, 0xfb, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "Closing Troy's first TX_2c11 dialogue did not set room 3:fb flag $40.");
        }

        int repeatChoice = referenceRandom.Next().Value & record.RandomMask;
        if (!manager.BeginNpcTalk(troy) ||
            troy.TextId != record.RepeatTextId ||
            troy.Message != database.ComposeMessage(firstTalk: false, repeatChoice) ||
            manager.RandomCalls != 514)
        {
            throw new InvalidOperationException(
                "Troy's repeat talk did not use TX_2c12 and the next shared RNG substitution.");
        }
        manager.EndNpcTalk(troy);
        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        if (troy.Active)
        {
            throw new InvalidOperationException(
                "GLOBALFLAG_FINISHEDGAME did not delete Troy $ca:$01 from room 3:fb.");
        }

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();

        // Exercise the canonical room pair through the live transition and
        // InteractionController paths in addition to the isolated data test.
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame, value: false);
        _saveData.SetGlobalFlag(
            OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame, value: false);
        _saveData.SetRoomFlag(3, 0xfb, OracleSaveData.RoomFlag40, value: false);
        if (_saveData.WriteWramByte(0xc6bf, 0x40))
            _saveData.CommitInventoryChange();
        LoadValidationRoom(0, 0x45);
        NpcCharacter liveBoy = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0x3f, SubId: 0x01 });
        if (!liveBoy.Active)
        {
            throw new InvalidOperationException(
                "Canonical room 0:45 did not expose its state-$03 boy.");
        }

        _player.WarpTo(exteriorDoor);
        if (!CheckTileWarp(_player) ||
            _activeGroup != 3 || _currentRoom.Id != 0xfb ||
            !IsTransitioning ||
            _player.Position != new Vector2(0x50, interior.Height))
        {
            throw new InvalidOperationException(
                "Room 0:45's $df waterfall did not begin the source transition-4 entry into 3:fb.");
        }
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        if (!IsTransitioning ||
            _player.Position != new Vector2(0x50, interior.Height - WarpEnterFrames))
        {
            throw new InvalidOperationException(
                "Room 3:fb entry did not complete its 28-update upward walk.");
        }
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("Room 3:fb entry fade did not finish on update 32.");

        NpcCharacter liveTroy = _entities.Entities<NpcCharacter>().Single(npc =>
            npc.Record is { Id: 0xca, SubId: 0x01 });
        _player.WarpTo(liveTroy.Position + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        int liveRandomCalls = _entities.RandomCalls;
        if (!_interactions.TryInteract(_player) ||
            !_dialogue.IsOpen ||
            liveTroy.TextId != record.FirstTextId ||
            _entities.RandomCalls != liveRandomCalls + 1 ||
            !Enumerable.Range(0, 16).Any(choice =>
                _dialogue.CurrentMessage ==
                DialogueBox.PlainText(database.ComposeMessage(true, choice))) ||
            _dialogue.CurrentMessage.Contains("\\n", StringComparison.Ordinal) ||
            _dialogue.GlyphCodeForValidation(0, 12, 0) != 'J' ||
            _saveData.HasRoomFlag(3, 0xfb, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "The live Troy A-button path did not open a source-valid first animal story.");
        }
        _dialogue.Close();
        _interactions.Update(0, _player);
        if (!_saveData.HasRoomFlag(3, 0xfb, OracleSaveData.RoomFlag40))
        {
            throw new InvalidOperationException(
                "The live dialogue close boundary did not commit Troy's room flag $40.");
        }

        liveRandomCalls = _entities.RandomCalls;
        if (!_interactions.TryInteract(_player) ||
            liveTroy.TextId != record.RepeatTextId ||
            _entities.RandomCalls != liveRandomCalls + 1 ||
            !Enumerable.Range(0, 16).Any(choice =>
                _dialogue.CurrentMessage ==
                DialogueBox.PlainText(database.ComposeMessage(false, choice))))
        {
            throw new InvalidOperationException(
                "The live Troy repeat path did not open a source-valid TX_2c12 animal story.");
        }
        _dialogue.Close();
        _interactions.Update(0, _player);

        _player.WarpTo(new Vector2(0x50, interior.Height + 2));
        _player.Face(Vector2I.Down);
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 3 || _currentRoom.Id != 0xfb)
        {
            throw new InvalidOperationException(
                "Room 3:fb's left-half bottom edge did not begin source transition 3.");
        }
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x45 || !IsTransitioning)
        {
            throw new InvalidOperationException(
                "Room 3:fb did not load exterior 0:45 after its 16-update exit walk.");
        }
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (IsTransitioning ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x62)
        {
            throw new InvalidOperationException(
                "Room 3:fb's exit did not step below exterior 0:45/$52 after its fade.");
        }

        _saveData.SetRoomFlag(3, 0xfb, OracleSaveData.RoomFlag40, value: false);
        if (_saveData.WriteWramByte(0xc6bf, 0))
            _saveData.CommitInventoryChange();

        GD.Print("Validated rooms 0:45/3:fb: exact $3f:$01 progress gate, " +
            "$ca:$01 lifetime, first/repeat room-$40 dialogue, all 16 shared-RNG " +
            "animal substitutions, and the bidirectional waterfall warp.");
    }

    private static Vector2 PackedPosition(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);
}
