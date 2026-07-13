using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class GameRoot
{
    private void ValidateAll()
    {
        _world.ValidateRepresentativeRooms();
        ValidateSaveDataFoundation();
        ValidateDebugFlagMenu();

        LoadValidationRoom(0, 0x11);
        ValidateStartupTransition();
        LoadValidationRoom(0, 0x22);
        ValidateSymmetryTransition();

        ValidateSigns();
        ValidateNpcs();
        ValidateMakuTreeDisappearanceCutscene();
        ValidateRalphPortalDepartureEvent();
        ValidateAnimations();
        ValidateSwordBush();
        ValidateKeese();
        ValidateOctoroks();
        ValidateZolsAndGels();
        ValidateItemDrops();
        ValidateTimePortals();
        ValidateHouseWarp();
        ValidateCaveWarps();
        ValidateTerrain();
        ValidateHealth();
        ValidateChests();
        ValidateInventoryFoundation();
        ValidateInventoryMenu();
        ValidateBraceletChestAndPushGate();
        ValidatePushBlocks();
        ValidateMapScreen();

        GD.Print("Validated all gameplay and world-data scenarios.");
    }

    private void ValidateSaveDataFoundation()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared) ||
            save.GetRoomFlags(0, 0x38) != 0)
        {
            throw new InvalidOperationException(
                "A standard file did not begin with clear global and room flags.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
        save.SetRoomFlag(2, 0x38, OracleSaveData.RoomFlagLayoutSwap);
        save.SetRoomFlag(6, 0x87, OracleSaveData.RoomFlagItem);
        save.SetRoomFlag(7, 0xa6, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(1, 0x41, OracleSaveData.RoomFlagPortalSpotDiscovered);
        save.SetMinimapLocation(1, 0x41);
        save.SetMakuTreeState(1);
        if (!save.HasGlobalFlag(0x0c) ||
            !save.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            !save.HasRoomFlag(4, 0x87, OracleSaveData.RoomFlagItem) ||
            !save.HasRoomFlag(5, 0xa6, OracleSaveData.RoomFlag80) ||
            !save.HasRoomFlag(3, 0x41, OracleSaveData.RoomFlagPortalSpotDiscovered) ||
            save.MinimapGroup != 1 || save.MinimapRoom != 0x41 || save.MakuTreeState != 1)
        {
            throw new InvalidOperationException(
                "Global flags, room flags, or the original 0/2, 1/3, 4/6 group aliases diverged.");
        }

        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWITCH_HOOK_00"));
        inventory.AddRupees(987);
        byte[] encoded = save.Serialize();
        if (encoded.Length != OracleSaveData.FileSize ||
            !encoded.AsSpan(2, 8).SequenceEqual("Z21216-0"u8) ||
            !OracleSaveData.TryDeserialize(encoded, out OracleSaveData? decoded))
        {
            throw new InvalidOperationException(
                "The $550-byte Ages save image did not pass its signature/checksum round trip.");
        }

        var restoredInventory = new InventoryState(_treasures, decoded!);
        if (!restoredInventory.HasTreasure(TreasureDatabase.TreasureSwitchHook) ||
            restoredInventory.SwitchHookLevel != 1 ||
            restoredInventory.EquippedB != TreasureDatabase.TreasureSwitchHook ||
            restoredInventory.Rupees != 987 || !decoded!.HasGlobalFlag(0x0c) ||
            !decoded.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap))
        {
            throw new InvalidOperationException(
                "Inventory, BCD rupees, story flags, or room flags were lost across save reload.");
        }

        encoded[^1] ^= 0x80;
        if (OracleSaveData.TryDeserialize(encoded, out _))
            throw new InvalidOperationException("A corrupted Ages save checksum was accepted.");

        GD.Print("Validated original $550-byte save signature/checksum, 128 global flags, " +
            "four aliased room-flag tables, inventory fields, and BCD rupee round trip.");
    }

    private void ValidateDebugFlagMenu()
    {
        if (!InputMap.HasAction("debug_flags"))
            throw new InvalidOperationException("The F1 debug_flags input action was not registered.");

        _debugFlagMenu.OpenImmediatelyForValidation();
        if (!_debugFlagMenu.IsActive || !_debugFlagScreen.Visible ||
            _player.IsPhysicsProcessing() || _player.IsProcessing())
        {
            throw new InvalidOperationException(
                "The debug flag menu did not open in screen space and freeze Link.");
        }

        _debugFlagScreen._Input(new InputEventKey
        {
            PhysicalKeycode = Key.Tab,
            Pressed = true
        });
        if (_debugFlagScreen.Page != DebugFlagScreen.FlagPage.Room)
            throw new InvalidOperationException(
                "A physical Tab key event did not switch the flag editor to room flags.");
        _debugFlagScreen._Input(new InputEventKey
        {
            Keycode = Key.Tab,
            Pressed = true
        });
        if (_debugFlagScreen.Page != DebugFlagScreen.FlagPage.Global)
            throw new InvalidOperationException(
                "A logical Tab key event did not switch the flag editor back to global flags.");

        _debugFlagScreen.SelectGlobalFlagForValidation(OracleSaveData.GlobalFlagIntroDone);
        if (!_debugFlagScreen.RenderedText.Contains("GLOBALFLAG_INTRO_DONE", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The imported name for GLOBALFLAG_INTRO_DONE was not displayed.");
        bool globalBefore = _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        _debugFlagScreen.ToggleSelectedFlag();
        if (_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) == globalBefore)
            throw new InvalidOperationException("The global flag editor did not toggle flag $0a.");
        _debugFlagScreen.ToggleSelectedFlag();

        const int room = 0xaa;
        const int bit = 6;
        byte mask = 1 << bit;
        bool roomBefore = _saveData.HasRoomFlag(5, room, mask);
        _debugFlagScreen.SelectRoomFlagForValidation(7, room, bit);
        if (_debugFlagScreen.SelectedRoomGroup != 5 ||
            !_debugFlagScreen.RenderedText.Contains("GENERIC_40", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The room editor did not expose group 7 through canonical table 5 and bit 6.");
        }
        _debugFlagScreen.ToggleSelectedFlag();
        if (_saveData.HasRoomFlag(5, room, mask) == roomBefore)
            throw new InvalidOperationException("The room flag editor did not toggle 5:aa bit 6.");
        _debugFlagScreen.MoveHorizontal(1);
        if (_debugFlagScreen.SelectedRoom != 0xab)
            throw new InvalidOperationException("Room flag browsing did not advance $aa -> $ab.");
        _debugFlagScreen.MoveHorizontal(-1);
        _debugFlagScreen.ToggleSelectedFlag();

        _debugFlagMenu.CloseImmediatelyForValidation();
        if (_debugFlagMenu.IsActive || _debugFlagScreen.Visible ||
            !_player.IsPhysicsProcessing() || !_player.IsProcessing())
        {
            throw new InvalidOperationException(
                "Closing the debug flag menu did not restore Link processing.");
        }

        GD.Print("Validated F1 global/room flag editor, all imported global labels, " +
            "aliased room tables, navigation, mutation, and gameplay freezing.");
    }

    private void ValidateTimePortals()
    {
        var database = new TimePortalDatabase();
        IReadOnlyList<TimePortalDatabase.PortalRecord> ordinaryRecords =
            database.GetRoomPortals(0, 0x3a);
        if (ordinaryRecords.Count != 1 || ordinaryRecords[0].SubId != 0x00 ||
            ordinaryRecords[0].X != 0x18 || ordinaryRecords[0].Y != 0x28)
        {
            throw new InvalidOperationException(
                "Room 0:3a did not preserve its ordinary `$e1:$00 portal at `$21.");
        }

        LoadValidationRoom(0, 0x3a);
        List<TimePortal> ordinaryPortals = _entities.Entities<TimePortal>();
        if (ordinaryPortals.Count != 1 || !ordinaryPortals[0].Active ||
            _currentRoom.GetMetatile(ordinaryPortals[0].Position) != 0xd7)
        {
            throw new InvalidOperationException(
                "The exposed `$d7 marker in room 0:3a did not create an active ordinary portal.");
        }

        IReadOnlyList<TimePortalDatabase.PortalRecord> records = database.GetRoomPortals(0, 0x39);
        if (records.Count != 1 || records[0].SubId != 0x01 ||
            records[0].X != 0x28 || records[0].Y != 0x28 || records[0].LoopStart != 3)
        {
            throw new InvalidOperationException(
                "Room 0:39 did not preserve its `$e1:$01 portal at `$22 with animation loop index 3.");
        }

        LoadValidationRoom(0, 0x39);
        List<TimePortal> portals = _entities.Entities<TimePortal>();
        if (portals.Count != 1 || portals[0].Active ||
            _currentRoom.GetMetatile(portals[0].Position) != 0x3a)
        {
            throw new InvalidOperationException(
                $"Room 0:39 portal initial state was count={portals.Count}, " +
                $"active={(portals.Count == 1 && portals[0].Active)}, tile=" +
                $"`${(portals.Count == 1 ? _currentRoom.GetMetatile(portals[0].Position) : 0):x2}.");
        }

        TimePortal portal = portals[0];
        if (!_currentRoom.ReplaceMetatile(portal.Position, 0x3a, 0xd7, (long)_animationTicks))
            throw new InvalidOperationException("Could not reveal portal-spot metatile `$d7 for validation.");
        _roomView.QueueRedraw();
        _entities.Update(1.0 / 60.0, _player);
        if (!portal.Active)
            throw new InvalidOperationException("The `$e1 portal did not initialize after its `$d7 spot was revealed.");
        for (int frame = 0; frame < 6; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (portal.CurrentFrame != 3)
            throw new InvalidOperationException("The portal did not finish its three 2-update intro frames.");
        for (int frame = 0; frame < 6; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (portal.CurrentFrame != 3)
            throw new InvalidOperationException("The portal's three-frame animation loop restarted incorrectly.");

        _player.WarpTo(portal.Position, recordSafe: false);
        _entities.Update(1.0 / 60.0, _player);
        if (!IsTransitioning)
            throw new InvalidOperationException("Touching the active `$e1 portal did not begin a time warp.");

        UpdateRoomWarpTransition(RoomTransitionController.TimeWarpChargeFrames / 60.0);
        UpdateRoomWarpTransition(RoomTransitionController.FastPaletteFadeFrames / 60.0);
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (_activeGroup != 1 || _currentRoom.Id != 0x39 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x22)
        {
            throw new InvalidOperationException(
                $"Time portal 0:39/`$22 landed at {_activeGroup:x1}:{_currentRoom.Id:x2}/" +
                $"`${_currentRoom.GetPackedPosition(_player.Position):x2} instead of 1:39/`$22.");
        }

        UpdateRoomWarpTransition((RoomTransitionController.TimeWarpArrivalHiddenFrames +
            RoomTransitionController.TimeWarpArrivalFlickerFrames) / 60.0);
        if (IsTransitioning || !_player.Visible)
            throw new InvalidOperationException("The time-warp arrival did not restore visible Link and control.");

        GD.Print("Validated all 21 `$e1 portal records, exposed 0:3a ordinary portal, " +
            "and active 0:39 -> 1:39 time warp at `$22.");
    }

    private void ValidateMapScreen()
    {
        if (!Mathf.IsEqualApprox(MapMenuController.FastFadeFrames, 11.0f))
            throw new InvalidOperationException("The map menu must use the 11-update fast palette fade.");

        LoadValidationRoom(0, 0x11);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapScreen.MapMode.Present || _mapScreen.CursorRoom != 0x11)
            throw new InvalidOperationException(
                $"Present map should open at room 11, got {_mapScreen.Mode} / {_mapScreen.CursorRoom:x2}.");
        if (_player.IsPhysicsProcessing() || _player.IsProcessing())
            throw new InvalidOperationException("Link continued updating while the map menu was open.");

        _mapScreen.MoveOverworldCursor(Vector2I.Left);
        _mapScreen.MoveOverworldCursor(Vector2I.Left);
        if (_mapScreen.CursorRoom != 0x1d)
            throw new InvalidOperationException(
                $"The 14-column overworld cursor did not wrap 11 -> 10 -> 1d; got {_mapScreen.CursorRoom:x2}.");

        if (!_mapScreen.LocationArrowVisible)
            throw new InvalidOperationException("The map location arrow was hidden on frame 0.");
        _mapScreen.Update(32.0 / 60.0);
        if (_mapScreen.LocationArrowVisible)
            throw new InvalidOperationException("The map location arrow did not toggle after 32 updates.");
        _mapScreen.Update(32.0 / 60.0);
        if (!_mapScreen.LocationArrowVisible)
            throw new InvalidOperationException("The map location arrow did not restore after 64 updates.");
        _mapMenu.CloseImmediatelyForValidation();
        if (!_player.IsPhysicsProcessing() || !_player.IsProcessing())
            throw new InvalidOperationException("Link processing was not restored after closing the map.");

        LoadValidationRoom(1, 0x11);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapScreen.MapMode.Past || _mapScreen.CursorRoom != 0x11)
            throw new InvalidOperationException(
                $"Past map should open at room 11, got {_mapScreen.Mode} / {_mapScreen.CursorRoom:x2}.");
        _mapMenu.CloseImmediatelyForValidation();

        DungeonMapDatabase.DungeonInfo dungeon = _rooms.DungeonMaps.GetDungeon(0x0d);
        if (!dungeon.TryGetRoom(0x09, out DungeonMapDatabase.DungeonCell cell) ||
            cell.Floor != 0 || cell.X != 2 || cell.Y != 2)
        {
            throw new InvalidOperationException(
                "Dungeon 0d room 09 was not imported at floor 0, cell (2,2).");
        }
        LoadValidationRoom(4, 0x09);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapScreen.MapMode.Dungeon || _mapScreen.DisplayedDungeonFloor != 0)
            throw new InvalidOperationException("Dungeon 0d map did not open on room 09's floor.");
        _mapMenu.CloseImmediatelyForValidation();

        _mapMenu.OpenDebugImmediatelyForValidation();
        if (!_mapScreen.DebugFastTravel || _mapScreen.Mode != MapScreen.MapMode.Past)
            throw new InvalidOperationException(
                "Debug fast travel did not retain the most recent past-overworld map from dungeon 0d.");
        _mapScreen.ToggleDebugWorld();
        if (_mapScreen.Mode != MapScreen.MapMode.Present)
            throw new InvalidOperationException("Debug fast travel did not switch from past to present.");
        _mapScreen.MoveOverworldCursor(Vector2I.Right);
        if (!_mapScreen.TryGetFastTravelTarget(out int group, out int room) ||
            group != 0 || room != 0x12)
        {
            throw new InvalidOperationException(
                $"Expected present fast-travel target 0:12, got {group}:{room:x2}.");
        }
        if (!_mapMenu.BeginTravelToSelectionForValidation())
            throw new InvalidOperationException("Debug map rejected the valid present target 0:12.");
        for (int frame = 0; frame < MapMenuController.FastFadeFrames - 1; frame++)
        {
            _mapMenu.Update(1.0 / 60.0);
            if (_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0x09)
                throw new InvalidOperationException("Debug fast travel loaded before the fade reached white.");
        }
        _mapMenu.Update(1.0 / 60.0);
        if (_rooms.ActiveGroup != 0 || _rooms.CurrentRoom.Id != 0x12)
        {
            throw new InvalidOperationException(
                "Debug map fast travel did not load present room 0:12 at full white.");
        }
        for (int frame = 0; frame < MapMenuController.FastFadeFrames; frame++)
            _mapMenu.Update(1.0 / 60.0);
        if (_mapMenu.IsActive || !_player.IsPhysicsProcessing() || !_player.IsProcessing())
            throw new InvalidOperationException("Debug fast travel did not restore gameplay processing.");

        GD.Print("Validated original present/past/dungeon map tilemaps, 14x14 cursor wrapping, " +
            "32-update marker blink, 11-update fast fades, Link input freezing, and " +
            "dungeon-to-overworld debug fast travel.");
    }

    private void LoadValidationRoom(int group, int room)
    {
        LoadDebugRoom(group, room);
        _player.WarpTo(FindSpawn());
        _player.Face(Vector2I.Down);
    }

    private void ValidatePushBlocks()
    {
        OracleRoomData directionalRoom = _rooms.Load(4, 0x09);
        _roomView.SetRoom(directionalRoom.Texture);
        _entities.LoadRoom(4, directionalRoom);

        // The left dungeon wall at this position sets the original $0c
        // adjacent-wall mask. Link should use the pushing walk variant only
        // while the held direction matches the side he is facing.
        _player.WarpTo(new Vector2(20, 24));
        _player.Face(Vector2I.Left);
        _player.UpdatePushingState(Vector2.Left);
        if (!_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link did not enter his pushing animation against the ordinary wall in 4:09.");
        }
        _player.UpdatePushingState(Vector2.Right);
        if (_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link retained his pushing animation without holding toward the wall.");
        }

        Vector2 rightOnlyBlock = new(0x0b * 16 + 8, 0x01 * 16 + 8);
        Vector2 linkAbove = rightOnlyBlock + new Vector2(0, -10);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkAbove, Vector2I.Down, Vector2.Down);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames !=
            PushBlockController.PushDelayFrames ||
            directionalRoom.GetMetatile(rightOnlyBlock) != 0x19 ||
            directionalRoom.GetCollision(
                directionalRoom.GetMetatile(rightOnlyBlock + Vector2.Down * 16)) != 0)
        {
            throw new InvalidOperationException(
                "Right-only block $19 accepted a downward push toward clear floor in 4:09.");
        }

        OracleRoomData room = _rooms.Load(4, 0x08);
        _roomView.SetRoom(room.Texture);
        _entities.LoadRoom(4, room);

        // Dungeon 0 room $08 has an all-direction tile $1c at packed
        // position $4b. Its right neighbor is the solid one-way block $19,
        // while the tile above is clear floor, so both rejection and a full
        // successful push can be checked against unmodified original data.
        Vector2 blockCenter = new(0x0b * 16 + 8, 0x04 * 16 + 8);
        Vector2 linkBelow = blockCenter + new Vector2(0, 10);
        Vector2 linkLeft = blockCenter + new Vector2(-10, 0);
        Vector2 linkRight = blockCenter + new Vector2(10, 0);
        if (room.ActiveCollisions != 2 || room.GetMetatile(blockCenter) != 0x1c)
        {
            throw new InvalidOperationException(
                $"Expected dungeon collision mode 2 and block $1c at 4:08/$4b, got " +
                $"mode {room.ActiveCollisions} / tile ${room.GetMetatile(blockCenter):x2}.");
        }

        _player.WarpTo(linkBelow);
        _player.Face(Vector2I.Up);
        _player.UpdatePushingState(Vector2.Up);
        if (!_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link did not enter his pushing animation while pressing block $1c in 4:08.");
        }

        Vector2 cornerApproach = new(blockCenter.X - 6, linkBelow.Y);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                cornerApproach, Vector2I.Up, Vector2.Up);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames !=
            PushBlockController.PushDelayFrames)
        {
            throw new InvalidOperationException(
                "Block $1c accepted a push while Link occupied a metatile corner.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkRight, Vector2I.Left, Vector2.Left);
        }
        for (int frame = 0; frame < PushBlockController.MoveFrames; frame++)
        {
            _pushBlocks.Advance(1.0 / 60.0);
        }
        Vector2 holeCenter = blockCenter + Vector2.Left * 16;
        if (_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            room.GetMetatile(holeCenter) != 0xf5)
        {
            throw new InvalidOperationException(
                "Block $1c did not disappear while preserving destination hole $f5.");
        }
        if (!room.ReplaceMetatile(blockCenter, 0xa0, 0x1c, (long)_animationTicks))
            throw new InvalidOperationException("Could not restore 4:08/$4b after the hole test.");

        _pushBlocks.UpdatePushAttempt(linkLeft, Vector2I.Right, Vector2.Right);
        if (_pushBlocks.RemainingPushFrames != PushBlockController.PushDelayFrames ||
            _pushBlocks.Active)
        {
            throw new InvalidOperationException(
                "Block $1c started moving right even though destination tile $19 is solid.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames - 1; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkBelow, Vector2I.Up, Vector2.Up);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 1 ||
            room.GetMetatile(blockCenter) != 0x1c)
        {
            throw new InvalidOperationException(
                "Block $1c moved before Link completed the original 20-update push delay.");
        }

        _pushBlocks.UpdatePushAttempt(linkBelow, Vector2I.Up, Vector2.Zero);
        if (_pushBlocks.RemainingPushFrames != PushBlockController.PushDelayFrames)
        {
            throw new InvalidOperationException(
                "Releasing the direction did not reset wPushingAgainstTileCounter to 20.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkBelow, Vector2I.Up, Vector2.Up);
        }
        if (!_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            !_collision.Collides(blockCenter + new Vector2(0, -2)))
        {
            throw new InvalidOperationException(
                "Block $1c did not become a Link-blocking object over source floor $a0.");
        }

        for (int frame = 0; frame < PushBlockController.MoveFrames - 1; frame++)
        {
            _pushBlocks.Advance(1.0 / 60.0);
        }
        Vector2 expectedTopLeft = new(blockCenter.X - 8, blockCenter.Y - 8 - 15.5f);
        if (!_pushBlocks.Active ||
            !_pushBlocks.BlockTopLeft.IsEqualApprox(expectedTopLeft) ||
            room.GetMetatile(blockCenter + Vector2.Up * 16) != 0xa0)
        {
            throw new InvalidOperationException(
                "Block $1c did not move at SPEED_80 for the first 31 updates.");
        }

        _pushBlocks.Advance(1.0 / 60.0);
        if (_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            room.GetMetatile(blockCenter + Vector2.Up * 16) != 0x1d)
        {
            throw new InvalidOperationException(
                "Block $1c did not finish after 32 updates as destination tile $1d.");
        }

        GD.Print("Validated Link's wall/block pushing animation, directional/corner " +
            "restrictions, hazard disposal, 4:08/$4b push delay reset, blocked " +
            "destination, source $a0 replacement, SPEED_80 movement, moving " +
            "collision, and destination tile $1d.");
    }

    private void ValidateChests()
    {
        WarpToChestTest();
        const int chestPosition = 0x51;
        Vector2 chestPoint = new(24, 88);
        if (_activeGroup != 0 || _currentRoom.Id != 0x49 ||
            _currentRoom.GetPackedPosition(chestPoint) != chestPosition ||
            _currentRoom.GetMetatile(chestPoint) != 0xf1)
        {
            throw new InvalidOperationException(
                "The canonical 0:49/$51 30-rupee chest was not available for testing.");
        }

        Image roomImage = _currentRoom.Texture.GetImage();
        int redChestPixels = 0;
        for (int y = 80; y < 96; y++)
        for (int x = 16; x < 32; x++)
        {
            Color pixel = roomImage.GetPixel(x, y);
            if (pixel.R > 0.5f && pixel.G < 0.2f && pixel.B < 0.25f)
                redChestPixels++;
        }
        if (redChestPixels == 0)
        {
            throw new InvalidOperationException(
                "Chest $51 did not render with PALH_0f background palette 0.");
        }

        _player.WarpTo(new Vector2(24, 74));
        _player.Face(Vector2I.Down);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "It won't open\nfrom this side!")
        {
            throw new InvalidOperationException("Chest $51 did not use TX_510d from the wrong side.");
        }
        _dialogue.Close();

        _player.WarpTo(new Vector2(24, 100));
        _player.Face(Vector2I.Up);
        int rupeesBefore = _player.Rupees;
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0)
        {
            throw new InvalidOperationException("Chest $51 did not open from below into tile $f0.");
        }

        _interactions.Update(31.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore)
            throw new InvalidOperationException("The chest reward completed before its 32-frame rise.");
        _interactions.Update(1.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore + 30 ||
            !_dialogue.IsOpen || _dialogue.CurrentMessage != "You got\n30 Rupees!\nThat's nice.")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_RUPEES_04 did not remain visible while showing TX_0005.");
        }

        _dialogue.Close();
        if (!_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward disappeared before its textbox closed.");
        _interactions.Update(0.0, _player);
        if (_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward remained after its textbox closed.");

        _currentRoom = _world.LoadRoom(0, 0x49);
        _roomView.SetRoom(_currentRoom.Texture);
        if (_currentRoom.GetMetatile(chestPoint) != 0xf0 || TryInteract(_player))
            throw new InvalidOperationException("The opened chest room flag did not persist for the session.");

        GD.Print("Validated 0:49/$51 red chest palette, direction, 32-frame reward rise, " +
            "reward visibility through TX_0005, 30 rupees, and opened state.");
    }

    private void ValidateInventoryFoundation()
    {
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            _inventory.EquippedA != InventoryState.ItemSword ||
            _inventory.SwordLevel != 1)
        {
            throw new InvalidOperationException(
                "The development sword was not granted through TREASURE_OBJECT_SWORD_00 into wInventoryA.");
        }

        var chests = new ChestDatabase();
        if (!chests.TryGet(4, 0x87, 0x65, out ChestDatabase.ChestRecord switchHookChest) ||
            switchHookChest.TreasureObject != "TREASURE_OBJECT_SWITCH_HOOK_00" ||
            switchHookChest.TreasureId != TreasureDatabase.TreasureSwitchHook ||
            switchHookChest.Parameter != 1)
        {
            throw new InvalidOperationException(
                "The original 4:87/$65 Switch Hook chest did not resolve to TREASURE_SWITCH_HOOK parameter 1.");
        }

        var tempInventory = new InventoryState(_treasures);
        tempInventory.GiveTreasure(new TreasureDatabase.TreasureObjectRecord(
            switchHookChest.TreasureObject,
            switchHookChest.TreasureId,
            switchHookChest.SubId,
            switchHookChest.Parameter,
            switchHookChest.TextId,
            switchHookChest.Graphic,
            switchHookChest.Message));
        if (!tempInventory.HasTreasure(TreasureDatabase.TreasureSwitchHook) ||
            tempInventory.SwitchHookLevel != 1 ||
            tempInventory.EquippedB != TreasureDatabase.TreasureSwitchHook)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_SWITCH_HOOK_00 did not update obtained flags, level, and first free button slot.");
        }

        GD.Print("Validated disassembly-backed inventory state, equipped A/B slots, " +
            "TREASURE_OBJECT_SWORD_00 startup grant, and non-rupee chest treasure give path.");
    }

    private void ValidateInventoryMenu()
    {
        ValidateItemIconShadeMapping();
        _inventoryMenu.OpenImmediatelyForValidation();
        if (!_inventoryMenu.IsActive || !_inventoryScreen.Visible ||
            _player.IsPhysicsProcessing() || _player.IsProcessing())
        {
            throw new InvalidOperationException("The inventory menu did not freeze gameplay on open.");
        }

        if (_inventoryScreen.Cursor != 0 || _inventory.StorageItemAt(0) != InventoryState.ItemNone ||
            _inventory.EquippedA != InventoryState.ItemSword)
        {
            throw new InvalidOperationException("Inventory menu validation expected sword on A and slot 0 empty.");
        }

        _inventoryScreen.EquipToA();
        if (_inventory.EquippedA != InventoryState.ItemNone ||
            _inventory.StorageItemAt(0) != InventoryState.ItemSword)
        {
            throw new InvalidOperationException(
                "Pressing A on empty storage slot 0 did not unequip the sword into wInventoryStorage.");
        }

        _inventoryScreen.EquipToB();
        if (_inventory.EquippedB != InventoryState.ItemSword ||
            _inventory.StorageItemAt(0) != InventoryState.ItemNone)
        {
            throw new InvalidOperationException(
                "Pressing B on storage slot 0 did not equip the sword to wInventoryB.");
        }

        _inventoryScreen.MoveCursor(Vector2I.Left);
        if (_inventoryScreen.Cursor != 15)
            throw new InvalidOperationException("Inventory cursor did not wrap left with the original & $0f rule.");
        _inventoryScreen.MoveCursor(Vector2I.Right);
        if (_inventoryScreen.Cursor != 0)
            throw new InvalidOperationException("Inventory cursor did not return to slot 0 after wrapping.");

        _inventoryScreen.EquipToB();
        _inventoryScreen.EquipToA();
        if (_inventory.EquippedA != InventoryState.ItemSword ||
            _inventory.EquippedB != InventoryState.ItemNone ||
            _inventory.StorageItemAt(0) != InventoryState.ItemNone)
        {
            throw new InvalidOperationException("Inventory menu did not restore the sword to A through storage swaps.");
        }

        _inventoryMenu.CloseImmediatelyForValidation();
        if (_inventoryMenu.IsActive || _inventoryScreen.Visible ||
            !_player.IsPhysicsProcessing() || !_player.IsProcessing())
        {
            throw new InvalidOperationException("The inventory menu did not restore gameplay processing on close.");
        }

        GD.Print("Validated inventory subscreen slot cursor wrapping, A/B storage swaps, " +
            "sword unequip, B equip, and gameplay freezing.");
    }

    private static void ValidateItemIconShadeMapping()
    {
        if (ItemIconAtlas.ShadeFromPng(Color.Color8(0, 0, 0), out bool transparent) != 0 || !transparent ||
            ItemIconAtlas.ShadeFromPng(Color.Color8(85, 85, 85), out transparent) != 1 || transparent ||
            ItemIconAtlas.ShadeFromPng(Color.Color8(170, 170, 170), out transparent) != 2 || transparent ||
            ItemIconAtlas.ShadeFromPng(Color.Color8(255, 255, 255), out transparent) != 3 || transparent)
        {
            throw new InvalidOperationException(
                "Item icon PNG shade mapping no longer treats black as transparent and white as highlight.");
        }

        Image icons1 = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        Image icons2 = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        Image icons3 = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        if (!ItemIconAtlas.Select(0x99, icons1, icons2, icons3, out Image bracelet, out int braceletCell) ||
            bracelet != icons2 || braceletCell != 9 ||
            !ItemIconAtlas.Select(0xaf, icons1, icons2, icons3, out Image glove, out int gloveCell) ||
            glove != icons3 || gloveCell != 15)
        {
            throw new InvalidOperationException(
                "Display `$99/`$af no longer resolve to item icon sheets 2:9 and 3:15.");
        }

        Color[,] palettes = ItemIconAtlas.LoadStandardSpritePalettes();
        if (!palettes[0, 2].IsEqualApprox(new Color(0x02 / 31.0f, 0x15 / 31.0f, 0x08 / 31.0f)) ||
            !palettes[5, 1].IsEqualApprox(new Color(0x1f / 31.0f, 0x16 / 31.0f, 0x06 / 31.0f)) ||
            !palettes[5, 2].IsEqualApprox(new Color(0x1b / 31.0f, 0x00, 0x00)))
        {
            throw new InvalidOperationException(
                "Standard sprite palettes no longer match bracelet palette `$05 and sword palette `$00.");
        }
    }

    private void ValidateBraceletChestAndPushGate()
    {
        var chests = new ChestDatabase();
        if (!chests.TryGet(5, 0xa6, 0x37, out ChestDatabase.ChestRecord braceletChest) ||
            braceletChest.TreasureObject != "TREASURE_OBJECT_BRACELET_02" ||
            braceletChest.TreasureId != TreasureDatabase.TreasureBracelet ||
            braceletChest.Parameter != 2)
        {
            throw new InvalidOperationException(
                "The original 5:a6/$37 chest did not resolve to TREASURE_OBJECT_BRACELET_02.");
        }

        var pushables = new PushableTileDatabase();
        if (!pushables.TryGet(2, 0x10, out PushableTileDatabase.PushableTileRecord braceletBlock) ||
            !braceletBlock.RequiresBracelet ||
            !braceletBlock.AllowsEveryDirection ||
            braceletBlock.SourceReplacement != 0xa0 ||
            braceletBlock.DestinationTile != 0x10)
        {
            throw new InvalidOperationException(
                "Collision mode 2 tile $10 did not retain interactable parameter $c0 and pushblock data.");
        }

        var breakables = new BreakableTileDatabase();
        if (!breakables.TryGet(2, 0x10, out BreakableTileDatabase.BreakableTileRecord liftablePot) ||
            !liftablePot.AllowsSource(BreakableTileDatabase.SourceBracelet) ||
            liftablePot.Replacement != 0xa0)
        {
            throw new InvalidOperationException(
                "Collision mode 2 tile $10 did not import as a bracelet-breakable tile with replacement $a0.");
        }

        LoadValidationRoom(4, 0x08);
        Vector2 blockCenter = new(0x0b * 16 + 8, 0x04 * 16 + 8);
        Vector2 linkBelow = blockCenter + new Vector2(0, 10);
        if (_currentRoom.GetMetatile(blockCenter) != 0x1c ||
            !_currentRoom.ReplaceMetatile(blockCenter, 0x1c, 0x10, (long)_animationTicks))
        {
            throw new InvalidOperationException("Could not prepare 4:08/$4b as bracelet-required tile $10.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _playerWorld.UpdatePushableBlocks(linkBelow, Vector2I.Up, Vector2.Up);
        if (_pushBlocks.Active || _currentRoom.GetMetatile(blockCenter) != 0x10)
        {
            throw new InvalidOperationException(
                "Bracelet-required tile $10 moved before TREASURE_BRACELET was obtained.");
        }

        LoadValidationRoom(4, 0xce);
        _interactions.ResetChestForTesting(4, 0xce, 0x67, "TREASURE_OBJECT_BRACELET_00");
        Vector2 debugBraceletChest = new(7 * OracleRoomData.MetatileSize + 8, 6 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(debugBraceletChest) != 0xf1)
            throw new InvalidOperationException("The debug 4:ce/$67 Power Bracelet chest was not closed.");

        _player.WarpTo(new Vector2(debugBraceletChest.X, debugBraceletChest.Y + 12));
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(debugBraceletChest) != 0xf0)
        {
            throw new InvalidOperationException("The debug 4:ce/$67 Power Bracelet chest did not open from below.");
        }

        _interactions.Update(32.0 / 60.0, _player);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureBracelet) ||
            _inventory.BraceletLevel != 1 ||
            _inventory.EquippedB != InventoryState.ItemBracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got the\nPower Bracelet!\nHold the button\nand press \\item(0x00)\nto lift heavy\nobjects!")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_BRACELET_00 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_0026.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        Vector2 liftPoint = new(7 * OracleRoomData.MetatileSize + 8, 2 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(liftPoint) != 0x10)
            throw new InvalidOperationException("The 4:ce bracelet-use test tile was not dungeon tile $10.");
        _player.WarpTo(new Vector2(liftPoint.X, liftPoint.Y + 12));
        _player.Face(Vector2I.Up);
        if (!_playerWorld.TryUseBracelet(_player) || _currentRoom.GetMetatile(liftPoint) != 0xa0)
        {
            throw new InvalidOperationException(
                "Equipped bracelet use did not break/lift the tile in front using BREAKABLETILESOURCE_BRACELET.");
        }

        LoadValidationRoom(5, 0xa6);
        _interactions.ResetChestForTesting(5, 0xa6, 0x37);
        Vector2 chestPoint = new(7 * OracleRoomData.MetatileSize + 8, 3 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(chestPoint) != 0xf1)
            throw new InvalidOperationException("The original 5:a6/$37 Power Glove chest was not closed.");

        _player.WarpTo(new Vector2(chestPoint.X, chestPoint.Y + 12));
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0)
        {
            throw new InvalidOperationException("The 5:a6/$37 Power Glove chest did not open from below.");
        }

        _interactions.Update(32.0 / 60.0, _player);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureBracelet) ||
            _inventory.BraceletLevel != 2 ||
            _inventory.EquippedB != InventoryState.ItemBracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got the\nPower Glove!\nYou can now lift\nheavy objects.")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_BRACELET_02 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_002f.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        LoadValidationRoom(4, 0x08);
        if (_currentRoom.GetMetatile(blockCenter) != 0x10)
        {
            if (_currentRoom.GetMetatile(blockCenter) != 0x1c ||
                !_currentRoom.ReplaceMetatile(blockCenter, 0x1c, 0x10, (long)_animationTicks))
            {
                throw new InvalidOperationException("Could not restore bracelet push validation tile $10.");
            }
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _playerWorld.UpdatePushableBlocks(linkBelow, Vector2I.Up, Vector2.Up);
        if (!_pushBlocks.Active || _currentRoom.GetMetatile(blockCenter) != 0xa0)
        {
            throw new InvalidOperationException(
                "Bracelet-required tile $10 did not start moving after TREASURE_BRACELET was obtained.");
        }
        _pushBlocks.Cancel();
        _currentRoom.ReplaceMetatile(blockCenter, 0xa0, 0x1c, (long)_animationTicks);

        GD.Print("Validated debug Power Bracelet chest TREASURE_OBJECT_BRACELET_00, " +
            "bracelet tile use via BREAKABLETILESOURCE_BRACELET, original Power Glove upgrade, " +
            "and bracelet-required pushblock tile $10.");
    }

    private void ValidateHouseWarp()
    {
        WarpToHouseTest();
        for (float y = 54; y >= 47; y--)
        {
            if (Collides(new Vector2(88, y)))
                throw new InvalidOperationException($"The path into exterior door $25 is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(88, 47));
        if (!CheckTileWarp(_player) || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected exterior 0:47/$25 to enter house 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height))
            throw new InvalidOperationException("House entry did not begin at the bottom edge of the interior.");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height - WarpEnterFrames))
            throw new InvalidOperationException("Link did not perform the 28-frame interior entry walk.");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("The 32-frame room fade did not finish after entering the house.");

        for (float y = _player.Position.Y; y <= _currentRoom.Height + 2; y++)
        {
            if (Collides(new Vector2(_currentRoom.Width / 2.0f, y)))
                throw new InvalidOperationException($"The house's bottom exit is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(_currentRoom.Width / 2.0f, _currentRoom.Height + 2));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException("The house exit did not begin with its scripted walk offscreen.");
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 || !IsTransitioning)
            throw new InvalidOperationException("The exterior was not loaded after the 16-frame exit walk.");
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x35)
            throw new InvalidOperationException(
                $"Expected house 2:ea bottom exit to step out below 0:47/$25, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}/${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        if (Collides(_player.Position + Vector2.Down))
            throw new InvalidOperationException("The exterior landing spot below 0:47/$25 is blocked.");

        _activeGroup = 2;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0xeb);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(new Vector2(-2, _currentRoom.Height / 2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected room 2:eb left edge to scroll to 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x49)
            throw new InvalidOperationException(
                $"Expected Link to finish 2:eb -> 2:ea near the right edge, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");

        GD.Print("Validated original house entry/exit fades, scripted walks, and 2:eb -> 2:ea screen transition.");
    }

    private void ValidateCaveWarps()
    {
        ValidateLargeRoomCaveWarp(0x21, 0x04);
        ValidateLargeDungeonTopTransition();
        ValidateLargeRoomCaveWarp(0x28, 0xce);
        GD.Print("Validated 0:48 cave entries and dungeon00 room 4:04 -> 4:03 top transition.");
    }

    private void ValidateLargeDungeonTopTransition()
    {
        float exitX = -1.0f;
        for (float x = 8.0f; x < _currentRoom.Width; x++)
        {
            if (!Collides(new Vector2(x, -2.0f)))
            {
                exitX = x;
                break;
            }
        }
        if (exitX < 0.0f)
            throw new InvalidOperationException("Could not find 4:04's open northern dungeon exit.");

        _player.WarpTo(new Vector2(exitX, -2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 4 || _currentRoom.Id != 0x03 || !_scrollTransitionActive)
            throw new InvalidOperationException(
                $"Expected dungeon00 room 4:04 north to lead to 4:03, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (_scrollTransitionFrames != 32 || !Mathf.IsEqualApprox(_scrollTransitionDistance, 128.0f))
            throw new InvalidOperationException("Large-room vertical scrolling did not use the 128px playfield distance.");

        FinishActiveScrollingTransitionForValidation();
        if (Mathf.Abs(WorldToScreen(_player.Position).Y - 118.0f) > 0.01f)
            throw new InvalidOperationException("Link did not finish 4:04 -> 4:03 at the lower playfield edge.");
    }

    private void ValidateLargeRoomCaveWarp(int sourcePosition, int destinationRoom)
    {
        WarpToNpcTest();
        int tileX = sourcePosition & 0x0f;
        int tileY = (sourcePosition >> 4) & 0x0f;
        _player.WarpTo(new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8));
        if (!CheckTileWarp(_player) || _activeGroup != 4 || _currentRoom.Id != destinationRoom)
            throw new InvalidOperationException(
                $"Expected 0:48/${sourcePosition:x2} to enter 4:{destinationRoom:x2}, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}.");
        int expectedWidth = OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize;
        int expectedHeight = OracleRoomData.LargeRoomHeightInTiles * OracleRoomData.MetatileSize;
        if (_currentRoom.Width != expectedWidth || _currentRoom.Height != expectedHeight ||
            _currentRoom.Texture.GetWidth() != expectedWidth ||
            _currentRoom.Texture.GetHeight() != expectedHeight)
            throw new InvalidOperationException(
                $"Expected 4:{destinationRoom:x2} to use the original 240x176 playable large-room dimensions.");
        if (_player.Position != new Vector2(0x78, 0xb0))
            throw new InvalidOperationException(
                $"Expected the original large-room entry coordinate $b0/$78, got {_player.Position}.");

        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 128)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not begin the 4:{destinationRoom:x2} cave entry at screen position (80,128).");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 100)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not finish the 28-frame 4:{destinationRoom:x2} cave entry at screen position (80,100).");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException($"The 4:{destinationRoom:x2} cave fade did not finish.");

        _player.WarpTo(new Vector2(_currentRoom.Width - 1, _currentRoom.Height / 2.0f));
        UpdateRoomCamera();
        if (Mathf.Abs(WorldToScreen(new Vector2(_currentRoom.Width, 0)).X -
            OracleRoomData.ViewportWidth) > 0.01f)
        {
            throw new InvalidOperationException(
                $"The 4:{destinationRoom:x2} camera exposed the padded 16th large-room column.");
        }
        if (!_collision.Collides(new Vector2(
            OracleRoomData.LargeRoomWidthInTiles * OracleRoomData.MetatileSize + 5,
            _currentRoom.Height / 2.0f)))
        {
            throw new InvalidOperationException(
                $"The 4:{destinationRoom:x2} padded 16th large-room column allowed Link out of bounds.");
        }
    }

    private void ValidateSwordBush()
    {
        WarpToBushTest();
        Vector2 bushPoint = new(24, 56);
        if (_currentRoom.GetMetatile(bushPoint) != 0xc5)
            throw new InvalidOperationException("Expected overworld bush $c5 in room 69 at $31.");
        Vector2 objectPosition = _player.Position;
        _player.StartSwordAttack();
        if (_player.AttackSpriteOrigin != new Vector2(-8, -8))
            throw new InvalidOperationException(
                $"Sword frame $ac displaced Link from the standard OAM origin: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(16, -2))
            throw new InvalidOperationException(
                $"Sword arc phase $00 was not relative to Link's object position: {_player.SwordSpritePosition}.");
        _player._Process(7.0 / 60.0);
        if (_player.Position != objectPosition)
            throw new InvalidOperationException("Swinging the sword changed Link's object position.");
        if (_player.AttackSpriteOrigin != new Vector2(-8, -11))
            throw new InvalidOperationException(
                $"Sword frame $b4 did not apply only its original OAM $08 pose offset: {_player.AttackSpriteOrigin}.");
        if (_player.SwordSpritePosition != new Vector2(-4, -17))
            throw new InvalidOperationException(
                $"Sword arc phase $08 was not relative to Link's object position: {_player.SwordSpritePosition}.");
        if (_currentRoom.GetMetatile(bushPoint) != 0x3a)
            throw new InvalidOperationException("The level-1 sword did not replace bush $c5 with ground $3a.");
        if (_currentRoom.IsSolid(bushPoint))
            throw new InvalidOperationException("The cut bush's replacement tile remained solid.");
        GD.Print("Validated level-1 sword OAM anchoring, hit, and bush substitution c5 -> 3a in room 69.");
    }

    private void ValidateKeese()
    {
        var database = new EnemyDatabase();
        if (database.KeeseRecordCount != 53 || database.KeeseInstanceCount != 158)
            throw new InvalidOperationException(
                $"Expected 53 ENEMY_KEESE room records / 158 instances, got " +
                $"{database.KeeseRecordCount} / {database.KeeseInstanceCount}.");

        LoadValidationRoom(4, 0x39);
        if (_entities.Entities<KeeseCharacter>().Count != 2 || _entities.Entities<KeeseCharacter>().Exists(keese => keese.Record.SubId != 1))
            throw new InvalidOperationException(
                $"Room 4:39 should contain two random-position ENEMY_KEESE subid `$01 objects, " +
                $"got {_entities.Entities<KeeseCharacter>().Count}.");

        KeeseCharacter approachKeese = _entities.Entities<KeeseCharacter>()[0];
        if (approachKeese.SpriteHeight != -1)
            throw new InvalidOperationException("Keese subid `$01 did not preserve its original z-height `$ff.");
        approachKeese.Position = new Vector2(80, 80);
        _player.WarpTo(new Vector2(128, 80));
        _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.State != KeeseCharacter.KeeseState.Moving ||
            approachKeese.Counter1 != 12 || approachKeese.Counter2 != 12 ||
            !approachKeese.Flying)
            throw new InvalidOperationException(
                "Keese subid `$01 did not wake inside strict Manhattan distance `$31 with 12x12 counters.");

        for (int frame = 0; frame < 4; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.CurrentAnimationFrame != 1)
            throw new InvalidOperationException(
                "Flying Keese did not switch OAM frames after the original 4 animation calls.");
        for (int frame = 4; frame < 144; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (approachKeese.State != KeeseCharacter.KeeseState.Resting || approachKeese.Flying)
            throw new InvalidOperationException(
                "Keese subid `$01 did not return to rest after 12 turning intervals of 12 updates.");

        LoadValidationRoom(4, 0xcb);
        if (_entities.Entities<KeeseCharacter>().Count != 4 || _entities.Entities<KeeseCharacter>().Exists(keese => keese.Record.SubId != 0))
            throw new InvalidOperationException(
                $"Room 4:cb should contain four random-position ENEMY_KEESE subid `$00 objects, " +
                $"got {_entities.Entities<KeeseCharacter>().Count}.");

        KeeseCharacter normalKeese = _entities.Entities<KeeseCharacter>()[0];
        normalKeese.Position = new Vector2(48, 48);
        _player.WarpTo(new Vector2(160, 120));
        _entities.Update(31.0 / 60.0, _player);
        if (normalKeese.State != KeeseCharacter.KeeseState.Resting || normalKeese.Counter1 != 1)
            throw new InvalidOperationException(
                "Normal Keese did not preserve its original 32-update initial rest counter.");
        _entities.Update(1.0 / 60.0, _player);
        if (normalKeese.State != KeeseCharacter.KeeseState.Moving || !normalKeese.Flying ||
            normalKeese.Counter1 is < 0xc0 or > 0xff)
            throw new InvalidOperationException(
                "Normal Keese did not choose its original random `$c0-`$ff flight counter and angle.");

        _player.RefillHealth();
        _player.WarpTo(normalKeese.Position, recordSafe: false);
        int healthBeforeContact = _player.HealthQuarters;
        _entities.Update(0.0, _player);
        if (_player.HealthQuarters != healthBeforeContact - 2 ||
            !Mathf.IsEqualApprox(_player.InvincibilityFrames, 0x22) ||
            !Mathf.IsEqualApprox(_player.KnockbackFrames, 0x0f))
            throw new InvalidOperationException(
                "Keese contact did not apply half-heart damage, 34 invincibility updates, and 15 knockback updates.");
        _entities.Update(0.0, _player);
        if (_player.HealthQuarters != healthBeforeContact - 2)
            throw new InvalidOperationException("Keese contact bypassed Link's invincibility counter.");

        _player.WarpTo(normalKeese.Position + Vector2.Down * 16.0f);
        Vector2 expectedPuffPosition = normalKeese.Position +
            Vector2.Down * normalKeese.SpriteHeight;
        int countBeforeSword = _entities.Entities<KeeseCharacter>().Count;
        if (!_entities.ApplySwordHit(normalKeese.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<KeeseCharacter>().Count != countBeforeSword - 1)
            throw new InvalidOperationException(
                "The level-1 sword did not defeat the 1-health ENEMY_KEESE in one hit.");
        if (_entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].Position != expectedPuffPosition ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].HighKnockback ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x32 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].DurationFrames != 20 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].CurrentPalette != 2)
        {
            throw new InvalidOperationException(
                "Defeating Keese did not create the ordinary 20-update PART_ENEMY_DESTROYED puff at its visual position.");
        }

        KeeseCharacter transitionKeese = _entities.Entities<KeeseCharacter>()[0];
        int transitionCounter = transitionKeese.Counter1;
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x39);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<KeeseCharacter>().Count != 3 || _entities.Entities<KeeseCharacter>().Count != 2 ||
            _entities.OutgoingEntities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.OutgoingEntities<EnemyDeathPuffEffect>()[0].ElapsedFrames != 0 ||
            transitionKeese.Counter1 != transitionCounter ||
            !_entities.Entities<KeeseCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze outgoing Keese/death puffs and preload/freeze destination Keese.");
        _entities.SetScreenTransitionOffsets(
            Vector2.Right * 4.0f,
            Vector2.Left * (incomingRoom.Width - 4.0f));
        if (!_entities.OutgoingEntities<EnemyDeathPuffEffect>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Right * 4.0f))
            throw new InvalidOperationException("The death puff did not move with its outgoing room during scrolling.");
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<KeeseCharacter>().Count != 0 || _entities.OutgoingEntities<EnemyDeathPuffEffect>().Count != 0 ||
            !_entities.Entities<KeeseCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
            throw new InvalidOperationException(
                "Keese/death-puff transition ownership and offsets were not normalized after scrolling.");

        var normalPuff = new EnemyDeathPuffEffect();
        normalPuff.Initialize(Vector2.Zero);
        for (int frame = 1; frame <= 20; frame++)
        {
            normalPuff.UpdateFrame(frame);
            if (frame == 1 &&
                (normalPuff.AnimationFrame != 0 || normalPuff.CurrentPalette != 2))
            {
                throw new InvalidOperationException(
                    "PART_ENEMY_DESTROYED changed frame/palette before its first 2-update record elapsed.");
            }
            if (frame == 2 &&
                (normalPuff.AnimationFrame != 1 || normalPuff.CurrentPalette != 3))
            {
                throw new InvalidOperationException(
                    "PART_ENEMY_DESTROYED did not advance after 2 updates and toggle OAM flags `$0a -> `$0b.");
            }
            if (frame < 20 && normalPuff.Finished)
                throw new InvalidOperationException("The ordinary enemy death puff ended before 20 updates.");
        }
        if (!normalPuff.Finished || normalPuff.ElapsedFrames != 20)
            throw new InvalidOperationException("The ordinary enemy death puff did not end after 20 updates.");
        normalPuff.Free();

        var highKnockbackPuff = new EnemyDeathPuffEffect();
        highKnockbackPuff.Initialize(Vector2.Zero, highKnockback: true);
        for (int frame = 1; frame <= 28; frame++)
        {
            highKnockbackPuff.UpdateFrame(frame);
            if (frame == 6 && highKnockbackPuff.AnimationFrame != 3)
                throw new InvalidOperationException(
                    "The high-knockback death puff did not enter its extra 8-update burst frame.");
            if (frame < 28 && highKnockbackPuff.Finished)
                throw new InvalidOperationException("The high-knockback enemy death puff ended before 28 updates.");
        }
        if (!highKnockbackPuff.Finished || highKnockbackPuff.DurationFrames != 28)
            throw new InvalidOperationException("The high-knockback enemy death puff did not end after 28 updates.");
        highKnockbackPuff.Free();
        _player.RefillHealth();

        GD.Print("Validated 53 imported ENEMY_KEESE room records, subid `$00/`$01 timing, " +
            "original RNG-driven flight, 4-update wing animation, collision radii, contact damage, " +
            "invincibility/knockback counters, one-hit level-1 sword defeat, common 20/28-update " +
            "death puffs with palette toggling, and retained/preloaded scrolling.");
    }

    private void ValidateOctoroks()
    {
        var database = new EnemyDatabase();
        if (database.OctorokRecordCount != 33 || database.OctorokInstanceCount != 48)
        {
            throw new InvalidOperationException(
                $"Expected 33 ENEMY_OCTOROK room records / 48 instances, got " +
                $"{database.OctorokRecordCount} / {database.OctorokInstanceCount}.");
        }
        EnemyDatabase.OctorokProjectileRecord projectile = database.OctorokProjectile;
        if (projectile.TileBase != 0x0c || projectile.Palette != 3 ||
            projectile.CollisionRadiusY != 2 || projectile.CollisionRadiusX != 2 ||
            projectile.DamageQuarters != 2 || projectile.SpeedRaw != 0x50)
        {
            throw new InvalidOperationException(
                "PART_OCTOROK_PROJECTILE did not retain tile `$0c, palette 3, radius 2x2, " +
                "half-heart damage, and SPEED_200 (`$50)." );
        }

        LoadValidationRoom(0, 0x74);
        if (_entities.Entities<OctorokCharacter>().Count != 2 ||
            _entities.Entities<OctorokCharacter>().Find(octorok => octorok.Record.SubId == 0) is not OctorokCharacter red ||
            _entities.Entities<OctorokCharacter>().Find(octorok => octorok.Record.SubId == 1) is not OctorokCharacter fastRed ||
            red.Record.SpeedRaw != 0x14 || fastRed.Record.SpeedRaw != 0x1e ||
            red.Record.Health != 2 || red.Record.DamageQuarters != 1)
        {
            throw new InvalidOperationException(
                "Room 0:74 did not load one random red and one fast-red Octorok with " +
                "SPEED_80/SPEED_c0, two health, and quarter-heart contact damage.");
        }

        int redCount = _entities.Entities<OctorokCharacter>().Count;
        if (!_entities.ApplySwordHit(red.CollisionBounds.Grow(1.0f), red.Position + Vector2.Down * 16.0f) ||
            _entities.Entities<OctorokCharacter>().Count != redCount - 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x09)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not defeat a two-health red Octorok in one hit and " +
                "create its ordinary ENEMY_OCTOROK `$09 death puff.");
        }

        LoadValidationRoom(1, 0xbc);
        if (_entities.Entities<OctorokCharacter>().Count != 2 ||
            _entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Record.SubId != 2) ||
            !_entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Position == new Vector2(0x48, 0x48)) ||
            !_entities.Entities<OctorokCharacter>().Exists(octorok => octorok.Position == new Vector2(0x58, 0x48)))
        {
            throw new InvalidOperationException(
                "Room 1:bc did not preserve its fixed blue Octoroks at `$48,`$48 and `$58,`$48.");
        }

        OctorokCharacter blue = _entities.Entities<OctorokCharacter>()[0];
        OctorokCharacter otherBlue = _entities.Entities<OctorokCharacter>()[1];
        if (blue.Record.Health != 3 || blue.Record.DamageQuarters != 2 ||
            blue.Record.CounterMask != 3)
        {
            throw new InvalidOperationException(
                "Blue Octorok subid `$02 did not retain three health, half-heart contact damage, " +
                "and decision mask `$03.");
        }

        _player.WarpTo(new Vector2(144, 120), recordSafe: false);
        otherBlue.SetStateForValidation(OctorokCharacter.OctorokState.Standing, counter1: 1000);
        blue.SetStateForValidation(
            OctorokCharacter.OctorokState.Shooting, counter1: 0x10, angle: 0x18);
        for (int frame = 1; frame < 0x10; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 0 || blue.Counter1 != 1)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK fired before completing its original `$10-update windup.");
        }
        Vector2 projectileOrigin = blue.Position;
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 1 ||
            _entities.Entities<OctorokRockProjectile>()[0].State != OctorokRockProjectile.RockState.Flying ||
            _entities.Entities<OctorokRockProjectile>()[0].ElapsedFrames != 1 ||
            _entities.Entities<OctorokRockProjectile>()[0].Position != projectileOrigin ||
            blue.State != OctorokCharacter.OctorokState.Standing || blue.Counter1 != 0x20)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK did not spawn PART_OCTOROK_PROJECTILE after 16 updates and " +
                "enter its 32-update post-shot stand.");
        }

        OctorokRockProjectile rock = _entities.Entities<OctorokRockProjectile>()[0];
        _entities.Update(1.0 / 60.0, _player);
        if (rock.Position != projectileOrigin + Vector2.Left * 2.0f)
            throw new InvalidOperationException("The Octorok rock did not move at SPEED_200 (2 pixels/update).");
        for (int frame = 0; frame < 4; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (!_entities.ApplySwordHit(rock.CollisionBounds.Grow(1.0f), _player.Position) ||
            rock.State != OctorokRockProjectile.RockState.Bouncing ||
            rock.Angle != 0x08 || rock.Counter != 0x20)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not reverse an Octorok rock and start its `$20-update bounce.");
        }
        for (int frame = 1; frame < 0x20; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (rock.Finished || _entities.Entities<OctorokRockProjectile>().Count != 1 || rock.Counter != 1)
            throw new InvalidOperationException("The deflected Octorok rock ended before bounce update `$20.");
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<OctorokRockProjectile>().Count != 0)
            throw new InvalidOperationException("The deflected Octorok rock survived bounce update `$20.");

        Vector2 terrainCollisionOrigin = Vector2.Zero;
        bool foundTerrainCollision = false;
        for (int y = 8; y < _currentRoom.Height - 8 && !foundTerrainCollision; y++)
        {
            for (int x = 8; x < _currentRoom.Width - 2; x++)
            {
                var origin = new Vector2(x, y);
                if (!_currentRoom.IsSolid(origin) &&
                    _currentRoom.IsSolid(origin + Vector2.Right * 2.0f) &&
                    origin.DistanceTo(_player.Position) > 16.0f)
                {
                    terrainCollisionOrigin = origin;
                    foundTerrainCollision = true;
                    break;
                }
            }
        }
        if (!foundTerrainCollision)
            throw new InvalidOperationException("Room 1:bc has no usable Octorok-rock collision edge.");
        var terrainRock = new OctorokRockProjectile();
        terrainRock.Initialize(projectile, _currentRoom, terrainCollisionOrigin, angle: 0x08);
        terrainRock.UpdateFrame(_player);
        terrainRock.UpdateFrame(_player);
        if (terrainRock.State != OctorokRockProjectile.RockState.CollisionPending ||
            terrainRock.Position != terrainCollisionOrigin + Vector2.Right * 2.0f)
        {
            throw new InvalidOperationException(
                "A terrain-striking Octorok rock did not enter state 2 after applying its final flying step.");
        }
        terrainRock.UpdateFrame(_player);
        if (terrainRock.State != OctorokRockProjectile.RockState.Bouncing ||
            terrainRock.Counter != 0x20 || terrainRock.Angle != 0x18)
        {
            throw new InvalidOperationException(
                "Octorok-rock terrain collision state 2 did not initialize the reversed bounce on the next update.");
        }
        terrainRock.Free();

        blue = _entities.Entities<OctorokCharacter>()[0];
        otherBlue.SetStateForValidation(OctorokCharacter.OctorokState.Standing, counter1: 1000);
        int blueCount = _entities.Entities<OctorokCharacter>().Count;
        if (!_entities.ApplySwordHit(
                blue.CollisionBounds.Grow(1.0f), blue.Position + Vector2.Left * 16.0f) ||
            blue.Health != 1 || blue.InvincibilityCounter != 0x10 ||
            blue.KnockbackCounter != 0x08 || _entities.Entities<OctorokCharacter>().Count != blueCount)
        {
            throw new InvalidOperationException(
                "A blue Octorok did not survive its first level-1 sword hit with one health, " +
                "16 invincibility updates, and 8 SPEED_200 knockback updates. Got " +
                $"health {blue.Health}, invincibility {blue.InvincibilityCounter}, " +
                $"knockback {blue.KnockbackCounter}, count {_entities.Entities<OctorokCharacter>().Count}/{blueCount}.");
        }
        if (_entities.ApplySwordHit(blue.CollisionBounds.Grow(1.0f), _player.Position))
            throw new InvalidOperationException("Blue Octorok invincibility accepted a second immediate sword hit.");
        for (int frame = 0; frame < 0x10; frame++)
            blue.UpdateFrame(_player.Position);
        if (blue.InvincibilityCounter != 0 || blue.KnockbackCounter != 0 ||
            !_entities.ApplySwordHit(blue.CollisionBounds.Grow(1.0f), _player.Position) ||
            _entities.Entities<OctorokCharacter>().Count != blueCount - 1 ||
            _entities.Entities<EnemyDeathPuffEffect>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x09)
        {
            throw new InvalidOperationException(
                "A blue Octorok did not become vulnerable after 16 updates and die on the second sword hit.");
        }

        OctorokCharacter transitionOctorok = _entities.Entities<OctorokCharacter>()[0];
        transitionOctorok.SetStateForValidation(
            OctorokCharacter.OctorokState.Standing, counter1: 1000, angle: 0x00);
        OctorokRockProjectile transitionRock = _entities.Spawn<OctorokRockProjectile>(
            new OctorokRockSpawn(transitionOctorok.Position, Angle: 0x00));
        OracleRoomData incomingRoom = _world.LoadRoom(0, 0x74);
        _entities.BeginScreenTransition(0, incomingRoom, Vector2.Left * incomingRoom.Width);
        int frozenCounter = transitionOctorok.Counter1;
        int frozenRockFrames = transitionRock.ElapsedFrames;
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<OctorokCharacter>().Count != 1 || _entities.OutgoingEntities<OctorokRockProjectile>().Count != 1 ||
            _entities.Entities<OctorokCharacter>().Count != 2 || transitionOctorok.Counter1 != frozenCounter ||
            transitionRock.ElapsedFrames != frozenRockFrames ||
            !_entities.Entities<OctorokCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
        {
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze outgoing Octoroks and rocks while preloading destination Octoroks.");
        }
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<OctorokCharacter>().Count != 0 || _entities.OutgoingEntities<OctorokRockProjectile>().Count != 0 ||
            !_entities.Entities<OctorokCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
        {
            throw new InvalidOperationException(
                "Octorok/rock transition ownership and offsets were not normalized after scrolling.");
        }

        var drops = new ItemDropDatabase();
        int octorokProbabilityRolls = 0;
        for (int roll = 0; roll < 64; roll++)
        {
            if (drops.ProbabilityAllows(4, roll))
                octorokProbabilityRolls++;
        }
        if (drops.EnemyTableRecord(0x09) != 0x8e || octorokProbabilityRolls != 24 ||
            drops.ChooseDrop(0x09, 0, 0) != ItemDropDatabase.Heart)
        {
            throw new InvalidOperationException(
                "ENEMY_OCTOROK `$09 did not preserve drop record `$8e, its 24-of-64 " +
                "probability `$04, and supported heart/rupee set `$0e.");
        }

        _player.RefillHealth();
        GD.Print("Validated 33 imported ENEMY_OCTOROK room records / 48 instances, random and fixed " +
            "red/fast-red/blue subids, movement/combat attributes, 16-update firing, SPEED_200 rocks, " +
            "sword deflection, 32-update bounce, two-hit blue combat, scrolling, and `$8e item drops.");
    }

    private void ValidateZolsAndGels()
    {
        var database = new EnemyDatabase();
        if (database.ZolRecordCount != 61 || database.ZolInstanceCount != 79 ||
            database.GelRecordCount != 1 || database.GelInstanceCount != 3)
        {
            throw new InvalidOperationException(
                $"Expected 61 ENEMY_ZOL records / 79 instances and one ENEMY_GEL record / " +
                $"3 instances, got {database.ZolRecordCount} / {database.ZolInstanceCount} " +
                $"and {database.GelRecordCount} / {database.GelInstanceCount}.");
        }
        if (database.Gel.Id != 0x43 || database.Gel.TileBase != 0 ||
            database.Gel.Palette != 2 || database.Gel.CollisionRadiusY != 2 ||
            database.Gel.CollisionRadiusX != 2 || database.Gel.DamageQuarters != 2 ||
            database.Gel.Health != 1)
        {
            throw new InvalidOperationException(
                "ENEMY_GEL did not retain id `$43, tile base 0, palette 2, radius 2x2, " +
                "half-heart damage, and one health.");
        }

        LoadValidationRoom(4, 0xcc);
        if (_entities.Entities<ZolCharacter>().Count != 6 ||
            _entities.Entities<ZolCharacter>().FindAll(zol => zol.Record.SubId == 0).Count != 3 ||
            _entities.Entities<ZolCharacter>().FindAll(zol => zol.Record.SubId == 1).Count != 3 ||
            !_entities.Entities<ZolCharacter>().Exists(zol => zol.Position == new Vector2(0x58, 0x78)) ||
            !_entities.Entities<ZolCharacter>().Exists(zol => zol.Position == new Vector2(0x48, 0x98)))
        {
            throw new InvalidOperationException(
                "Room 4:cc did not preserve its three fixed green and three fixed red Zols.");
        }

        ZolCharacter transitionZol = _entities.Entities<ZolCharacter>()[0];
        int frozenCounter = transitionZol.Counter1;
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x0b);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<ZolCharacter>().Count != 6 || _entities.Entities<ZolCharacter>().Count != 0 ||
            _entities.Entities<GelCharacter>().Count != 3 || transitionZol.Counter1 != frozenCounter ||
            !_entities.Entities<GelCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Left * incomingRoom.Width))
        {
            throw new InvalidOperationException(
                "Scrolling did not retain/freeze six outgoing Zols and preload/freeze " +
                "the three direct room 4:0b Gels.");
        }
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<ZolCharacter>().Count != 0 ||
            !_entities.Entities<GelCharacter>()[0].TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
        {
            throw new InvalidOperationException(
                "Zol/Gel transition ownership and offsets were not normalized after scrolling.");
        }

        EnemyDatabase.ZolRecord greenRecord = default;
        foreach (EnemyDatabase.ZolRecord record in database.GetRoomZols(4, 0xcc))
        {
            if (record.SubId == 0)
            {
                greenRecord = record;
                break;
            }
        }
        if (greenRecord.Id != 0x34 || greenRecord.Health != 2 ||
            greenRecord.DamageQuarters != 2 || greenRecord.Palette != 0)
        {
            throw new InvalidOperationException(
                "Green ENEMY_ZOL `$34/`$00 attributes were not imported correctly.");
        }

        var timingZol = new ZolCharacter();
        var timingRandom = new OracleRandom();
        timingZol.Initialize(greenRecord, _world.LoadRoom(4, 0xcc), new Vector2(80, 80), timingRandom);
        timingZol.UpdateFrame(new Vector2(120, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHidden)
            throw new InvalidOperationException("Green Zol woke at the excluded Manhattan distance `$28.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenEmerging ||
            timingZol.Counter2 != 4 || !timingZol.Visible)
        {
            throw new InvalidOperationException(
                "Green Zol did not wake inside Manhattan distance `$28 with four hops queued.");
        }
        for (int frame = 0; frame < 32; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenEmerging ||
            timingZol.AnimationParameter != 1 || timingZol.ZFixed != 0)
        {
            throw new InvalidOperationException(
                "Green Zol emergence did not reach its terminal animation parameter after 32 updates.");
        }
        for (int frame = 0; frame < 26; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.ZFixed >= 0 || timingZol.State != ZolCharacter.ZolState.GreenEmerging)
            throw new InvalidOperationException("Green Zol landed before its 27th gravity update.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenWaiting ||
            timingZol.Counter1 != 0x30 || !timingZol.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "Green Zol did not land on gravity update 27 and begin its 48-update wait.");
        }
        for (int frame = 0; frame < 47; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.Counter1 != 1 || timingZol.State != ZolCharacter.ZolState.GreenWaiting)
            throw new InvalidOperationException("Green Zol ended its 48-update wait early.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHopping)
            throw new InvalidOperationException("Green Zol did not begin its first pursuit hop.");
        for (int frame = 0; frame < 27; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.Counter2 != 3 || timingZol.State != ZolCharacter.ZolState.GreenWaiting)
            throw new InvalidOperationException("Green Zol did not consume exactly one of four hops.");
        for (int hop = 0; hop < 3; hop++)
        {
            for (int frame = 0; frame < 48; frame++)
                timingZol.UpdateFrame(new Vector2(119, 80));
            for (int frame = 0; frame < 27; frame++)
                timingZol.UpdateFrame(new Vector2(119, 80));
        }
        if (timingZol.State != ZolCharacter.ZolState.GreenDisappearing ||
            timingZol.Counter2 != 0 || timingZol.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "Green Zol did not disable collision and disappear after exactly four hops.");
        }
        for (int frame = 0; frame < 40; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.AnimationParameter != 1 ||
            timingZol.State != ZolCharacter.ZolState.GreenDisappearing)
        {
            throw new InvalidOperationException(
                "Green Zol disappearance did not reach its terminal parameter after 40 updates.");
        }
        timingZol.UpdateFrame(new Vector2(119, 80));
        for (int frame = 0; frame < 39; frame++)
            timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenGone || timingZol.Counter1 != 1)
            throw new InvalidOperationException("Green Zol ended its 40-update underground wait early.");
        timingZol.UpdateFrame(new Vector2(119, 80));
        if (timingZol.State != ZolCharacter.ZolState.GreenHidden)
            throw new InvalidOperationException("Green Zol did not return to its hidden proximity state.");
        timingZol.Free();

        LoadValidationRoom(4, 0xcc);
        ZolCharacter green = _entities.Entities<ZolCharacter>().Find(zol => zol.Record.SubId == 0)!;
        green.SetStateForValidation(
            ZolCharacter.ZolState.GreenWaiting,
            counter1: 1000,
            animation: 1);
        int greenCount = _entities.Entities<ZolCharacter>().Count;
        if (!_entities.ApplySwordHit(green.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<ZolCharacter>().Count != greenCount - 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x34)
        {
            throw new InvalidOperationException(
                "The level-1 sword did not defeat a surfaced green Zol and create its normal `$34 death puff.");
        }

        LoadValidationRoom(4, 0xcc);
        _player.WarpTo(new Vector2(220, 160), recordSafe: false);
        ZolCharacter red = _entities.Entities<ZolCharacter>().Find(zol => zol.Record.SubId == 1)!;
        Vector2 splitPosition = red.Position;
        int redRoomCount = _entities.Entities<ZolCharacter>().Count;
        if (!_entities.ApplySwordHit(red.CollisionBounds.Grow(1.0f)) ||
            red.State != ZolCharacter.ZolState.RedSplitting ||
            _entities.Entities<ZolCharacter>().Count != redRoomCount || _entities.Entities<EnemyDeathPuffEffect>().Count != 0)
        {
            throw new InvalidOperationException(
                "A sword-hit red Zol did not enter its special split state without a normal death puff.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (red.State != ZolCharacter.ZolState.RedSplitDelay || red.Counter2 != 18 ||
            red.Visible || red.CollisionEnabled || _entities.Entities<KillEnemyPuffEffect>().Count != 1 ||
            _entities.Entities<KillEnemyPuffEffect>()[0].DurationFrames != 20)
        {
            throw new InvalidOperationException(
                "Red Zol did not create the 20-update INTERAC_KILLENEMYPUFF and begin its 18-update delay.");
        }
        for (int frame = 0; frame < 17; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<GelCharacter>().Count != 0 || red.Counter2 != 1)
            throw new InvalidOperationException("Red Zol spawned Gels before split delay update 18.");
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<ZolCharacter>().Count != redRoomCount - 1 || _entities.Entities<GelCharacter>().Count != 2 ||
            !_entities.Entities<GelCharacter>().Exists(gel => gel.Position == splitPosition + Vector2.Right * 4.0f) ||
            !_entities.Entities<GelCharacter>().Exists(gel => gel.Position == splitPosition + Vector2.Left * 4.0f))
        {
            throw new InvalidOperationException(
                "Red Zol did not replace itself with two Gels at the original +/-4 X offsets.");
        }
        _entities.Update(2.0 / 60.0, _player);
        if (_entities.Entities<KillEnemyPuffEffect>().Count != 0 || _entities.Entities<ItemDropEffect>().Count != 0)
            throw new InvalidOperationException(
                "INTERAC_KILLENEMYPUFF did not end after 20 updates or incorrectly resolved an item drop.");

        GelCharacter defeatedGel = _entities.Entities<GelCharacter>()[0];
        int gelCount = _entities.Entities<GelCharacter>().Count;
        if (!_entities.ApplySwordHit(defeatedGel.CollisionBounds.Grow(1.0f)) ||
            _entities.Entities<GelCharacter>().Count != gelCount - 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 1 ||
            _entities.Entities<EnemyDeathPuffEffect>()[0].EnemyId != 0x43)
        {
            throw new InvalidOperationException(
                "The one-health ENEMY_GEL did not die to one level-1 sword hit with a `$43 death puff.");
        }

        GelCharacter latchGel = _entities.Entities<GelCharacter>()[0];
        Vector2 latchPosition = new(180, 140);
        latchGel.Position = latchPosition;
        _player.WarpTo(latchPosition, recordSafe: false);
        _player.RefillHealth();
        int healthBeforeLatch = _player.HealthQuarters;
        _entities.Update(0.0, _player);
        if (!latchGel.IsAttached || latchGel.Counter2 != 120 ||
            _player.HealthQuarters != healthBeforeLatch ||
            !_entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Gel contact damaged Link or failed to latch for 120 updates and disable the sword.");
        }
        bool movementPhase = _entities.PlayerMovementDisabled;
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.PlayerMovementDisabled == movementPhase)
            throw new InvalidOperationException("Attached Gel did not immobilize Link on alternating updates.");
        for (int frame = 0; frame < 118; frame++)
            _entities.Update(1.0 / 60.0, _player);
        if (!latchGel.IsAttached || latchGel.Counter2 != 1)
            throw new InvalidOperationException(
                "Gel did not remain attached through latch update 119.");
        _entities.Update(1.0 / 60.0, _player);
        if (latchGel.IsAttached || latchGel.State != GelCharacter.GelState.Hopping ||
            latchGel.Angle != 0x00 || latchGel.CollisionEnabled ||
            _entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Gel did not automatically release after 120 updates with hop collision disabled.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (latchGel.IsAttached || _entities.PlayerSwordDisabled)
            throw new InvalidOperationException(
                "A naturally released Gel immediately relatched before completing its hop.");

        Vector2 buttonLatchPosition = new(120, 140);
        GelCharacter buttonGel = _entities.Spawn<GelCharacter>(
            new GelSpawn(buttonLatchPosition, "ButtonReleaseGel"));
        _player.WarpTo(buttonLatchPosition, recordSafe: false);
        _entities.Update(0.0, _player);
        if (!buttonGel.IsAttached || buttonGel.Counter2 != 120)
            throw new InvalidOperationException("Button-release test Gel did not latch.");
        for (int press = 0; press < 30; press++)
            buttonGel.UpdateFrame(_player.Position, Vector2I.Down, anyButtonJustPressed: true);
        if (!buttonGel.IsAttached || buttonGel.Counter2 != 1)
            throw new InvalidOperationException(
                "Thirty button presses did not reduce the Gel latch counter to one.");
        buttonGel.UpdateFrame(_player.Position, Vector2I.Down, anyButtonJustPressed: false);
        if (buttonGel.IsAttached || buttonGel.State != GelCharacter.GelState.Hopping ||
            buttonGel.Angle != 0x00 || buttonGel.CollisionEnabled ||
            _entities.PlayerSwordDisabled)
        {
            throw new InvalidOperationException(
                "Button presses did not release the Gel with collision disabled and hop it away from Link.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (buttonGel.IsAttached || _entities.PlayerSwordDisabled)
            throw new InvalidOperationException(
                "A button-released Gel immediately relatched before completing its hop.");

        _player.RefillHealth();
        GD.Print("Validated 61 ENEMY_ZOL records / 79 instances, room 4:cc fixed placements, " +
            "strict `$28 emergence, 32/27/48-update green timing, four-hop disappearance, " +
            "red 18-update splitting with INTERAC_KILLENEMYPUFF, +/-4 Gel spawning, direct " +
            "room Gels, non-damaging 120-update latch/button release, collision-safe hop-off, " +
            "alternating movement suppression, " +
            "sword disabling, combat, drops, and retained/preloaded scrolling.");
    }

    private void ValidateItemDrops()
    {
        var database = new ItemDropDatabase();
        if (ItemDropDatabase.SelectionDataSize != 720 ||
            database.EnemyTableRecord(0x32) != 0xae)
        {
            throw new InvalidOperationException(
                "ENEMY_KEESE `$32 did not retain item-drop record `$ae in the 720-byte selection data.");
        }

        int allowedProbabilityRolls = 0;
        int allowedRoll = -1;
        int deniedRoll = -1;
        for (int roll = 0; roll < 64; roll++)
        {
            if (database.ProbabilityAllows(5, roll))
            {
                allowedProbabilityRolls++;
                if (allowedRoll < 0)
                    allowedRoll = roll;
            }
            else if (deniedRoll < 0)
            {
                deniedRoll = roll;
            }
        }
        if (allowedProbabilityRolls != 32 || allowedRoll < 0 || deniedRoll < 0 ||
            database.ChooseDrop(0x32, (byte)deniedRoll, 0).HasValue ||
            database.ChooseDrop(0x32, (byte)allowedRoll, 0) != ItemDropDatabase.Heart)
        {
            throw new InvalidOperationException(
                "Keese probability set 5 did not preserve its original 32-of-64 drop chance.");
        }

        int hearts = 0;
        int oneRupees = 0;
        int fiveRupees = 0;
        for (int roll = 0; roll < ItemDropDatabase.SetSize; roll++)
        {
            switch (database.DropSetValue(0x0e, roll))
            {
                case ItemDropDatabase.Heart: hearts++; break;
                case ItemDropDatabase.OneRupee: oneRupees++; break;
                case ItemDropDatabase.FiveRupees: fiveRupees++; break;
            }
        }
        if (hearts != 16 || oneRupees != 12 || fiveRupees != 4)
        {
            throw new InvalidOperationException(
                $"Keese drop set `$0e should contain 16 hearts, 12 single rupees, and 4 five-rupee drops; " +
                $"got {hearts}, {oneRupees}, and {fiveRupees}.");
        }

        ItemDropDatabase.VisualRecord heartVisual = database.GetVisual(ItemDropDatabase.Heart);
        ItemDropDatabase.VisualRecord oneRupeeVisual = database.GetVisual(ItemDropDatabase.OneRupee);
        ItemDropDatabase.VisualRecord fiveRupeeVisual = database.GetVisual(ItemDropDatabase.FiveRupees);
        if (heartVisual.TileBase != 2 || heartVisual.Palette != 5 ||
            oneRupeeVisual.TileBase != 4 || oneRupeeVisual.Palette != 0 ||
            fiveRupeeVisual.TileBase != 6 || fiveRupeeVisual.Palette != 5)
        {
            throw new InvalidOperationException(
                "Heart/rupee PART_ITEM_DROP visuals do not match spriteData tile bases `$02/`$04/`$06.");
        }

        var lifecycleRoot = new Node { Name = "ItemDropLifecycleValidation" };
        AddChild(lifecycleRoot);
        var lifecycleManager = new RoomEntityManager(
            lifecycleRoot,
            new NpcDatabase(),
            new EnemyDatabase(),
            database,
            new TimePortalDatabase(),
            new OracleRandom());
        lifecycleManager.LoadRoom(0, _world.LoadRoom(0, 0x00));
        _player.WarpTo(new Vector2(140, 120), recordSafe: false);
        for (int defeated = 0; defeated < 5; defeated++)
        {
            lifecycleManager.Spawn<EnemyDeathPuffEffect>(
                new EnemyDeathPuffSpawn(new Vector2(24, 24), EnemyId: 0x32));
            for (int frame = 0; frame < 20; frame++)
                lifecycleManager.Update(1.0 / 60.0, _player);
            if (lifecycleManager.Entities<EnemyDeathPuffEffect>().Count != 0)
            {
                throw new InvalidOperationException(
                    "A finished Keese death puff was not replaced/deleted by item-drop resolution.");
            }
        }
        if (lifecycleManager.Entities<ItemDropEffect>().Count != 1 ||
            lifecycleManager.Entities<ItemDropEffect>()[0].SubId != ItemDropDatabase.FiveRupees ||
            lifecycleManager.Entities<ItemDropEffect>()[0].ElapsedFrames != 0)
        {
            throw new InvalidOperationException(
                "The deterministic fifth Keese death did not replace its completed puff with ITEM_DROP_5_RUPEES.");
        }
        lifecycleManager.Clear();
        RemoveChild(lifecycleRoot);
        lifecycleRoot.Free();

        LoadValidationRoom(4, 0xcb);
        Vector2 dropPosition = FindOpenItemDropPosition(_currentRoom);
        Vector2 farPosition = dropPosition + Vector2.Right * 40.0f;
        _player.WarpTo(farPosition, recordSafe: false);

        var bounceDrop = new ItemDropEffect();
        bounceDrop.Initialize(
            ItemDropDatabase.Heart, dropPosition, _currentRoom, heartVisual);
        bounceDrop.UpdateFrame(_player, 1);
        if (bounceDrop.State != ItemDropEffect.DropState.Bouncing ||
            bounceDrop.ZFixed != 0 || bounceDrop.SpeedZ != -0x160)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not spend its first update initializing speedZ to -`$160.");
        }
        for (int frame = 2; frame < 36; frame++)
        {
            bounceDrop.UpdateFrame(_player, frame);
            if (bounceDrop.State == ItemDropEffect.DropState.Grounded)
                throw new InvalidOperationException("PART_ITEM_DROP finished bouncing before update 36.");
        }
        bounceDrop.UpdateFrame(_player, 36);
        if (bounceDrop.State != ItemDropEffect.DropState.Grounded ||
            bounceDrop.ZFixed != 0 || bounceDrop.SpeedZ != 0 ||
            bounceDrop.Counter != 240 || !bounceDrop.CollisionEnabled)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not complete its original fixed-point bounce and start counter `$f0 on update 36.");
        }
        bounceDrop.Free();

        _player.RefillHealth();
        _player.ApplyDamage(4);
        var heartDrop = new ItemDropEffect();
        heartDrop.Initialize(
            ItemDropDatabase.Heart, dropPosition, _currentRoom, heartVisual);
        for (int frame = 1; frame <= 36; frame++)
            heartDrop.UpdateFrame(_player, frame);
        _player.WarpTo(dropPosition, recordSafe: false);
        heartDrop.UpdateFrame(_player, 37);
        if (!heartDrop.Collected || !heartDrop.Finished ||
            _player.HealthQuarters != _player.MaxHealthQuarters)
        {
            throw new InvalidOperationException(
                "Collecting ITEM_DROP_HEART did not restore four quarter-heart units and delete the drop.");
        }
        heartDrop.Free();

        ValidateRupeeItemDrop(oneRupeeVisual, ItemDropDatabase.OneRupee, 1, dropPosition);
        ValidateRupeeItemDrop(fiveRupeeVisual, ItemDropDatabase.FiveRupees, 5, dropPosition);

        _player.WarpTo(farPosition, recordSafe: false);
        var expiryDrop = new ItemDropEffect();
        expiryDrop.Initialize(
            ItemDropDatabase.OneRupee, dropPosition, _currentRoom, oneRupeeVisual);
        for (int frame = 1; frame <= 395; frame++)
            expiryDrop.UpdateFrame(_player, frame);
        if (expiryDrop.Counter != 60 || !expiryDrop.Visible || expiryDrop.Finished)
        {
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not retain visibility through countdown value 60.");
        }
        expiryDrop.UpdateFrame(_player, 396);
        expiryDrop.UpdateFrame(_player, 397);
        if (expiryDrop.Counter != 59 || expiryDrop.Visible)
            throw new InvalidOperationException("PART_ITEM_DROP did not begin flickering below counter 60.");
        expiryDrop.UpdateFrame(_player, 398);
        expiryDrop.UpdateFrame(_player, 399);
        if (!expiryDrop.Visible)
            throw new InvalidOperationException("PART_ITEM_DROP did not alternate visibility while flickering.");
        for (int frame = 400; frame <= 514; frame++)
            expiryDrop.UpdateFrame(_player, frame);
        if (expiryDrop.Finished || expiryDrop.Counter != 1)
            throw new InvalidOperationException("PART_ITEM_DROP expired before its 240th countdown tick.");
        expiryDrop.UpdateFrame(_player, 515);
        if (!expiryDrop.Finished || expiryDrop.Collected)
            throw new InvalidOperationException(
                "PART_ITEM_DROP did not expire after 240 alternating-frame ticks (480 update span).");
        expiryDrop.Free();

        ItemDropEffect transitionDrop = _entities.Spawn<ItemDropEffect>(
            new ItemDropSpawn(ItemDropDatabase.OneRupee, dropPosition));
        OracleRoomData incomingRoom = _world.LoadRoom(4, 0x39);
        _entities.BeginScreenTransition(4, incomingRoom, Vector2.Left * incomingRoom.Width);
        _entities.Update(1.0, _player);
        if (_entities.OutgoingEntities<ItemDropEffect>().Count != 1 ||
            _entities.OutgoingEntities<ItemDropEffect>()[0] != transitionDrop ||
            transitionDrop.ElapsedFrames != 0)
        {
            throw new InvalidOperationException(
                "Scrolling did not retain and freeze the outgoing PART_ITEM_DROP object.");
        }
        _entities.SetScreenTransitionOffsets(
            Vector2.Right * 4.0f,
            Vector2.Left * (incomingRoom.Width - 4.0f));
        if (!transitionDrop.TransitionDrawOffset.IsEqualApprox(Vector2.Right * 4.0f))
            throw new InvalidOperationException("The item drop did not move with its outgoing room.");
        _entities.FinishScreenTransition();
        if (_entities.OutgoingEntities<ItemDropEffect>().Count != 0)
            throw new InvalidOperationException("The outgoing item drop survived completed scrolling.");

        _player.RefillHealth();
        GD.Print("Validated all 144 enemy drop records, Keese `$ae probability/set data, " +
            "PART_ITEM_DROP heart/rupee visuals, -`$160 fixed-point bounce, pickup rewards, " +
            "240 alternating-frame lifetime ticks, final flicker, and frozen scrolling ownership.");
    }

    private void ValidateRupeeItemDrop(
        ItemDropDatabase.VisualRecord visual,
        int subId,
        int amount,
        Vector2 position)
    {
        int rupeesBefore = _player.Rupees;
        _player.WarpTo(position + Vector2.Right * 40.0f, recordSafe: false);
        var drop = new ItemDropEffect();
        drop.Initialize(subId, position, _currentRoom, visual);
        for (int frame = 1; frame <= 36; frame++)
            drop.UpdateFrame(_player, frame);
        _player.WarpTo(position, recordSafe: false);
        drop.UpdateFrame(_player, 37);
        if (!drop.Collected || _player.Rupees != Mathf.Min(999, rupeesBefore + amount) ||
            _hud.Rupees != _player.Rupees)
        {
            throw new InvalidOperationException(
                $"Collecting PART_ITEM_DROP ${subId:x2} did not add {amount} rupee(s) to Link and the HUD.");
        }
        drop.Free();
    }

    private static Vector2 FindOpenItemDropPosition(OracleRoomData room)
    {
        for (int y = 1; y < room.HeightInTiles - 1; y++)
        for (int x = 1; x < room.WidthInTiles - 1; x++)
        {
            Vector2 point = new(
                x * OracleRoomData.MetatileSize + 8,
                y * OracleRoomData.MetatileSize + 8);
            if (!room.IsSolid(point) &&
                room.GetTerrainInfo(point).Hazard == OracleRoomData.HazardType.None)
                return point;
        }
        throw new InvalidOperationException(
            $"Room {room.Group:x1}:{room.Id:x2} has no open item-drop validation position.");
    }

    private void ValidateTerrain()
    {
        _dialogue.Close();
        _player.RefillHealth();
        _activeGroup = 0;
        ClearDeactivatedWarp();

        _currentRoom = _world.LoadRoom(_activeGroup, 0xb8);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 waterSafe = new(40, 8);
        _player.WarpTo(waterSafe);
        _player.WarpTo(new Vector2(8, 8), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Water)
            throw new InvalidOperationException("Expected room b8/$00 to be water terrain.");
        ValidateDrowningSequence(waterSafe, OracleRoomData.HazardType.Water);

        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 lavaSafe = new(56, 8);
        _player.WarpTo(lavaSafe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Lava)
            throw new InvalidOperationException("Expected room 03/$10 to be lava terrain.");
        ValidateDrowningSequence(lavaSafe, OracleRoomData.HazardType.Lava);

        if (!TryFindTerrainSample(
            OracleRoomData.HazardType.Hole,
            out int holeGroup,
            out int holeRoom,
            out Vector2 holeCenter,
            out Vector2 holeSafe))
        {
            throw new InvalidOperationException("Could not find a testable hole terrain tile.");
        }

        _player.RefillHealth();
        _activeGroup = holeGroup;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, holeRoom);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(holeSafe);
        Vector2 offCenterHoleEntry = holeCenter + new Vector2(3, -2);
        int beforeHoleHealth = _player.HealthQuarters;
        _player.WarpTo(offCenterHoleEntry, recordSafe: false);
        Vector2 expectedHoleCenter = GetActiveTerrain(_player.Position).TileCenter;
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                $"Room {holeGroup:x1}:{holeRoom:x2} hole terrain did not start Link's pull-in state.");
        if (_player.HealthQuarters != beforeHoleHealth)
            throw new InvalidOperationException("Hole damage was applied before the pull/fall animation finished.");

        AdvanceHolePullUntilFall(expectedHoleCenter);
        AdvanceHoleFallUntilRespawn(holeSafe);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(holeSafe) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the last safe tile.");
        if (_player.HealthQuarters != beforeHoleHealth - 2)
            throw new InvalidOperationException("Hole terrain did not apply half-heart damage after respawn.");

        ValidateRoom01HoleBoundaryCase();

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x11);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 ledgeStart = new(24, 56);
        _player.WarpTo(ledgeStart);
        _player.Face(Vector2I.Down);
        if (!TryStartLedgeHop(_player, _player.Position, Vector2.Down))
            throw new InvalidOperationException("Room 11's south cliff did not start a ledge hop.");
        for (int i = 0; i < 30; i++)
            _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Position.Y <= ledgeStart.Y + OracleRoomData.MetatileSize)
            throw new InvalidOperationException("The ledge hop did not carry Link down across the cliff.");

        ValidateRoom56TileEdgeSlide();

        GD.Print("Validated terrain hazards, hole fall/respawn, ledge hop, and original tile-edge sliding.");
    }

    private void ValidateDrowningSequence(
        Vector2 safePosition,
        OracleRoomData.HazardType hazard)
    {
        string terrainName = hazard.ToString();
        Vector2 hazardPosition = _player.Position;
        int healthBeforeDrowning = _player.HealthQuarters;
        int worldChildCount = GetChildCount();

        _player._PhysicsProcess(1.0 / 60.0);
        SplashEffect? splash = _terrain.ActiveSplash;
        if (GetChildCount() != worldChildCount + 1 || splash is null ||
            splash.Position != hazardPosition ||
            splash.IsLava != (hazard == OracleRoomData.HazardType.Lava))
        {
            throw new InvalidOperationException(
                $"{terrainName} drowning did not create its original splash interaction at Link's position.");
        }
        int splashFrameDuration = splash.IsLava ? 2 : 4;
        int splashFrameCount = splash.IsLava ? 10 : 3;
        if (splash.DurationFrames != splashFrameDuration * splashFrameCount ||
            splash.AnimationFrame != 0)
        {
            throw new InvalidOperationException(
                $"{terrainName} splash did not start with the original interaction timing.");
        }
        splash.Advance((splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} splash did not hold its first OAM record for {splashFrameDuration} updates.");
        splash.Advance(1.0 / 60.0);
        if (splash.AnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} splash did not advance to its second OAM record.");
        splash.Advance((splash.DurationFrames - splashFrameDuration - 1.0) / 60.0);
        if (splash.AnimationFrame != splashFrameCount - 1 || splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not reach and hold its final OAM record.");
        splash.Advance(1.0 / 60.0);
        if (!splash.IsQueuedForDeletion())
            throw new InvalidOperationException(
                $"{terrainName} splash did not delete after {splash.DurationFrames} updates.");
        if (!_player.IsDrowning || !_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} terrain did not begin visible LINK_ANIM_MODE_DROWN frame $d4.");
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the drowning animation finished.");

        _player._PhysicsProcess(5.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 0)
            throw new InvalidOperationException(
                $"{terrainName} did not hold directional drowning frame $d4 for six updates.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.Visible || _player.DrownAnimationFrame != 1)
            throw new InvalidOperationException(
                $"{terrainName} did not advance to drowning frame $0b after six updates.");

        _player._PhysicsProcess(15.0 / 60.0);
        if (!_player.Visible || _player.Position != hazardPosition)
            throw new InvalidOperationException(
                $"{terrainName} moved or hid Link before the 22-update drowning animation finished.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible || !_player.IsDrowning)
            throw new InvalidOperationException(
                $"{terrainName} did not hide Link after the 22-update drowning animation.");
        if (_player.HealthQuarters != healthBeforeDrowning)
            throw new InvalidOperationException(
                $"{terrainName} damage was applied before the two-update respawn delay.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible)
            throw new InvalidOperationException(
                $"{terrainName} did not preserve the first invisible respawn update.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.IsDrowning || !_player.Visible ||
            _player.Position.DistanceSquaredTo(safePosition) > 1.0f)
        {
            throw new InvalidOperationException(
                $"{terrainName} did not return Link to the last safe tile after drowning.");
        }
        if (_player.HealthQuarters != healthBeforeDrowning - 2)
            throw new InvalidOperationException(
                $"{terrainName} did not apply one half-heart after Link reappeared.");
    }

    private void ValidateRoom56TileEdgeSlide()
    {
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x56);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();

        // The left end of bridge metatiles $1d/$1e starts at x=$30. These two
        // positions reproduce approaching its upper and lower rails from the
        // west, including the lower corner shown in the reported stuck case.
        ValidateBridgeSlidePath(new Vector2(44, 62), expectUp: true);
        ValidateBridgeSlidePath(new Vector2(44, 92), expectUp: false);

        // A fractional fixed-point remainder is possible when Link walks onto
        // the bridge after moving over slower terrain. At y=70.5 the upper
        // rail correctly blocks another upward step, while the original yh
        // coordinate (and therefore the rendered sprite) remains row 70.
        Vector2 fractionalStop = new(56, 70.5f);
        if (_collision.ResolveMovement(
                fractionalStop, Vector2.Up, allowWallSlide: true) != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "Room 0:56's upper bridge rail did not block the fractional direct approach.");
        }

        _player.WarpTo(fractionalStop, recordSafe: false);
        if (_player.Position != new Vector2(56, 70))
        {
            throw new InvalidOperationException(
                "Link's fixed-point bridge position was rounded instead of rendered from yh/xh.");
        }
    }

    private void ValidateBridgeSlidePath(Vector2 start, bool expectUp)
    {
        Vector2 position = start;
        bool deflected = false;
        for (int frame = 0; frame < 16; frame++)
        {
            Vector2 resolved = _collision.ResolveMovement(
                position, Vector2.Right, allowWallSlide: true);
            if (resolved == Vector2.Zero)
                throw new InvalidOperationException(
                    $"Room 0:56 bridge slide became stuck at {position}.");
            deflected |= expectUp ? resolved.Y < 0.0f : resolved.Y > 0.0f;
            position += resolved;
        }

        Vector2 expected = start + new Vector2(12.0f, expectUp ? -4.0f : 4.0f);
        if (!deflected || position.DistanceSquaredTo(expected) > 0.01f)
            throw new InvalidOperationException(
                $"Room 0:56 bridge did not apply the symmetric four-frame " +
                $"{(expectUp ? "upward" : "downward")} deflection; " +
                $"start={start}, expected={expected}, end={position}.");
    }

    private void ValidateRoom01HoleBoundaryCase()
    {
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x01);
        _roomView.SetRoom(_currentRoom.Texture);

        if (!TryFindHoleWithSafeNeighbor(_currentRoom, out Vector2 holeCenter, out Vector2 safePosition))
            throw new InvalidOperationException("Room 0:01 did not have a testable hole with a safe neighbor.");

        _player.RefillHealth();
        _player.WarpTo(safePosition);
        int beforeHealth = _player.HealthQuarters;

        float tileTop = Mathf.FloorToInt(holeCenter.Y / OracleRoomData.MetatileSize) *
            OracleRoomData.MetatileSize;
        Vector2 boundaryEntry = new(holeCenter.X, tileTop - 5.0f + 0.6f);
        _player.WarpTo(boundaryEntry, recordSafe: false);
        Vector2 expectedCenter = GetActiveTerrain(_player.Position).TileCenter;
        if (expectedCenter.DistanceSquaredTo(holeCenter) > 1.0f)
            throw new InvalidOperationException("Room 0:01 boundary setup did not sample the hole tile.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                "Room 0:01 boundary hole entry did not start the pull-in state.");

        AdvanceHolePullUntilFall(holeCenter);
        AdvanceHoleFallUntilRespawn(safePosition);
        if (_player.IsFallingInHole)
            throw new InvalidOperationException("Room 0:01 boundary hole fall did not complete.");
        if (_player.Position.DistanceSquaredTo(safePosition) > 1.0f)
            throw new InvalidOperationException("Room 0:01 hole respawn did not return to the room entry anchor.");
        if (_player.HealthQuarters != beforeHealth - 2)
            throw new InvalidOperationException("Room 0:01 hole fall did not apply half-heart damage.");
    }

    private void AdvanceHolePullUntilFall(Vector2 expectedCenter)
    {
        for (int i = 0; i < 120 && !_player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (!_player.IsFallingInHole)
            throw new InvalidOperationException("Hole pull-in did not transition to the fall animation.");
        if (_player.Position.DistanceSquaredTo(expectedCenter) > 1.0f)
            throw new InvalidOperationException("Hole pull-in did not center Link on the sampled hole tile.");
    }

    private void AdvanceHoleFallUntilRespawn(Vector2 expectedRespawn)
    {
        for (int i = 0; i < 80 && _player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (_player.IsFallingInHole)
            throw new InvalidOperationException("The falling-in-hole animation did not finish.");
        if (_player.Position.DistanceSquaredTo(expectedRespawn) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the stored respawn anchor.");
    }

    private static bool TryFindHoleWithSafeNeighbor(
        OracleRoomData room,
        out Vector2 holeCenter,
        out Vector2 safePosition)
    {
        for (int tileY = 1; tileY < room.HeightInTiles; tileY++)
        for (int tileX = 0; tileX < room.WidthInTiles; tileX++)
        {
            Vector2 center = new(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            OracleRoomData.TerrainInfo terrain = room.GetTerrainInfo(center);
            if (terrain.Hazard != OracleRoomData.HazardType.Hole)
                continue;

            foreach (Vector2I direction in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                Vector2 candidate = center + (Vector2)direction * OracleRoomData.MetatileSize;
                if (candidate.X < 0 || candidate.X >= room.Width ||
                    candidate.Y < 0 || candidate.Y >= room.Height)
                {
                    continue;
                }

                OracleRoomData.TerrainInfo safeTerrain = room.GetTerrainInfo(candidate);
                if (safeTerrain.Hazard == OracleRoomData.HazardType.None &&
                    !RoomCollides(room, candidate))
                {
                    holeCenter = center;
                    safePosition = candidate;
                    return true;
                }
            }
        }

        holeCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private bool TryFindTerrainSample(
        OracleRoomData.HazardType hazard,
        out int group,
        out int room,
        out Vector2 hazardCenter,
        out Vector2 safePosition)
    {
        for (int candidateGroup = 0; candidateGroup <= 5; candidateGroup++)
        for (int candidateRoom = 0; candidateRoom <= 0xff; candidateRoom++)
        {
            if (!_world.HasRoom(candidateGroup, candidateRoom))
                continue;

            OracleRoomData data = _world.LoadRoom(candidateGroup, candidateRoom);
            Vector2? safe = null;
            Vector2? target = null;

            for (int tileY = 0; tileY < data.HeightInTiles; tileY++)
            for (int tileX = 0; tileX < data.WidthInTiles; tileX++)
            {
                Vector2 center = new(
                    tileX * OracleRoomData.MetatileSize + 8,
                    tileY * OracleRoomData.MetatileSize + 8);
                OracleRoomData.TerrainInfo terrain = data.GetTerrainInfo(center);

                if (terrain.Hazard == OracleRoomData.HazardType.None &&
                    safe == null &&
                    !RoomCollides(data, center))
                {
                    safe = center;
                }
                if (terrain.Hazard == hazard &&
                    terrain.Type == OracleRoomData.TerrainType.Hole &&
                    !RoomCollides(data, center))
                {
                    target = center;
                }
            }

            if (safe != null && target != null)
            {
                group = candidateGroup;
                room = candidateRoom;
                hazardCenter = target.Value;
                safePosition = safe.Value;
                return true;
            }
        }

        group = -1;
        room = -1;
        hazardCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private static bool RoomCollides(OracleRoomData room, Vector2 playerPosition)
    {
        foreach (Vector2 offset in new[]
        {
            new Vector2(-5, -2), new Vector2(5, -2),
            new Vector2(-5, 5), new Vector2(5, 5)
        })
        {
            Vector2 sample = playerPosition + offset;
            if (sample.X < 0 || sample.X >= room.Width ||
                sample.Y < 0 || sample.Y >= room.Height ||
                room.IsSolid(sample))
            {
                return true;
            }
        }
        return false;
    }

    private void ValidateHealth()
    {
        _dialogue.Close();
        _player.RefillHealth();
        SyncHudToInventory();

        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12 ||
            _hud.MaxHealthQuarters != _player.MaxHealthQuarters)
            throw new InvalidOperationException("Expected Link and the HUD to start with three full hearts.");

        _player.ApplyDamage(1);
        if (_player.HealthQuarters != 11 || _hud.HealthQuarters != 11)
            throw new InvalidOperationException("Direct quarter-heart damage did not synchronize to the HUD.");

        _player.Heal(1);
        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12)
            throw new InvalidOperationException("Direct quarter-heart healing did not synchronize to the HUD.");

        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 safe = new(56, 8);
        _player.WarpTo(safe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);

        ValidateDrowningSequence(safe, OracleRoomData.HazardType.Lava);
        if (_player.HealthQuarters != 10 || _hud.HealthQuarters != 10)
            throw new InvalidOperationException(
                "Lava hazard did not synchronize its delayed half-heart damage to the HUD.");

        GD.Print("Validated quarter-heart health, HUD synchronization, and delayed half-heart terrain damage.");
    }

    private void ValidateAnimations()
    {
        OracleRoomData water = _world.LoadRoom(0, 0xb8);
        ulong waterStart = water.GetAnimationChecksum(0);
        bool waterChanged = false;
        for (int tick = 1; tick <= 120 && !waterChanged; tick++)
            waterChanged = water.GetAnimationChecksum(tick) != waterStart;

        OracleRoomData lava = _world.LoadRoom(0, 0x03);
        ulong lavaStart = lava.GetAnimationChecksum(0);
        bool lavaChanged = false;
        for (int tick = 1; tick <= 60 && !lavaChanged; tick++)
            lavaChanged = lava.GetAnimationChecksum(tick) != lavaStart;

        if (!waterChanged || !lavaChanged)
            throw new InvalidOperationException(
                $"Expected animated water and lava frames; water={waterChanged}, lava={lavaChanged}.");

        OracleRoomData waterfall = _world.LoadRoom(0, 0x45);
        const int settledWaterfallTick = 32;
        if (waterfall.AnimationGroup != 0 ||
            !waterfall.HasAnimationOverride(4, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(236, settledWaterfallTick) ||
            !waterfall.HasAnimationOverride(238, settledWaterfallTick))
        {
            throw new InvalidOperationException(
                "Waterfall animation did not preserve its three alternating VRAM destination writes.");
        }

        // Room textures are cached independently, but the original engine has
        // one shared animated-tile VRAM state. Prime 0:56 with a stale phase,
        // then verify that beginning the 0:55 -> 0:56 scroll synchronizes it
        // to the outgoing room before both textures are shown together.
        OracleRoomData target = _world.LoadRoom(0, 0x56);
        target.UpdateAnimation(0);
        int staleTargetSignature = target.CurrentAnimationSignature;
        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(0, 0x55);
        OracleRoomData source = _currentRoom;
        if (!Mathf.IsZeroApprox((float)_animationTicks))
            throw new InvalidOperationException("Changing animation groups did not reset their shared clock.");
        _animationTicks = 13.0;
        source.UpdateAnimation((long)_animationTicks);
        if (source.CurrentAnimationSignature == staleTargetSignature)
            throw new InvalidOperationException("Animation phase-lock validation chose indistinguishable ticks.");

        _roomView.SetRoom(source.Texture);
        _entities.LoadRoom(_activeGroup, source);
        _player.WarpTo(new Vector2(source.Width + 2.0f, source.Height / 2.0f));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x56)
            throw new InvalidOperationException("Room 0:55 did not begin its rightward scroll into 0:56.");
        if (source.AnimationGroup != _currentRoom.AnimationGroup ||
            source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
        {
            throw new InvalidOperationException(
                "Outgoing and incoming water/waterfall tiles began the scroll in different phases.");
        }

        ValidateLinkScrollsForOneTransitionFrame();
        if (source.CurrentAnimationSignature != _currentRoom.CurrentAnimationSignature)
            throw new InvalidOperationException("Animated-tile phases diverged during the frozen scroll.");
        FinishActiveScrollingTransitionForValidation();

        GD.Print("Validated disassembly-driven water and lava animation plus persistent " +
            "three-range waterfall VRAM updates and 0:55 -> 0:56 phase locking.");
    }

    private void ValidateSigns()
    {
        WarpToSignTest();
        if (_dialogue.VisibleLinesPerPage != 2 || _dialogue.TextLineSpacing != 16)
            throw new InvalidOperationException(
                "The textbox does not use the original two 8x16 text rows.");
        if (_currentRoom.GetMetatile(new Vector2(88, 58)) != 0xf2)
            throw new InvalidOperationException("Expected sign metatile $f2 in room 2a at $35.");
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 2a test sign did not open its dialogue.");

        bool arrowBefore = _dialogue.ArrowVisible;
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (_dialogue.ArrowVisible == arrowBefore)
            throw new InvalidOperationException("The textbox arrow did not toggle after 16 original-engine frames.");
        _dialogue.AdvanceArrowClockForValidation(16.0 / 60.0);
        if (_dialogue.ArrowVisible != arrowBefore)
            throw new InvalidOperationException("The textbox arrow did not complete its 32-frame blink cycle.");

        _dialogue.ShowMessage("First.\nSecond.\nThird.\nFourth.", _player.Position.Y);
        _dialogue.AdvanceOrClose();
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f))
        {
            throw new InvalidOperationException(
                "The button frame did not perform standardTextStateb's first 8px shift.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!_dialogue.IsScrollingText ||
            !Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 8.0f))
        {
            throw new InvalidOperationException(
                "The standard textbox DMA state did not hold the first 8px shift for one frame.");
        }
        _dialogue.AdvanceTextScrollForValidation(1.0 / 60.0);
        if (!Mathf.IsEqualApprox(_dialogue.TextScrollOffset, 16.0f))
            throw new InvalidOperationException("standardTextState7 did not perform the second 8px shift.");
        _dialogue.AdvanceTextScrollForValidation(5.0 / 60.0);
        if (_dialogue.IsScrollingText || !Mathf.IsZeroApprox(_dialogue.TextScrollOffset))
            throw new InvalidOperationException(
                "The two discrete tile-row scroll sequences did not finish in seven frames.");

        _dialogue.ShowMessage("Last line.", _player.Position.Y);
        _dialogue.AdvanceOrClose();
        if (_dialogue.IsOpen || !_dialogue.BlocksPlayerInput)
            throw new InvalidOperationException("Closing the final textbox did not consume its button press.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_dialogue.IsOpen)
            throw new InvalidOperationException("The final textbox press immediately restarted the interaction.");

        GD.Print("Validated dialogue spacing, discrete tile-row text scroll, 32-frame arrow blink, and final-page input consumption.");
    }

    private void ValidateNpcs()
    {
        WarpToNpcTest();
        NpcCharacter? villager = _npcNodes.Find(npc => npc.Record.Id == 0x3a && npc.Record.SubId == 0x03);
        if (villager is null)
            throw new InvalidOperationException($"Expected the room 0:48 villager among {_npcNodes.Count} extracted NPCs.");
        if (villager.TextId != 0x1420)
            throw new InvalidOperationException($"Expected room 0:48 villager to resolve TX_1420, got TX_{villager.TextId:x4}.");
        if (villager.Position != new Vector2(0x38, 0x48) ||
            villager.SpriteBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager sprite is not centered on its object tile.");
        }
        if (villager.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            villager.ObjectCollisionBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager object hitbox does not match the original $06/$06 collision radii.");
        }
        if (villager.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            villager.LinkBlockingBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager does not combine NPC and Link $06 radii into a 24px blocking region.");
        }
        if (!villager.BlocksLinkCenter(villager.Position) ||
            villager.BlocksLinkCenter(villager.Position + new Vector2(0.0f, 12.0f)))
        {
            throw new InvalidOperationException("The room 0:48 villager's strict radius collision boundary is not centered correctly.");
        }
        if (!Collides(villager.Position + new Vector2(0.0f, 11.9f)) ||
            Collides(villager.Position + new Vector2(0.0f, 12.1f)))
        {
            throw new InvalidOperationException("The room 0:48 villager did not stop Link at the original bottom collision radius.");
        }
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 0:48 villager did not open dialogue.");
        int frameBase = villager.Record.TileBase / 4;
        if (villager.CurrentFrameColumn != frameBase)
            throw new InvalidOperationException("The room 0:48 villager did not face down toward Link after talking.");
        villager.UpdateNpc(16.0 / 60.0, _player.Position);
        if (villager.CurrentAnimationFrame != 1)
            throw new InvalidOperationException("The room 0:48 villager did not advance its original 16-frame idle animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Left * 20.0f);
        if (villager.FacingVector != Vector2I.Left || villager.CurrentAnimationFrame != 0)
            throw new InvalidOperationException("The room 0:48 villager did not face nearby Link and reset its animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Left)
            throw new InvalidOperationException("The NPC facing cooldown did not preserve the current direction.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Right || villager.CurrentFrameColumn != frameBase + 2)
            throw new InvalidOperationException("The villager did not use the mirrored side OAM after the 30-frame facing delay.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 80.0f);
        if (villager.FacingVector != Vector2I.Down)
            throw new InvalidOperationException("The villager did not return to facing down when Link left the $28 awareness radius.");

        _dialogue.Close();
        _transitions.BeginScroll(_player, Vector2I.Down, 0x58);
        NpcCharacter? destinationNpc = _npcNodes.Find(npc =>
            npc.Record.Room == 0x58 && npc.Record.Id == 0x41 && npc.Record.SubId == 0x04);
        if (!_entities.ScreenTransitionActive || _currentRoom.Id != 0x58 ||
            _entities.OutgoingEntities<NpcCharacter>().Count != 2 || destinationNpc is null)
        {
            throw new InvalidOperationException(
                $"The 0:48 -> 0:58 scroll did not retain two outgoing NPCs and preload the destination NPC " +
                $"(active={_entities.ScreenTransitionActive}, room={_currentRoom.Id:x2}, " +
                $"outgoing={_entities.OutgoingEntities<NpcCharacter>().Count}, incoming={_npcNodes.Count}, " +
                $"destinationFound={destinationNpc is not null}).");
        }
        foreach (NpcCharacter outgoingNpc in _entities.OutgoingEntities<NpcCharacter>())
        {
            if (outgoingNpc.Record.Room != 0x48 ||
                !outgoingNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Zero))
            {
                throw new InvalidOperationException(
                    "An outgoing room 0:48 NPC was not retained at its initial screen position.");
            }
        }
        if (!destinationNpc.TransitionDrawOffset.IsEqualApprox(
            Vector2.Down * OracleRoomData.ViewportHeight))
        {
            throw new InvalidOperationException(
                "The room 0:58 NPC was not staged one screen below the outgoing room.");
        }

        UpdateScrollingTransition(1.0 / 60.0);
        foreach (NpcCharacter outgoingNpc in _entities.OutgoingEntities<NpcCharacter>())
        {
            if (!outgoingNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Up * 4.0f))
                throw new InvalidOperationException("An outgoing NPC did not move with its scrolling room.");
        }
        if (!destinationNpc.TransitionDrawOffset.IsEqualApprox(
            Vector2.Down * (OracleRoomData.ViewportHeight - 4.0f)))
        {
            throw new InvalidOperationException("The preloaded destination NPC did not scroll into view with room 0:58.");
        }

        FinishActiveScrollingTransitionForValidation();
        if (_entities.ScreenTransitionActive || _entities.OutgoingEntities<NpcCharacter>().Count != 0 ||
            !destinationNpc.TransitionDrawOffset.IsEqualApprox(Vector2.Zero) ||
            destinationNpc.GetParent() != this)
        {
            throw new InvalidOperationException(
                "The destination NPC did not become the normal room NPC after the scroll completed.");
        }

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x66);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        NpcCharacter? woman = _npcNodes.Find(npc =>
            npc.Record.Id == 0x3b && npc.Record.SubId == 0x01);
        if (woman is null || woman.Position != new Vector2(0x7a, 0x44))
            throw new InvalidOperationException("Expected female villager $3b/$01 at 0:66 $44/$7a.");

        woman.UpdateDrawPriority(woman.Position - Vector2.Down * 12.0f);
        if (woman.ZIndex != NpcCharacter.InFrontOfLinkZIndex)
            throw new InvalidOperationException(
                "Room 0:66's woman did not cover Link when yh exceeded w1Link.yh+$0b.");
        woman.UpdateDrawPriority(woman.Position - Vector2.Down * 11.0f);
        if (woman.ZIndex != NpcCharacter.BehindLinkZIndex)
            throw new InvalidOperationException(
                "Room 0:66's woman covered Link at the strict w1Link.yh+$0b boundary.");

        Color linkBlack = Player.RecolorLinkPixel(new Color(0.25f, 0.25f, 0.25f));
        Color linkGreen = Player.RecolorLinkPixel(new Color(0.75f, 0.75f, 0.75f));
        Color linkSkin = Player.RecolorLinkPixel(Colors.White);
        if (!linkBlack.IsEqualApprox(Colors.Black) ||
            !linkGreen.IsEqualApprox(new Color(0x02 / 31.0f, 0x15 / 31.0f, 0x08 / 31.0f)) ||
            !linkSkin.IsEqualApprox(new Color(0x1f / 31.0f, 0x1a / 31.0f, 0x11 / 31.0f)))
        {
            throw new InvalidOperationException(
                "Link did not use standardSpritePaletteData palette 0 selected by OAM flags $08.");
        }

        GD.Print("Validated villager idle animation, $28 Link awareness, 30-frame facing delay, " +
            "TX_1420 dialogue, retained/preloaded NPC screen scrolling, room 0:66 " +
            "Link-relative draw priority, and Link sprite palette 0.");
    }

    private void ValidateMakuTreeDisappearanceCutscene()
    {
        LoadValidationRoom(0, 0x38);
        // The event is entered through room position $52 (the open $dc tile),
        // before its simulated right/up approach takes over.
        _player.WarpTo(new Vector2(0x28, 0x58));
        NpcCharacter? makuTree = _npcNodes.Find(npc =>
            npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        if (makuTree is null || !_roomEvents.Active || !makuTree.Active ||
            makuTree.Position != new Vector2(0x50, 0x40))
        {
            throw new InvalidOperationException(
                "Room 0:38 did not start the $87:$01 Maku Tree entry event at $40/$50.");
        }
        if (makuTree.CurrentAnimationTextureSize.X <= 32 ||
            makuTree.CurrentAnimationTextureSize.Y <= 32 ||
            makuTree.CurrentAnimationOffset.Y >= -16)
        {
            throw new InvalidOperationException(
                $"The Maku Tree face OAM was clipped to an ordinary NPC canvas " +
                $"(size={makuTree.CurrentAnimationTextureSize}, offset={makuTree.CurrentAnimationOffset}).");
        }

        Vector2 inputStart = _player.Position;
        StepRoomEventFrames(60);
        if (_player.Position != inputStart)
            throw new InvalidOperationException("Maku Tree simulated input did not begin with 60 idle updates.");
        StepRoomEventFrames(48);
        if (_roomEvents.InputFrame != 108 || _player.FacingVector != Vector2I.Right ||
            _player.Position.X <= inputStart.X)
        {
            throw new InvalidOperationException(
                $"Maku Tree simulated input did not hold BTN_RIGHT for exactly 48 updates " +
                $"(input={_roomEvents.InputFrame}, facing={_player.FacingVector}, " +
                $"start={inputStart}, current={_player.Position}).");
        }
        StepRoomEventFrames(4);
        Vector2 beforeUp = _player.Position;
        StepRoomEventFrames(14);
        if (_roomEvents.InputFrame != 126 || _player.FacingVector != Vector2I.Up ||
            _player.Position.Y >= beforeUp.Y)
        {
            throw new InvalidOperationException(
                "Maku Tree simulated input did not hold BTN_UP for exactly 14 updates.");
        }
        StepRoomEventFrames(84);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 80 ||
            !_dialogue.CurrentMessage.StartsWith("Pleased to meet\nyou, young hero."))
        {
            throw new InvalidOperationException(
                "TX_0564 did not open after the original 210-update introduction delay.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(60);
        if (makuTree.CurrentAnimationFrame != 0 || makuTree.CurrentAnimationOpaquePixels == 0)
            throw new InvalidOperationException(
                "The Maku Tree frown animation did not reset to a visible frame zero.");
        StepRoomEventFrames(4);
        if (makuTree.CurrentAnimationFrame != 1)
            throw new InvalidOperationException(
                "INTERAC_MAKU_TREE animation 4 did not use its original four-update first frame.");
        StepRoomEventFrames(56);

        int paletteBefore = _roomEvents.PaletteHeader;
        StepRoomEventFrames(8);
        if (_roomEvents.PaletteHeader == paletteBefore)
            throw new InvalidOperationException(
                "The $9a/$c4/$8f/$c5 Maku Tree palettes did not cycle within eight updates.");
        StepRoomEventFrames(202);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 80 || _dialogue.CurrentMessage != "Ahh...")
            throw new InvalidOperationException("TX_0540 did not open after 210 disappearance updates.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(210);
        if (!_dialogue.IsOpen || _dialogue.Position.Y != 80 ||
            !_dialogue.CurrentMessage.StartsWith("I feel so weird.\nI'm vanishing!"))
            throw new InvalidOperationException("TX_0541 did not follow the original 210-update pause.");

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(150);
        if (!_roomEvents.Completed || _roomEvents.Active || !IsTransitioning ||
            _activeGroup != 0 || _currentRoom.Id != 0x38 ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared) ||
            !_saveData.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            _saveData.MakuTreeState != 1)
        {
            throw new InvalidOperationException(
                "The Maku Tree event did not persist GLOBALFLAG_0c, wMakuTreeState, room bit 0, " +
                "and initiate its hardcoded same-room warp after 150 updates.");
        }
        if (_currentRoom.TilesetId != 0x22 || !makuTree.Active || _warpFade.Color.A != 0.0f)
        {
            throw new InvalidOperationException(
                "The delayed $83 fade replaced room 0:38 before beginning its transition to white.");
        }

        for (int frame = 0; frame < RoomTransitionController.DelayedWarpFadeFrames - 1; frame++)
            UpdateRoomWarpTransition(1.0 / 60.0);
        if (_currentRoom.TilesetId != 0x22 || _warpFade.Color.A <= 0.9f ||
            _warpFade.Color.A >= 1.0f)
        {
            throw new InvalidOperationException(
                $"The $83 delayed fade did not retain the old layout for 124 updates " +
                $"(tileset={_currentRoom.TilesetId:x2}, alpha={_warpFade.Color.A}).");
        }
        UpdateRoomWarpTransition(1.0 / 60.0);
        NpcCharacter? reloadedTree = _npcNodes.Find(npc =>
            npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        if (reloadedTree is null || reloadedTree.Active ||
            !_rooms.IsLayoutSwapped(0, 0x38) || _currentRoom.TilesetId != 0x23 ||
            _currentRoom.GetMetatile(new Vector2(0x48, 0x28)) != 0xf9)
        {
            throw new InvalidOperationException(
                $"Room flag bit 0 did not load group 2's tree-less room 0:38 layout and suppress $87 " +
                $"(tree={reloadedTree is not null}/{reloadedTree?.Active}, " +
                $"swap={_rooms.IsLayoutSwapped(0, 0x38)}, tileset={_currentRoom.TilesetId:x2}, " +
                $"tile24={_currentRoom.GetMetatile(new Vector2(0x48, 0x28)):x2}).");
        }

        for (int frame = 0; frame < WarpFadeFrames; frame++)
            UpdateRoomWarpTransition(1.0 / 60.0);
        if (IsTransitioning || _player.Position != new Vector2(0x58, 0x48))
        {
            throw new InvalidOperationException(
                $"The room $38/$45 hardcoded warp did not remain at $48/$58; got {_player.Position}.");
        }
        _player._PhysicsProcess(1.0 / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x38 || IsTransitioning)
            throw new InvalidOperationException(
                "The $45 re-entry incorrectly walked Link into room 0:38's $ee/$ef warp to 5:cf.");

        LoadValidationRoom(0, 0x38);
        reloadedTree = _npcNodes.Find(npc => npc.Record.Id == 0x87 && npc.Record.SubId == 0x00);
        if (_roomEvents.Active || reloadedTree is null || reloadedTree.Active)
            throw new InvalidOperationException("The completed Maku Tree entry event retriggered on room reload.");

        GD.Print("Validated room 0:38 Maku Tree $87:$01 simulated input, two-sheet unclipped face OAM, " +
            "fixed-bottom \\pos(2) dialogue, 210/60/60/210/210/150 timing, four-header " +
            "palette cycle, delayed 125-update white fade, and one-shot $45 re-entry warp.");
    }

    private void StepRoomEventFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
    }

    private void ValidateRalphPortalDepartureEvent()
    {
        // @initSubid0d deletes the object on a direct room load because
        // wScreenTransitionDirection is not DIR_RIGHT ($01).
        LoadValidationRoom(0, 0x39);
        NpcCharacter? directRalph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (directRalph is null || directRalph.Active || _roomEvents.Active)
        {
            throw new InvalidOperationException(
                "INTERAC_RALPH $37:$0d ignored its DIR_RIGHT room-entry guard.");
        }

        LoadValidationRoom(0, 0x38);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x39);
        NpcCharacter? ralph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (ralph is null || !ralph.Active || !_roomEvents.Active ||
            !_roomEvents.RalphWaitingForScroll || !_entities.ScreenTransitionActive ||
            ralph.Position != new Vector2(0x18, 0x28))
        {
            throw new InvalidOperationException(
                "Room 0:39 did not retain Ralph at $28/$18 while entering from the left.");
        }

        FinishActiveScrollingTransitionForValidation();
        if (!_roomEvents.RalphWaitingForScroll || _player.CutsceneControlled)
            throw new InvalidOperationException(
                "Ralph's script began before the destination-room scroll finished.");

        StepRoomEventFrames(1);
        if (!_player.CutsceneControlled || _roomEvents.Counter != 40)
            throw new InvalidOperationException(
                "Ralph did not disable input and install his original 40-update wait.");
        StepRoomEventFrames(39);
        if (_dialogue.IsOpen || _roomEvents.Counter != 1)
            throw new InvalidOperationException("Ralph's introductory wait ended early.");
        StepRoomEventFrames(1);
        if (!_dialogue.IsOpen || _dialogue.CurrentMessage !=
            "The Maku Tree?\nThis is more\nof Veran's work!\nLink! You made\nit! Veran just\nleapt through\nthis Time\nPortal! If we go\nback in time, we\nshould be able\nto save Nayru\nand the Maku\nTree! I'm\ncoming, Nayru!")
        {
            throw new InvalidOperationException(
                "TX_2a1e did not open after Ralph's original 40-update wait.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(29);
        if (_roomEvents.Counter != 1 || ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException("Ralph's post-text 30-update wait ended early.");
        StepRoomEventFrames(1);
        if (ralph.CurrentAnimationFrame != 0)
            throw new InvalidOperationException(
                "Ralph did not select animation $01 after the post-text wait.");
        StepRoomEventFrames(2);
        if (ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException(
                "Ralph moved during the setspeed/setangle script-command updates.");
        StepRoomEventFrames(1);
        if (_roomEvents.Counter != 17 || ralph.Position != new Vector2(0x18, 0x28))
            throw new InvalidOperationException(
                "Ralph did not install applyspeed counter $11 on its own script update.");

        StepRoomEventFrames(12);
        if (ralph.Position != new Vector2(0x24, 0x28) ||
            ralph.CurrentAnimationFrame != 0 || _roomEvents.Counter != 5)
        {
            throw new InvalidOperationException(
                "Ralph's SPEED_100 movement or animation $01 first-frame duration diverged.");
        }
        StepRoomEventFrames(1);
        if (ralph.Position != new Vector2(0x25, 0x28) ||
            ralph.CurrentAnimationFrame != 1 || _roomEvents.Counter != 4)
        {
            throw new InvalidOperationException(
                "Ralph's animation $01 did not change after its original 16 updates.");
        }
        StepRoomEventFrames(2);
        if (ralph.Position != new Vector2(0x27, 0x28) || _roomEvents.Counter != 2)
            throw new InvalidOperationException("Ralph's SPEED_100 movement skipped an update.");
        StepRoomEventFrames(1);
        if (ralph.Position != new Vector2(0x28, 0x28) ||
            _roomEvents.Counter != 1)
        {
            throw new InvalidOperationException(
                "applyspeed $11 did not move Ralph exactly 16 pixels to the portal.");
        }
        StepRoomEventFrames(1);
        if (_roomEvents.Counter != 0 || _roomEvents.RalphFlickering ||
            ralph.Position != new Vector2(0x28, 0x28))
        {
            throw new InvalidOperationException(
                "Ralph's counter2 path did not pause for one update after reaching zero.");
        }
        StepRoomEventFrames(1);
        if (ralph.CurrentAnimationFrame != 0 || _roomEvents.RalphFlickering)
            throw new InvalidOperationException("Ralph did not select portal animation $09.");
        StepRoomEventFrames(2);
        if (_roomEvents.Counter != 45 || _roomEvents.RalphFlickering)
            throw new InvalidOperationException(
                "Ralph's var3f=$2d and SND_MYSTERY_SEED commands lost their script updates.");
        StepRoomEventFrames(1);
        bool firstFlickerVisibility = (_entities.FrameCounter & 1) != 0;
        if (!_roomEvents.RalphFlickering || _roomEvents.Counter != 44 ||
            ralph.CurrentAnimationFrame != 0 || ralph.Visible != firstFlickerVisibility ||
            _roomEvents.RalphCompleted)
        {
            throw new InvalidOperationException(
                "Ralph did not select animation $09 and begin the $2d-frame parity flicker.");
        }
        StepRoomEventFrames(1);
        if (ralph.Visible == firstFlickerVisibility || _roomEvents.Counter != 43)
            throw new InvalidOperationException(
                "Ralph's objectFlickerVisibility b=$01 did not alternate every update.");
        StepRoomEventFrames(42);
        if (!_roomEvents.Active || _roomEvents.Counter != 1 ||
            _roomEvents.RalphCompleted || !ralph.Active)
        {
            throw new InvalidOperationException(
                "Ralph's $2d-frame portal flicker completed one update early.");
        }
        StepRoomEventFrames(1);
        if (_roomEvents.Active || !_roomEvents.RalphCompleted || ralph.Active ||
            _player.CutsceneControlled ||
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagRalphEnteredPortal))
        {
            throw new InvalidOperationException(
                "Ralph's departure did not set GLOBALFLAG_RALPH_ENTERED_PORTAL $40 and restore input.");
        }

        LoadValidationRoom(0, 0x39);
        NpcCharacter? completedRalph = _npcNodes.Find(npc =>
            npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d);
        if (completedRalph is null || completedRalph.Active || _roomEvents.Active)
            throw new InvalidOperationException("Ralph's one-shot portal event retriggered after flag $40.");

        GD.Print("Validated room 0:39 Ralph $37:$0d DIR_RIGHT guard, TX_2a1e, " +
            "40/30 waits, per-command script cadence, animation $01, 16-pixel SPEED_100 " +
            "movement, animation $09, " +
            "$2d-frame flicker, and persistent GLOBALFLAG $40.");
    }

    private void ValidateStartupTransition()
    {
        if (_currentRoom.Id != 0x11)
            throw new InvalidOperationException("The transition validation expects startup room 11.");

        // Room 11's top staircase is metatile $d0 at column 4. This position
        // crosses the same collision samples and room-exit code as player input.
        Vector2 exitPosition = new(4 * OracleRoomData.MetatileSize + 8, -2);
        for (float y = _player.Position.Y; y >= exitPosition.Y; y -= 2)
        {
            if (Collides(new Vector2(exitPosition.X, y)))
                throw new InvalidOperationException(
                    $"Room 11's path to the top staircase is blocked at y={y}.");
        }

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x01)
            throw new InvalidOperationException(
                $"Expected room 01 after the startup transition, got {_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x74)
            throw new InvalidOperationException(
                $"Expected Link to finish the 11 -> 01 transition near $74, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        GD.Print("Validated original-style transition 11 -> 01 through staircase collision $18.");
    }

    private void ValidateSymmetryTransition()
    {
        if (_currentRoom.Id != 0x22)
            throw new InvalidOperationException("The Symmetry transition validation expects room 22.");

        int oldTileset = _currentRoom.TilesetId;
        Vector2 exitPosition = new(3 * OracleRoomData.MetatileSize + 8, -2);
        if (Collides(exitPosition))
            throw new InvalidOperationException("Room 22's north staircase is blocked.");

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x12 || _currentRoom.TilesetId == oldTileset)
            throw new InvalidOperationException(
                $"Expected room 12 / a new tileset, got {_currentRoom.Id:x2} / {_currentRoom.TilesetId:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        GD.Print($"Validated cross-tileset transition 22 ({oldTileset:x2}) -> " +
            $"12 ({_currentRoom.TilesetId:x2}).");
    }

    private void ValidateLinkScrollsForOneTransitionFrame()
    {
        if (!IsTransitioning)
            return;

        double animationTickBefore = _animationTicks;
        UpdateAnimatedTiles(1.0 / 60.0);
        if (!Mathf.IsEqualApprox((float)_animationTicks, (float)animationTickBefore))
            throw new InvalidOperationException("Animated tiles advanced during a room transition.");

        Vector2 position = _player.Position;
        UpdateScrollingTransition(1.0 / 60.0);
        Vector2 moved = _player.Position - position;
        Vector2 scrollDirection = -(Vector2)_scrollTransitionDirection;
        if (moved.Dot(scrollDirection) <= 0.0f)
            throw new InvalidOperationException("Link did not scroll with the screen transition.");
    }

    private void FinishActiveScrollingTransitionForValidation()
    {
        for (int i = 0; i < 80 && IsTransitioning; i++)
            UpdateScrollingTransition(1.0 / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("Scrolling transition did not finish within 80 frames.");
    }
}
