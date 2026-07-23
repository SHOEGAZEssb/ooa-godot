using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMainMenu()
    {
        var stored = new OracleSaveData?[OracleSaveStore.SlotCount];
        var screen = new MainMenuScreen { Name = "MainMenuValidation" };
        AddChild(screen);
        int startedSlot = -1;
        OracleSaveData? startedSave = null;
        var menu = new MainMenuController(
            screen,
            (slot, save) => { startedSlot = slot; startedSave = save; },
            slot => stored[slot],
            (slot, save) =>
            {
                stored[slot] = save;
                return SaveResult.Succeeded;
            },
            slot => stored[slot] = null);

        if (MainMenuScreen.InterleavedSourceTileForValidation(0, 16) != 0 ||
            MainMenuScreen.InterleavedSourceTileForValidation(1, 16) != 16 ||
            MainMenuScreen.InterleavedSourceTileForValidation(2, 16) != 1 ||
            !screen.FileNameStripColorForValidation.IsEqualApprox(Colors.Black) ||
            !screen.FilePanelColorForValidation.IsEqualApprox(
                screen.DeathTileBackgroundColorForValidation) ||
            !screen.EraseBackgroundAccentForValidation.IsEqualApprox(
                new Color(0x10 / 31.0f, 0x16 / 31.0f, 0x1b / 31.0f)) ||
            !screen.EraseFilePanelColorForValidation.IsEqualApprox(
                screen.EraseDeathTileBackgroundColorForValidation) ||
            screen.EraseFilePanelColorForValidation.IsEqualApprox(
                screen.FilePanelColorForValidation) ||
            !screen.NameEntryFieldColorForValidation.IsEqualApprox(
                screen.NameEntryPanelColorForValidation) ||
            !screen.NameEntryHighlightIsGlyphMaskForValidation ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.White, inverted: false) != 0 ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.Black, inverted: false) != 3 ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.Black, inverted: true) != 0 ||
            MainMenuScreen.SpriteColorIndexForValidation(Colors.White, inverted: true) != 3 ||
            MainMenuScreen.TextSpeedCursorPositionForValidation(0) != new Vector2(41, 128) ||
            MainMenuScreen.TextSpeedCursorPositionForValidation(4) != new Vector2(105, 128))
        {
            throw new InvalidOperationException(
                "File-select tile interleave, normal/PALH_06 erase panel fill, " +
                "filename/name-entry fields, " +
                "name cursor priority mask, sprite inversion, " +
                "or original message-speed cursor coordinates regressed.");
        }

        menu.BeginTitleStart();
        for (int frame = 0; frame < MainMenuController.WhiteFadeFrames - 1; frame++)
            menu.Update(1.0 / 60.0);
        if (menu.CurrentPage != Page.Title)
            throw new InvalidOperationException("The title white fade ended before its original 32 updates.");
        menu.Update(1.0 / 60.0);
        if (menu.CurrentPage != Page.FileSelect)
            throw new InvalidOperationException("The title did not enter file select after its 32-update white fade.");
        menu.Move(Vector2I.Up);
        if (menu.Cursor != 3)
            throw new InvalidOperationException("File-select Up did not wrap from file 1 to the bottom row.");
        menu.Move(Vector2I.Right);
        if (screen.Choice != 1)
            throw new InvalidOperationException("File-select Copy/Erase horizontal selection did not toggle.");
        menu.Accept();
        if (menu.CurrentPage != Page.EraseSelect ||
            !screen.CurrentDeathTileBackgroundColorForValidation.IsEqualApprox(
                screen.EraseDeathTileBackgroundColorForValidation))
        {
            throw new InvalidOperationException(
                "Erase selection did not swap the file background and HUD tiles to PALH_06.");
        }
        menu.Back();
        if (menu.CurrentPage != Page.FileSelect ||
            !screen.CurrentDeathTileBackgroundColorForValidation.IsEqualApprox(
                screen.DeathTileBackgroundColorForValidation))
        {
            throw new InvalidOperationException(
                "Leaving erase selection did not restore the normal PALH_05 file palette.");
        }

        screen.SetCursor(0);
        menu.Accept();
        if (menu.CurrentPage != Page.NewFileOptions)
            throw new InvalidOperationException("A blank slot did not open New Game/Secret/Game Link.");
        menu.Accept();
        if (menu.CurrentPage != Page.NameEntry)
            throw new InvalidOperationException("New Game did not open file name entry.");
        if (!screen.TryGetSelectedNameCharacter(out char initialCharacter) ||
            initialCharacter != 'A')
            throw new InvalidOperationException(
                "The original US name keyboard did not begin on uppercase A.");
        for (int column = 0; column < 6; column++)
            screen.MoveNameCursor(Vector2I.Right);
        if (!screen.TryGetSelectedNameCharacter(out char lowercaseCharacter) ||
            lowercaseCharacter != 'a')
            throw new InvalidOperationException(
                "Name entry did not preserve the original six-column uppercase/lowercase split.");
        for (int column = 0; column < 6; column++)
            screen.MoveNameCursor(Vector2I.Left);
        foreach (char character in "LINK")
            screen.AppendNameCharacter(character);
        for (int row = 0; row < 5; row++)
            screen.MoveNameCursor(Vector2I.Down);
        screen.MoveNameCursor(Vector2I.Right);
        screen.MoveNameCursor(Vector2I.Right);
        if (screen.NameCursor != 0x56 || screen.NameLowerChoice != 2)
            throw new InvalidOperationException(
                "Name entry did not map the five character rows onto its original OK option.");
        menu.Accept();
        if (stored[0]?.LinkName != "LINK" || menu.CurrentPage != Page.FileSelect)
            throw new InvalidOperationException("Name entry did not initialize and save the selected standard file.");

        screen.SetCursor(0);
        menu.Accept();
        if (menu.CurrentPage != Page.TextSpeed || screen.TextSpeed != 4 ||
            screen.Cursor != 0 || screen.SelectedSlot != 0)
            throw new InvalidOperationException("An existing file did not open its message-speed confirmation.");
        menu.Move(Vector2I.Left);
        menu.Accept();
        for (int frame = 0; frame < MainMenuController.WhiteFadeFrames - 1; frame++)
            menu.Update(1.0 / 60.0);
        if (startedSave is not null)
            throw new InvalidOperationException("File select started gameplay before its 32-update white fade.");
        menu.Update(1.0 / 60.0);
        if (startedSlot != 0 || startedSave != stored[0] || stored[0]!.TextSpeed != 3)
            throw new InvalidOperationException("Message-speed confirmation did not save and start the chosen file.");

        var copyScreen = new MainMenuScreen { Name = "MainMenuCopyValidation" };
        AddChild(copyScreen);
        var copyMenu = new MainMenuController(
            copyScreen, (_, _) => { }, slot => stored[slot],
            (slot, save) =>
            {
                stored[slot] = save;
                return SaveResult.Succeeded;
            },
            slot => stored[slot] = null);
        copyMenu.OpenFileSelect();
        copyScreen.SetCursor(3);
        copyMenu.Accept();
        copyScreen.SetCursor(0);
        copyMenu.Accept();
        copyMenu.Accept();
        copyMenu.Move(Vector2I.Right);
        copyMenu.Accept();
        if (stored[1]?.LinkName != "LINK" || ReferenceEquals(stored[0], stored[1]))
            throw new InvalidOperationException("Copy did not clone the original $550-byte file into its destination.");

        copyScreen.SetCursor(3);
        copyScreen.SetChoice(1);
        copyMenu.Accept();
        copyScreen.SetCursor(1);
        copyMenu.Accept();
        copyMenu.Move(Vector2I.Right);
        copyMenu.Accept();
        if (stored[1] is not null)
            throw new InvalidOperationException("Erase confirmation did not clear the selected file slot.");

        bool startedAfterFailure = false;
        var failureScreen = new MainMenuScreen { Name = "MainMenuSaveFailureValidation" };
        AddChild(failureScreen);
        var failureMenu = new MainMenuController(
            failureScreen,
            (_, _) => startedAfterFailure = true,
            slot => stored[slot],
            (_, _) => SaveResult.Failed("validation failure"));
        failureMenu.OpenFileSelect();
        failureScreen.SetCursor(0);
        failureMenu.Accept();
        failureMenu.Accept();
        if (failureMenu.CurrentPage != Page.TextSpeed ||
            !failureScreen.SaveErrorVisible ||
            failureMenu.LastSaveError != "validation failure" || startedAfterFailure)
        {
            throw new InvalidOperationException(
                "A file-select save failure escaped, changed page, or began gameplay.");
        }
        failureMenu.Accept();
        if (failureScreen.SaveErrorVisible || failureMenu.CurrentPage != Page.TextSpeed)
            throw new InvalidOperationException("The file-select save error was not dismissible and retryable.");

        screen.QueueFree();
        copyScreen.QueueFree();
        failureScreen.QueueFree();
        GD.Print("Validated title/file-select 32-update white fades, slot wrapping, " +
            "PALH_05/PALH_06 erase palette switching, new-file naming, message speed, " +
            "copy, erase, and retryable save failures.");
    }

    private void ValidateDebugFlagMenu()
    {
        if (!InputMap.HasAction("debug_flags"))
            throw new InvalidOperationException("The F1 debug_flags input action was not registered.");

        _debugFlagMenu.OpenImmediatelyForValidation();
        if (!_debugFlagMenu.IsActive || !_debugFlagScreen.Visible ||
            !_gameplayPause.IsOwnedBy(_debugFlagMenu) ||
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
        if (_debugFlagScreen.Page != FlagPage.Room)
            throw new InvalidOperationException(
                "A physical Tab key event did not switch the flag editor to room flags.");
        _debugFlagScreen._Input(new InputEventKey
        {
            Keycode = Key.Tab,
            Pressed = true
        });
        if (_debugFlagScreen.Page != FlagPage.Inventory)
            throw new InvalidOperationException(
                "A logical Tab key event did not switch the flag editor to linked/items.");
        _debugFlagScreen._Input(new InputEventKey
        {
            Keycode = Key.Tab,
            Pressed = true
        });
        if (_debugFlagScreen.Page != FlagPage.Rings)
            throw new InvalidOperationException(
                "Tab did not switch the debug editor from items to appraised rings.");
        _debugFlagScreen._Input(new InputEventKey
        {
            Keycode = Key.Tab,
            Pressed = true
        });
        if (_debugFlagScreen.Page != FlagPage.Global)
            throw new InvalidOperationException(
                "Tab did not cycle the debug editor back to global flags.");

        _debugFlagScreen.SelectGlobalFlagForValidation(OracleSaveData.GlobalFlagIntroDone);
        if (!_debugFlagScreen.RenderedText.Contains("GLOBALFLAG_INTRO_DONE", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The imported name for GLOBALFLAG_INTRO_DONE was not displayed.");
        bool globalBefore = _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        _debugFlagScreen.ActivateSelection();
        if (_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) == globalBefore)
            throw new InvalidOperationException("The global flag editor did not toggle flag $0a.");
        _debugFlagScreen.ActivateSelection();

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
        _debugFlagScreen.ActivateSelection();
        if (_saveData.HasRoomFlag(5, room, mask) == roomBefore)
            throw new InvalidOperationException("The room flag editor did not toggle 5:aa bit 6.");
        _debugFlagScreen.MoveHorizontal(1);
        if (_debugFlagScreen.SelectedRoom != 0xab)
            throw new InvalidOperationException("Room flag browsing did not advance $aa -> $ab.");
        _debugFlagScreen.MoveHorizontal(-1);
        _debugFlagScreen.ActivateSelection();

        bool linkedBefore = _saveData.IsLinkedGame;
        _debugFlagScreen.SelectLinkedGameForValidation();
        if (!_debugFlagScreen.RenderedText.Contains("wIsLinkedGame $c612", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The linked/items page did not expose the source WRAM linked-game field.");
        _debugFlagScreen.ActivateSelection();
        if (_saveData.IsLinkedGame == linkedBefore)
            throw new InvalidOperationException("The debug editor did not toggle wIsLinkedGame.");
        _debugFlagScreen.ActivateSelection();

        var itemSave = OracleSaveData.CreateStandardGame();
        var itemTreasures = new TreasureDatabase();
        var itemInventory = new InventoryState(itemTreasures, itemSave, () => 0);
        var itemScreen = new DebugFlagScreen { Name = "DebugItemGrantValidation" };
        AddChild(itemScreen);
        itemScreen.Initialize(
            itemSave, new GlobalFlagDatabase(), itemTreasures, itemInventory);
        itemScreen.Open(0, 0);
        itemScreen.SelectTreasureForValidation("TREASURE_OBJECT_SWORD_01");
        if (!itemScreen.RenderedText.Contains("PARAM $02", StringComparison.Ordinal) ||
            !itemScreen.RenderedText.Contains("SWORD_01", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The item grant browser did not display the imported treasure variant and parameter.");
        }
        itemScreen.ActivateSelection();
        if (itemInventory.SwordLevel != 2 ||
            !itemInventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            !itemSave.HasTreasure(TreasureDatabase.TreasureSword))
        {
            throw new InvalidOperationException(
                "The debug item grant did not use the imported treasure transaction.");
        }
        itemScreen.SelectRingForValidation((int)RingId.Protection);
        if (!itemScreen.RenderedText.Contains("Protection Ring", StringComparison.Ordinal) ||
            itemInventory.HasAppraisedRing((int)RingId.Protection))
        {
            throw new InvalidOperationException(
                "The debug appraised-ring browser did not display the imported ring text.");
        }
        itemScreen.ActivateSelection();
        if (!itemInventory.HasAppraisedRing((int)RingId.Protection) ||
            (itemSave.ReadWramByte(0xc61d) & 0x80) == 0)
        {
            throw new InvalidOperationException(
                "The debug appraised-ring grant did not update wRingsObtained.");
        }
        itemScreen.QueueFree();

        _debugFlagMenu.CloseImmediatelyForValidation();
        if (_debugFlagMenu.IsActive || _debugFlagScreen.Visible ||
            !_player.IsPhysicsProcessing() || !_player.IsProcessing())
        {
            throw new InvalidOperationException(
                "Closing the debug flag menu did not restore Link processing.");
        }

        GD.Print("Validated F1 global/room flag editor, linked-game toggle, imported " +
            "treasure/appraised-ring grants, navigation, mutation, and gameplay freezing.");
    }

    private void ValidateDebugCollision()
    {
        bool hasF2Binding = InputMap.HasAction("debug_collision") &&
            InputMap.ActionGetEvents("debug_collision")
                .OfType<InputEventKey>()
                .Any(input => input.PhysicalKeycode == Key.F2);
        if (!hasF2Binding || _debugCollision.CollisionsDisabled)
        {
            throw new InvalidOperationException(
                "The disabled-by-default F2 debug_collision input action was not registered.");
        }

        LoadValidationRoom(4, 0x09);
        Vector2 wallApproach = new(20, 24);
        _player.WarpTo(wallApproach);
        _player.Face(Vector2I.Left);
        if (_playerWorld.ResolveMovement(
                wallApproach, Vector2.Left, allowWallSlide: true) != Vector2.Zero)
        {
            throw new InvalidOperationException(
                "Room 4:09's left wall was not available for debug collision validation.");
        }

        _player.UpdatePushingState(Vector2.Left);
        if (!_player.IsPushing)
            throw new InvalidOperationException("Link did not detect the wall before noclip was enabled.");

        _debugCollision.ToggleForValidation();
        _player.UpdatePushingState(Vector2.Left);
        UpdateRoomDebugLabel();
        if (!_debugCollision.CollisionsDisabled ||
            _playerWorld.ResolveMovement(
                wallApproach, Vector2.Left, allowWallSlide: true) != Vector2.Left ||
            _player.IsPushing || _roomDebug.Text != "4:09 NOCLIP")
        {
            throw new InvalidOperationException(
                "F2 noclip did not bypass Link's wall resolution and pushing state or expose its status.");
        }

        _debugCollision.ToggleForValidation();
        UpdateRoomDebugLabel();
        if (_debugCollision.CollisionsDisabled ||
            _playerWorld.ResolveMovement(
                wallApproach, Vector2.Left, allowWallSlide: true) != Vector2.Zero ||
            _roomDebug.Text != "4:09")
        {
            throw new InvalidOperationException(
                "Disabling F2 noclip did not restore ordinary Link collision and debug status.");
        }

        GD.Print("Validated F2 Link noclip toggle, wall bypass, push suppression, and status label.");
    }

    private void ValidateDebugRoomWarp()
    {
        string[] removedActions =
        [
            "debug_sign",
            "debug_animation",
            "debug_bush",
            "debug_house",
            "debug_chest",
            "debug_bracelet_chest"
        ];
        if (removedActions.Any(action => InputMap.HasAction(action)))
        {
            throw new InvalidOperationException(
                "A fixed test-room input action remained registered.");
        }

        InputEventKey[] bindings = InputMap.HasAction("debug_room_warp")
            ? InputMap.ActionGetEvents("debug_room_warp").OfType<InputEventKey>().ToArray()
            : [];
        if (bindings.Length != 1 || bindings[0].PhysicalKeycode != Key.V)
        {
            throw new InvalidOperationException(
                "The configurable debug room warp was not bound exclusively to physical V.");
        }
        if (_debugWarps.TargetGroup != 4 || _debugWarps.TargetRoom != 0x11)
        {
            throw new InvalidOperationException(
                $"Expected the default debug target 4:11, got " +
                $"{_debugWarps.TargetGroup:x1}:{_debugWarps.TargetRoom:x2}.");
        }

        LoadValidationRoom(0, 0x11);
        _debugWarps.WarpToTarget();
        if (_activeGroup != 4 || _currentRoom.Id != 0x11 ||
            _player.Position != FindSpawn() ||
            _player.FacingVector != Vector2I.Down)
        {
            throw new InvalidOperationException(
                "The configurable debug warp did not load and place Link in 4:11.");
        }

        GD.Print("Validated the sole fixed-key room shortcut: V to configurable " +
            "debug target 4:11.");
    }

    private void ValidateMapScreen()
    {
        if (!Mathf.IsEqualApprox(MapMenuController.FastFadeFrames, 11.0f))
            throw new InvalidOperationException("The map menu must use the 11-update fast palette fade.");
        if (MapScreen.GetSpriteShadeForValidation(Colors.Black, true) != 0 ||
            MapScreen.GetSpriteShadeForValidation(Color.Color8(85, 85, 85), true) != 1 ||
            MapScreen.GetSpriteShadeForValidation(Color.Color8(170, 170, 170), true) != 2 ||
            MapScreen.GetSpriteShadeForValidation(Colors.White, true) != 3 ||
            MapScreen.GetSpriteShadeForValidation(Colors.White, false) != 0 ||
            MapScreen.GetSpriteShadeForValidation(Colors.Black, false) != 3)
        {
            throw new InvalidOperationException(
                "Map OAM grayscale no longer handles inverted spr_ and normal gfx sheets separately.");
        }
        if (MapScreen.UsesInvertedSpriteGrayscale(MapMode.Present, 0x0e) ||
            MapScreen.UsesInvertedSpriteGrayscale(MapMode.Present, 0x22) ||
            MapScreen.UsesInvertedSpriteGrayscale(MapMode.Dungeon, 0x88) ||
            !MapScreen.UsesInvertedSpriteGrayscale(MapMode.Dungeon, 0x00))
        {
            throw new InvalidOperationException(
                "Map OAM sheets no longer honor spr_minimap_icons invert:false separately " +
                "from the default-inverted dungeon item sheet.");
        }
        if ((MapScreen.LocationArrowAttributes & 0x40) == 0)
            throw new InvalidOperationException("The map location arrow lost OAM Y-flip attribute $47.");

        LoadValidationRoom(0, 0x11);
        int openMenuRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu);
        _mapMenu.BeginOpeningForValidation();
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != openMenuRequests)
            throw new InvalidOperationException("MENU_MAP played SND_OPENMENU $54 before its opening fade.");
        for (int frame = 0; frame < MapMenuController.FastFadeFrames - 1; frame++)
        {
            _mapMenu.Update(1.0 / 60.0);
            if (_mapScreen.Visible ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != openMenuRequests)
            {
                throw new InvalidOperationException(
                    "MENU_MAP appeared or played SND_OPENMENU $54 before full white.");
            }
        }
        _mapMenu.Update(1.0 / 60.0);
        if (!_mapScreen.Visible || _sound.LastPlayRequest != OracleSoundEngine.SndOpenMenu ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != openMenuRequests + 1)
        {
            throw new InvalidOperationException(
                "MENU_MAP did not request SND_OPENMENU $54 at the full-white screen swap.");
        }
        for (int frame = 0; frame < MapMenuController.FastFadeFrames; frame++)
            _mapMenu.Update(1.0 / 60.0);
        if (_mapScreen.Mode != MapMode.Present || _mapScreen.CursorRoom != 0x11)
            throw new InvalidOperationException(
                $"Present map should open at room 11, got {_mapScreen.Mode} / {_mapScreen.CursorRoom:x2}.");
        if (!_gameplayPause.IsOwnedBy(_mapMenu) ||
            _player.IsPhysicsProcessing() || _player.IsProcessing())
            throw new InvalidOperationException("Link continued updating while the map menu was open.");

        int mapMoveRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        if (!_mapMenu.NavigateForValidation(Vector2I.Left) ||
            !_mapMenu.NavigateForValidation(Vector2I.Left) ||
            _mapScreen.CursorRoom != 0x1d ||
            _sound.LastPlayRequest != OracleSoundEngine.SndMenuMove ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != mapMoveRequests + 2)
        {
            throw new InvalidOperationException(
                $"The overworld cursor did not wrap 11 -> 10 -> 1d with two " +
                $"SND_MENU_MOVE $84 requests; got {_mapScreen.CursorRoom:x2}.");
        }

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

        LoadValidationRoom(0, 0x45);
        _mapMenu.OpenImmediatelyForValidation();
        if (!_mapScreen.TryGetSelectedAreaText(out MapText areaText) ||
            areaText.TextId != 0x0343 || string.IsNullOrWhiteSpace(areaText.Message))
        {
            throw new InvalidOperationException(
                "Present room 45 did not resolve its imported TX_0343 map text.");
        }
        _mapScreen.Update(7.0 / 60.0);
        if (_mapScreen.PopupPrimary != 0x01 || _mapScreen.PopupSize != 4)
        {
            throw new InvalidOperationException(
                $"Room 45's house popup did not expand in 7 updates: " +
                $"icon={_mapScreen.PopupPrimary:x2}, size={_mapScreen.PopupSize}.");
        }
        _mapMenu.CloseImmediatelyForValidation();

        LoadValidationRoom(1, 0x11);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapMode.Past || _mapScreen.CursorRoom != 0x11)
            throw new InvalidOperationException(
                $"Past map should open at room 11, got {_mapScreen.Mode} / {_mapScreen.CursorRoom:x2}.");
        _mapMenu.CloseImmediatelyForValidation();

        DungeonInfo dungeon = _rooms.DungeonMaps.GetDungeon(0x0d);
        if (!dungeon.TryGetRoom(0x09, out DungeonCell cell) ||
            cell.Floor != 0 || cell.X != 2 || cell.Y != 2)
        {
            throw new InvalidOperationException(
                "Dungeon 0d room 09 was not imported at floor 0, cell (2,2).");
        }
        LoadValidationRoom(4, 0x09);
        _mapMenu.OpenImmediatelyForValidation();
        if (_mapScreen.Mode != MapMode.Dungeon ||
            _mapScreen.DisplayedDungeonFloor != 0 ||
            _mapScreen.DungeonLinkIconPosition != new Vector2(96, 48))
        {
            throw new InvalidOperationException(
                "Dungeon 0d room 09 did not open on floor 0 with Link's 8x16 " +
                $"map icon at source position (96,48); got " +
                $"{_mapScreen.DungeonLinkIconPosition}.");
        }
        _mapMenu.CloseImmediatelyForValidation();

        byte oldCompasses = _saveData.ReadWramByte(0xc684);
        byte oldMaps = _saveData.ReadWramByte(0xc686);
        byte oldRoom30Flags = _saveData.GetRoomFlags(4, 0x30);
        _saveData.WriteWramByte(0xc684, (byte)(oldCompasses | 0x04));
        _saveData.WriteWramByte(0xc686, (byte)(oldMaps | 0x04));
        _saveData.SetRoomFlag(4, 0x30, OracleSaveData.RoomFlagItem, value: false);
        var mapInventory = new InventoryState(_treasures, _saveData);
        _mapScreen.Initialize(_rooms, mapInventory);
        LoadValidationRoom(4, 0x46);
        _mapMenu.OpenImmediatelyForValidation();
        DungeonInfo dungeon02 = _rooms.DungeonMaps.GetDungeon(0x02);
        if (dungeon02.CompassFloors != 0x01 ||
            _mapScreen.DisplayedDungeonFloor != 1 || _mapScreen.DungeonTileAt(6, 1) != 0x83)
        {
            throw new InvalidOperationException(
                "Dungeon 02's map/compass did not reveal its imported boss room and floor mask.");
        }
        mapMoveRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        if (!_mapMenu.NavigateForValidation(Vector2I.Down) ||
            _mapScreen.DisplayedDungeonFloor != 0 || _mapScreen.DungeonTileAt(5, 1) != 0xae ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != mapMoveRequests + 1)
        {
            throw new InvalidOperationException(
                "Dungeon 02's floor navigation did not reveal room 30 on floor 0 " +
                "and request SND_MENU_MOVE $84.");
        }
        if (_mapMenu.NavigateForValidation(Vector2I.Down) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != mapMoveRequests + 1)
        {
            throw new InvalidOperationException(
                "Blocked dungeon-floor navigation played SND_MENU_MOVE $84.");
        }
        _mapMenu.CloseImmediatelyForValidation();
        _saveData.WriteWramByte(0xc684, oldCompasses);
        _saveData.WriteWramByte(0xc686, oldMaps);
        _saveData.SetRoomFlag(4, 0x30, 0xff, value: false);
        if (oldRoom30Flags != 0)
            _saveData.SetRoomFlag(4, 0x30, oldRoom30Flags);
        _mapScreen.Initialize(_rooms, _inventory);
        LoadValidationRoom(4, 0x09);

        _mapMenu.OpenDebugImmediatelyForValidation();
        if (!_mapScreen.DebugFastTravel || _mapScreen.Mode != MapMode.Past)
            throw new InvalidOperationException(
                "Debug fast travel did not retain the most recent past-overworld map from dungeon 0d.");
        _mapScreen.CycleDebugPage();
        bool hasTarget = _mapScreen.TryGetFastTravelTarget(out int group, out int room);
        if (_mapScreen.Mode != MapMode.Interior || _mapScreen.InteriorGroup != 2 ||
            !hasTarget || group != 2 || room != 0x11)
        {
            throw new InvalidOperationException(
                $"Expected first interior fast-travel page at 2:11, got " +
                $"{_mapScreen.Mode} / {group}:{room:x2}.");
        }
        _mapScreen.CycleDebugPage();
        if (_mapScreen.InteriorGroup != 3 ||
            !_mapScreen.TryGetFastTravelTarget(out group, out room) || group != 3 || room != 0x11)
        {
            throw new InvalidOperationException(
                $"Expected second interior fast-travel page at 3:11, got {group}:{room:x2}.");
        }
        _mapScreen.CycleDebugPage();
        if (_mapScreen.InteriorGroup != 4 || _mapScreen.CursorRoom != 0x09)
            throw new InvalidOperationException("Group 4 interior page did not select active room 4:09.");
        mapMoveRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        for (int step = 0; step < 10; step++)
        {
            if (!_mapMenu.NavigateForValidation(Vector2I.Left))
                throw new InvalidOperationException("The interior room cursor rejected a left move.");
        }
        if (_mapScreen.CursorRoom != 0x0f ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != mapMoveRequests + 10)
        {
            throw new InvalidOperationException(
                $"The 16x16 interior cursor did not wrap 09 left to 0f with ten move sounds; " +
                $"got {_mapScreen.CursorRoom:x2}.");
        }
        _mapScreen.CycleDebugPage();
        if (_mapScreen.InteriorGroup != 5 ||
            !_mapScreen.TryGetFastTravelTarget(out group, out room) || group != 5 || room != 0x11)
        {
            throw new InvalidOperationException(
                $"Expected fourth interior fast-travel page at 5:11, got {group}:{room:x2}.");
        }
        _mapScreen.CycleDebugPage();
        if (_mapScreen.Mode != MapMode.Present ||
            !_mapScreen.TryGetFastTravelTarget(out group, out room) || group != 0 || room != 0x11)
        {
            throw new InvalidOperationException(
                $"Interior group 5 did not cycle back to present room 0:11; got {group}:{room:x2}.");
        }
        _mapScreen.CycleDebugPage();
        _mapScreen.CycleDebugPage();
        _mapScreen.CycleDebugPage();
        _mapScreen.CycleDebugPage();
        if (_mapScreen.Mode != MapMode.Interior || _mapScreen.InteriorGroup != 4 ||
            _mapScreen.CursorRoom != 0x0f ||
            !_mapScreen.TryGetFastTravelTarget(out group, out room) || group != 4 || room != 0x0f)
        {
            throw new InvalidOperationException(
                $"Group 4 did not retain interior selection 4:0f; got {group}:{room:x2}.");
        }
        if (!_mapMenu.BeginTravelToSelectionForValidation())
            throw new InvalidOperationException("Debug map rejected the valid interior target 4:0f.");
        for (int frame = 0; frame < MapMenuController.FastFadeFrames - 1; frame++)
        {
            _mapMenu.Update(1.0 / 60.0);
            if (_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0x09)
                throw new InvalidOperationException("Debug fast travel loaded before the fade reached white.");
        }
        _mapMenu.Update(1.0 / 60.0);
        if (_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0x0f)
        {
            throw new InvalidOperationException(
                "Debug map fast travel did not load interior room 4:0f at full white.");
        }
        for (int frame = 0; frame < MapMenuController.FastFadeFrames; frame++)
            _mapMenu.Update(1.0 / 60.0);
        if (_mapMenu.IsActive || !_player.IsPhysicsProcessing() || !_player.IsProcessing())
            throw new InvalidOperationException("Debug fast travel did not restore gameplay processing.");

        GD.Print("Validated original present/past/dungeon map tilemaps, imported TX_03XX area " +
            "text, source-specific color-0 OAM transparency, arrow Y-flip, 7-update popup " +
            "expansion, dungeon Link-icon OAM bias, map/compass floor and " +
            "boss/treasure reveals, SND_OPENMENU/SND_MENU_MOVE boundaries, 14x14 cursor " +
            "wrapping, 32-update marker blink, 11-update " +
            "fast fades, Link input freezing, 16x16 group 2-5 interior browsing, per-page " +
            "selection retention, and fade-safe interior debug fast travel.");
    }

    private void ValidateInventoryFoundation()
    {
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            _inventory.EquippedB != InventoryState.ItemSword ||
            _inventory.SwordLevel != 1)
        {
            throw new InvalidOperationException(
                "Impa's TREASURE_OBJECT_SWORD_00 gift was not added to the first empty " +
                "inventory slot, wInventoryB.");
        }

        // The menu swap checks below begin from their established sword-on-A
        // arrangement; move Impa's B-slot gift there through the normal path.
        _inventory.EquipA(InventoryState.ItemSword);

        var chests = new ChestDatabase();
        if (!chests.TryGet(4, 0x87, 0x65, out ChestRecord switchHookChest) ||
            switchHookChest.TreasureObject != "TREASURE_OBJECT_SWITCH_HOOK_00" ||
            switchHookChest.TreasureId != TreasureDatabase.TreasureSwitchHook ||
            switchHookChest.Parameter != 1)
        {
            throw new InvalidOperationException(
                "The original 4:87/$65 Switch Hook chest did not resolve to TREASURE_SWITCH_HOOK parameter 1.");
        }

        var tempInventory = new InventoryState(_treasures);
        tempInventory.GiveTreasure(new TreasureObjectRecord(
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
            "Impa's TREASURE_OBJECT_SWORD_00 gift, and non-rupee chest treasure give path.");
    }

    private void ValidateInventoryMenu()
    {
        ValidateItemIconShadeMapping();
        if (!Mathf.IsEqualApprox(InventoryMenuController.FastFadeFrames, 11.0f))
            throw new InvalidOperationException("The inventory menu must use the 11-update fast palette fade.");

        int openMenuRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu);
        _inventoryMenu.BeginOpeningForValidation();
        if (!_gameplayPause.IsOwnedBy(_inventoryMenu) ||
            _mapMenu.CanOpenNormalForValidation ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != openMenuRequests)
        {
            throw new InvalidOperationException(
                "Inventory opening did not exclusively own the shared menu pause/load state.");
        }
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames - 1; frame++)
        {
            _inventoryMenu.Update(1.0 / 60.0);
            if (_inventoryScreen.Visible ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != openMenuRequests)
            {
                throw new InvalidOperationException(
                    "The inventory screen appeared or played SND_OPENMENU $54 before full white.");
            }
        }
        if (_scene.MenuFade.Color.A <= 0.0f || _scene.MenuFade.Color.A >= 1.0f)
            throw new InvalidOperationException("The inventory opening fade did not remain partial for 10 updates.");
        _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryScreen.Visible || !Mathf.IsEqualApprox(_scene.MenuFade.Color.A, 1.0f) ||
            _sound.LastPlayRequest != OracleSoundEngine.SndOpenMenu ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != openMenuRequests + 1)
        {
            throw new InvalidOperationException(
                "The inventory screen did not swap in with SND_OPENMENU $54 at full white.");
        }
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryMenu.IsActive || !_inventoryScreen.Visible ||
            !_inventoryMenu.IsOpen || !Mathf.IsZeroApprox(_scene.MenuFade.Color.A) ||
            _player.IsPhysicsProcessing() || _player.IsProcessing())
        {
            throw new InvalidOperationException(
                "The inventory opening fade did not finish with the menu open and gameplay frozen.");
        }

        if (_inventoryScreen.Cursor != 0 || _inventory.StorageItemAt(0) != InventoryState.ItemNone ||
            _inventory.EquippedA != InventoryState.ItemSword ||
            _inventoryScreen.ActiveTextKey != 0 ||
            _inventoryScreen.VisibleTextForValidation != new string(' ', 16))
        {
            throw new InvalidOperationException("Inventory menu validation expected sword on A and slot 0 empty.");
        }

        InventoryTextRecord woodenSwordText = _treasures.GetInventoryText(0x23);
        InventoryTextRecord friendshipRingText = _treasures.GetRingText(0x00);
        var swordLevelOverlay = _inventoryScreen.LevelOverlayForValidation(
            InventoryState.ItemSword);
        var hudSwordLevelOverlay = _hud.LevelOverlayForValidation(
            InventoryState.ItemSword, isA: true);
        if (woodenSwordText.NameTextId != 0x0923 ||
            woodenSwordText.Message != "Wooden Sword\nA hero's blade." ||
            friendshipRingText.NameTextId != 0x3040 ||
            friendshipRingText.DescriptionTextId != 0x3080 ||
            friendshipRingText.Message != "Friendship Ring\nSymbol of a\nmeeting" ||
            swordLevelOverlay is not { } levelOverlay ||
            levelOverlay.SymbolTile != 0x1a || levelOverlay.DigitTile != 0x11 ||
            levelOverlay.Attributes != 0x07 || levelOverlay.Offset != new Vector2(8, 8) ||
            !_inventoryScreen.EquippedLevelSymbolBackgroundColorForValidation.IsEqualApprox(
                new Color(0x1f / 31.0f, 0x1a / 31.0f, 0x11 / 31.0f)) ||
            hudSwordLevelOverlay is not { } hudLevelOverlay ||
            hudLevelOverlay.SymbolTile != 0x1a || hudLevelOverlay.DigitTile != 0x11 ||
            hudLevelOverlay.Position != new Vector2(56, 8))
        {
            throw new InvalidOperationException(
                "Imported inventory text or the level-1 sword's L-/digit tiles regressed.");
        }

        var heartPieceDisplay = _inventoryScreen.HeartPieceDisplayForValidation;
        if (heartPieceDisplay.NormalAttributes != 0x07 ||
            heartPieceDisplay.FlippedAttributes != 0x27 ||
            !heartPieceDisplay.Shade2.IsEqualApprox(
                new Color(0x1b / 31.0f, 0x00, 0x00)) ||
            !heartPieceDisplay.Shade3.IsEqualApprox(Colors.Black))
        {
            throw new InvalidOperationException(
                "bank2.s:itemSubmenu2HeartPieceDisplayData did not map source " +
                "attributes $05/$25 to BG attributes $07/$27 and the original red/black palette.");
        }

        int selectItemRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem);
        int inventoryMoveRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        if (!_inventoryMenu.EquipToAForValidation() ||
            _sound.LastPlayRequest != OracleSoundEngine.SndSelectItem ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) != selectItemRequests + 1 ||
            _inventory.EquippedA != InventoryState.ItemNone ||
            _inventory.StorageItemAt(0) != InventoryState.ItemSword ||
            _inventoryScreen.ActiveTextKey != 0x23 ||
            _inventoryScreen.VisibleTextForValidation != "  Wooden Sword  ")
        {
            throw new InvalidOperationException(
                "Pressing A on empty storage slot 0 did not unequip the sword or center TX_0923.");
        }
        for (int update = 0; update < 40; update++)
            _inventoryScreen.UpdateInventoryText(1.0 / 60.0);
        if (_inventoryScreen.VisibleTextForValidation != "  Wooden Sword  ")
        {
            throw new InvalidOperationException(
                "Inventory text scrolled before its original 40-update name pause completed.");
        }
        _inventoryScreen.UpdateInventoryText(1.0 / 60.0);
        if (_inventoryScreen.VisibleTextForValidation != " Wooden Sword  A")
        {
            throw new InvalidOperationException(
                "Inventory text did not begin TX_0923's description after the original pause.");
        }
        for (int update = 0; update < 7; update++)
            _inventoryScreen.UpdateInventoryText(1.0 / 60.0);
        if (_inventoryScreen.VisibleTextForValidation != " Wooden Sword  A")
        {
            throw new InvalidOperationException(
                "Inventory text ignored the original eight-update character interval.");
        }
        _inventoryScreen.UpdateInventoryText(1.0 / 60.0);
        if (_inventoryScreen.VisibleTextForValidation != "Wooden Sword  A ")
        {
            throw new InvalidOperationException(
                "Inventory text did not advance on the eighth marquee update.");
        }

        if (!_inventoryMenu.EquipToBForValidation() ||
            _sound.LastPlayRequest != OracleSoundEngine.SndSelectItem ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) != selectItemRequests + 2 ||
            _inventory.EquippedB != InventoryState.ItemSword ||
            _inventory.StorageItemAt(0) != InventoryState.ItemNone)
        {
            throw new InvalidOperationException(
                "Pressing B on storage slot 0 did not equip the sword to wInventoryB.");
        }

        if (!_inventoryMenu.MoveCursorForValidation(Vector2I.Left) ||
            _sound.LastPlayRequest != OracleSoundEngine.SndMenuMove ||
            _inventoryScreen.Cursor != 15)
            throw new InvalidOperationException("Inventory cursor did not wrap left with the original & $0f rule.");
        if (!_inventoryMenu.MoveCursorForValidation(Vector2I.Right) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != inventoryMoveRequests + 2 ||
            _inventoryScreen.Cursor != 0)
            throw new InvalidOperationException("Inventory cursor did not return to slot 0 after wrapping.");

        if (!_inventoryMenu.EquipToBForValidation() ||
            !_inventoryMenu.EquipToAForValidation() ||
            _sound.LastPlayRequest != OracleSoundEngine.SndSelectItem ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) != selectItemRequests + 4 ||
            _inventory.EquippedA != InventoryState.ItemSword ||
            _inventory.EquippedB != InventoryState.ItemNone ||
            _inventory.StorageItemAt(0) != InventoryState.ItemNone)
        {
            throw new InvalidOperationException("Inventory menu did not restore the sword to A through storage swaps.");
        }

        int tabSoundRequests = _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu);
        if (!_inventoryMenu.BeginNextSubscreenForValidation() ||
            _sound.LastPlayRequest != OracleSoundEngine.SndOpenMenu ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != tabSoundRequests + 1)
        {
            throw new InvalidOperationException(
                "The first inventory tab switch did not request SND_OPENMENU $54.");
        }
        if (_inventoryMenu.BeginNextSubscreenForValidation() ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != tabSoundRequests + 1)
        {
            throw new InvalidOperationException(
                "An in-progress inventory tab scroll replayed SND_OPENMENU $54.");
        }
        for (int frame = 0; frame < InventoryScreen.PageScrollUpdates - 1; frame++)
            _inventoryScreen.UpdatePageTransition(1.0 / 60.0);
        if (!_inventoryScreen.PageTransitionActive ||
            _inventoryScreen.Subscreen != InventorySubscreen.Items)
        {
            throw new InvalidOperationException(
                "The secondary inventory page arrived before the original 13-update scroll finished.");
        }
        _inventoryScreen.UpdatePageTransition(1.0 / 60.0);
        if (_inventoryScreen.PageTransitionActive ||
            _inventoryScreen.Subscreen != InventorySubscreen.SecondaryItems)
        {
            throw new InvalidOperationException("The secondary inventory page did not finish after 13 updates.");
        }
        if (!_inventoryMenu.MoveCursorForValidation(Vector2I.Left) ||
            _inventoryScreen.ActiveCursor != 14)
            throw new InvalidOperationException("The empty secondary page did not wrap 0 -> 14 to the left.");
        if (!_inventoryMenu.MoveCursorForValidation(Vector2I.Right) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != inventoryMoveRequests + 4)
        {
            throw new InvalidOperationException(
                "Secondary inventory cursor movement did not request SND_MENU_MOVE $84.");
        }

        OracleSaveData ringSave = OracleSaveData.CreateStandardGame();
        ringSave.WriteWramByte(0xc6cc, 2);
        ringSave.WriteWramByte(0xc6c6, 7);
        ringSave.WriteWramByte(0xc6c7, 8);
        ringSave.WriteWramByte(0xc6c8, 9);
        var ringInventory = new InventoryState(_treasures, ringSave);
        if (ringInventory.RingBoxCapacity != 3 || ringInventory.RingAt(2) != 9 ||
            !ringInventory.EquipRingAt(1) || ringInventory.ActiveRing != 8 ||
            !OracleSaveData.TryDeserialize(ringSave.Serialize(), out OracleSaveData? restoredRingSave) ||
            new InventoryState(_treasures, restoredRingSave!).ActiveRing != 8)
        {
            throw new InvalidOperationException(
                "Ring-box capacity, contents, active-ring toggle, or save-image persistence regressed.");
        }

        if (!_inventoryMenu.BeginNextSubscreenForValidation() ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenMenu) != tabSoundRequests + 2)
        {
            throw new InvalidOperationException(
                "The second inventory tab switch did not request SND_OPENMENU $54.");
        }
        for (int frame = 0; frame < InventoryScreen.PageScrollUpdates; frame++)
            _inventoryScreen.UpdatePageTransition(1.0 / 60.0);
        if (_inventoryScreen.Subscreen != InventorySubscreen.EssencesAndSave ||
            _inventory.Essences != 0 || _inventoryScreen.ActiveTextKey != 0 ||
            _inventoryScreen.VisibleTextForValidation != new string(' ', 16))
        {
            throw new InvalidOperationException(
                "The essence/save page displayed text for an unobtained essence.");
        }
        if (!_inventoryMenu.MoveCursorForValidation(Vector2I.Right) ||
            !_inventoryMenu.MoveCursorForValidation(Vector2I.Down) ||
            !_inventoryMenu.MoveCursorForValidation(Vector2I.Down) ||
            _sound.LastPlayRequest != OracleSoundEngine.SndMenuMove ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) != inventoryMoveRequests + 7 ||
            !_inventoryScreen.SaveAndQuitSelected || _inventoryScreen.ActiveCursor != 0x82 ||
            _inventoryScreen.ActiveTextKey != 0x60 ||
            _inventoryScreen.VisibleTextForValidation != "  Save Screen   ")
            throw new InvalidOperationException("The page-3 cursor did not reach the original Save & Quit entry.");

        _inventoryMenu.OpenSaveMenuFromInventoryForValidation();
        if (!_saveQuitScreen.Visible || _inventoryScreen.Visible ||
            !Mathf.IsEqualApprox(_scene.MenuFade.Color.A, 1.0f))
        {
            throw new InvalidOperationException(
                "Selecting Save & Quit did not swap screens immediately at full white.");
        }
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryMenu.SaveMenuOpen || !_saveQuitScreen.Visible ||
            !Mathf.IsZeroApprox(_scene.MenuFade.Color.A) || _saveQuitScreen.Cursor != 0)
        {
            throw new InvalidOperationException(
                "The three-option save menu did not finish its 11-update fade-in on Continue.");
        }
        _saveQuitScreen.Move(1);
        int saveRequests = _inventoryMenu.SaveRequests;
        int saveWrites = _saveWriteRequests;
        _inventoryMenu.SelectSaveOptionForValidation();
        if (_inventoryMenu.SaveRequests != saveRequests + 1 ||
            _saveWriteRequests != saveWrites + 1 ||
            _saveQuitScreen.DelayCounter != InventoryMenuController.SaveSelectionDelayFrames)
        {
            throw new InvalidOperationException("Save and Continue did not save immediately and start its 30-update delay.");
        }
        for (int frame = 0; frame < InventoryMenuController.SaveSelectionDelayFrames - 1; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_saveQuitScreen.Visible || !_inventoryMenu.SaveMenuOpen)
            throw new InvalidOperationException("Save and Continue exited before its original 30-update delay.");
        _inventoryMenu.Update(1.0 / 60.0);

        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames - 1; frame++)
        {
            _inventoryMenu.Update(1.0 / 60.0);
            if (!_saveQuitScreen.Visible)
                throw new InvalidOperationException("The save menu disappeared before the fade reached white.");
        }
        _inventoryMenu.Update(1.0 / 60.0);
        if (_saveQuitScreen.Visible || !Mathf.IsEqualApprox(_scene.MenuFade.Color.A, 1.0f))
            throw new InvalidOperationException("The save menu was not removed at full white.");
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (_inventoryMenu.IsActive || _inventoryScreen.Visible || _saveQuitScreen.Visible ||
            !Mathf.IsZeroApprox(_scene.MenuFade.Color.A) ||
            !_player.IsPhysicsProcessing() || !_player.IsProcessing())
        {
            throw new InvalidOperationException(
                "The inventory closing fade did not restore gameplay processing.");
        }

        _inventoryMenu.BeginSaveOpeningForValidation();
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames - 1; frame++)
        {
            _inventoryMenu.Update(1.0 / 60.0);
            if (_saveQuitScreen.Visible)
                throw new InvalidOperationException("Start+Select save menu appeared before the fade reached white.");
        }
        _inventoryMenu.Update(1.0 / 60.0);
        for (int frame = 0; frame < InventoryMenuController.FastFadeFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (!_inventoryMenu.SaveMenuOpen || !_saveQuitScreen.Visible)
            throw new InvalidOperationException("Start+Select did not open the save menu through the shared fast fade.");
        _inventoryMenu.CloseImmediatelyForValidation();

        _saveQuitScreen.Open();
        ulong standardSaveBackground = _saveQuitScreen.BackgroundPixelHash;
        if (!_saveQuitScreen.BackgroundIsOpaque)
        {
            throw new InvalidOperationException(
                "The ordinary save-menu background exposed transparent VRAM cells.");
        }
        _saveQuitScreen.Close();
        int gameOverSaveWrites = 0;
        int gameOverContinues = 0;
        int gameOverQuits = 0;
        int gameOverMoveRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove);
        int gameOverSelectRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem);
        var gameOverMenu = new InventoryMenuController(
            _inventoryScreen,
            _saveQuitScreen,
            _menuLifecycle,
            () => true,
            () => true,
            () =>
            {
                gameOverSaveWrites++;
                return SaveResult.Succeeded;
            },
            () => gameOverQuits++,
            _sound.PlaySound,
            () => gameOverContinues++);

        gameOverMenu.BeginGameOverForValidation();
        if (!gameOverMenu.GameOver ||
            !_saveQuitScreen.Visible ||
            !_saveQuitScreen.IsGameOver ||
            !_saveQuitScreen.BackgroundIsOpaque ||
            _saveQuitScreen.BackgroundPixelHash == standardSaveBackground ||
            _saveQuitScreen.Cursor != 0 ||
            !Mathf.IsEqualApprox(_scene.MenuFade.Color.A, 1.0f) ||
            !_gameplayPause.IsOwnedBy(gameOverMenu) ||
            gameOverMenu.CancelSaveMenuForValidation())
        {
            throw new InvalidOperationException(
                "Forced game over did not open gfx_gameover with PALH_06 at " +
                "white, retain the save-option graphics without transparent " +
                "cells, select Continue, freeze gameplay, and block B.");
        }
        for (int update = 0;
            update < InventoryMenuController.FastFadeFrames - 1;
            update++)
        {
            gameOverMenu.Update(1.0 / 60.0);
        }
        if (gameOverMenu.IsOpen ||
            Mathf.IsZeroApprox(_scene.MenuFade.Color.A))
        {
            throw new InvalidOperationException(
                "Forced game over finished its fade-in before update 11.");
        }
        gameOverMenu.Update(1.0 / 60.0);
        if (!gameOverMenu.IsOpen ||
            !Mathf.IsZeroApprox(_scene.MenuFade.Color.A))
        {
            throw new InvalidOperationException(
                "Forced game over did not finish its fade-in on update 11.");
        }

        gameOverMenu.SelectSaveOptionForValidation();
        if (gameOverSaveWrites != 0 ||
            gameOverMenu.SaveRequests != 0 ||
            _saveQuitScreen.DelayCounter !=
                InventoryMenuController.SaveSelectionDelayFrames ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) !=
                gameOverSelectRequests + 1)
        {
            throw new InvalidOperationException(
                "Game-over Continue saved the file or missed SND_SELECTITEM " +
                "and its 30-update delay.");
        }
        for (int update = 0;
            update < InventoryMenuController.SaveSelectionDelayFrames - 1;
            update++)
        {
            gameOverMenu.Update(1.0 / 60.0);
        }
        if (!gameOverMenu.IsActive || gameOverContinues != 0)
            throw new InvalidOperationException(
                "Game-over Continue resumed before its 30-update delay.");
        gameOverMenu.Update(1.0 / 60.0);
        if (gameOverMenu.IsActive ||
            gameOverContinues != 1 ||
            gameOverQuits != 0 ||
            _saveQuitScreen.Visible ||
            !_player.IsPhysicsProcessing() ||
            !_player.IsProcessing())
        {
            throw new InvalidOperationException(
                "Game-over Continue did not close immediately and resume at " +
                "the death checkpoint after 30 updates.");
        }

        gameOverMenu.BeginGameOverForValidation();
        for (int update = 0;
            update < InventoryMenuController.FastFadeFrames;
            update++)
        {
            gameOverMenu.Update(1.0 / 60.0);
        }
        if (!gameOverMenu.MoveSaveCursorForValidation(1) ||
            _saveQuitScreen.Cursor != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) !=
                gameOverMoveRequests + 1)
        {
            throw new InvalidOperationException(
                "Game-over Save and Continue cursor movement missed SND_MENU_MOVE.");
        }
        gameOverMenu.SelectSaveOptionForValidation();
        for (int update = 0;
            update < InventoryMenuController.SaveSelectionDelayFrames;
            update++)
        {
            gameOverMenu.Update(1.0 / 60.0);
        }
        if (gameOverSaveWrites != 1 ||
            gameOverMenu.SaveRequests != 1 ||
            gameOverContinues != 2 ||
            gameOverQuits != 0 ||
            gameOverMenu.IsActive)
        {
            throw new InvalidOperationException(
                "Game-over Save and Continue did not save once and then " +
                "resume after 30 updates.");
        }

        gameOverMenu.BeginGameOverForValidation();
        for (int update = 0;
            update < InventoryMenuController.FastFadeFrames;
            update++)
        {
            gameOverMenu.Update(1.0 / 60.0);
        }
        if (!gameOverMenu.MoveSaveCursorForValidation(1) ||
            !gameOverMenu.MoveSaveCursorForValidation(1) ||
            _saveQuitScreen.Cursor != 2 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMenuMove) !=
                gameOverMoveRequests + 3)
        {
            throw new InvalidOperationException(
                "Game-over Save and Quit cursor movement missed its two " +
                "SND_MENU_MOVE requests.");
        }
        gameOverMenu.SelectSaveOptionForValidation();
        for (int update = 0;
            update < InventoryMenuController.SaveSelectionDelayFrames;
            update++)
        {
            gameOverMenu.Update(1.0 / 60.0);
        }
        if (gameOverSaveWrites != 2 ||
            gameOverMenu.SaveRequests != 2 ||
            gameOverMenu.QuitRequests != 1 ||
            gameOverContinues != 2 ||
            gameOverQuits != 1 ||
            gameOverMenu.IsActive ||
            _saveQuitScreen.BackgroundPixelHash != standardSaveBackground ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSelectItem) !=
                gameOverSelectRequests + 3)
        {
            throw new InvalidOperationException(
                "Game-over Save and Quit did not save, delay, close, restore " +
                "the ordinary background, and request title exactly once.");
        }

        GD.Print("Validated inventory SND_OPENMENU boundaries, 11-update fast white fades, " +
            "13-update three-page scrolling, SND_SELECTITEM equip/unequip and " +
            "SND_MENU_MOVE cursor boundaries, TX_09XX/ring marquee text and 40/8-update counters, " +
            "mode-$00 item level tiles, " +
            "secondary cursor/ring persistence, essence/save navigation, Start+Select, three save choices, " +
            "forced gfx_gameover/PALH_06 presentation and blocked B, all three " +
            "game-over choices, 30-update selection delay, A/B storage swaps, " +
            "and gameplay freezing.");
    }

    private void ValidateItemIconShadeMapping()
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

        ulong storedSheetHash = OracleGraphicsCache.PixelHash(
            OracleGraphicsCache.LoadImage(
                "res://assets/oracle/gfx/spr_item_icons_1_spr.png"));
        ulong equippedSheetHash = OracleGraphicsCache.PixelHash(
            OracleGraphicsCache.LoadImage(
                "res://assets/oracle/gfx/spr_item_icons_1.png"));
        Image storedSheet = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_1_spr.png");
        Image equippedSheet = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_item_icons_1.png");
        ItemIconAtlas.ShadeFromPng(
            storedSheet.GetPixel(0, 0), out bool storedBackgroundTransparent);
        int equippedBackgroundShade = ItemIconAtlas.ShadeFromPng(
            equippedSheet.GetPixel(0, 0), out bool equippedBackgroundTransparent);
        ulong expectedEquippedSatchelHash = ItemIconAtlas.DecodedCellHash(
            equippedSheet, 0);
        if (storedSheetHash == equippedSheetHash ||
            _inventoryScreen.StoredItemIconSheet1HashForValidation != storedSheetHash ||
            _inventoryScreen.EquippedItemIconSheet1HashForValidation != equippedSheetHash ||
            !storedBackgroundTransparent || equippedBackgroundTransparent ||
            equippedBackgroundShade != 3 ||
            ItemIconAtlas.EquippedLeftPalette(0x80, 0x05) != 0x03 ||
            ItemIconAtlas.EquippedLeftPalette(0x86, 0x05) != 0x05 ||
            ItemIconAtlas.EquippedLeftPalette(0x8a, 0x05) != 0x03 ||
            _inventoryScreen.EquippedItemIconShadeHashForValidation(0x80) !=
                expectedEquippedSatchelHash ||
            _hud.ItemIconShadeHashForValidation(0x80) != expectedEquippedSatchelHash)
        {
            throw new InvalidOperationException(
                "The Start-menu and equipped Satchel icon paths did not preserve their " +
                "distinct sheets, sprite color indices, or equipped-left palette transform.");
        }
    }

    private void ValidateRingFunctionality()
    {
        RingId[] ids = Enum.GetValues<RingId>();
        if (ids.Length != 0x40 ||
            ids.Select(id => (int)id).Distinct().Count() != 0x40 ||
            ids.Min(id => (int)id) != 0 || ids.Max(id => (int)id) != 0x3f)
        {
            throw new InvalidOperationException(
                "The imported ring index contract no longer covers exactly $00-$3f.");
        }

        var transformedLink = new TransformedLinkDatabase();
        foreach (int specialObject in new[] { 3, 4, 5, 6, 7 })
        for (int direction = 0; direction < 4; direction++)
        for (int frame = 0; frame < 2; frame++)
        {
            FrameRecord record =
                transformedLink.Record(specialObject, direction, frame);
            Image image = transformedLink.Texture(
                specialObject, direction, frame).GetImage();
            bool hasOpaquePixel = false;
            for (int y = 0; y < image.GetHeight() && !hasOpaquePixel; y++)
            for (int x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A > 0.1f)
                {
                    hasOpaquePixel = true;
                    break;
                }
            }
            if (record.InitialDuration != 2 || record.LoopDuration != 6 ||
                image.GetSize() != new Vector2I(32, 32) || !hasOpaquePixel)
            {
                throw new InvalidOperationException(
                    $"Transformed-Link frame {specialObject:x2}:{direction}:{frame} regressed.");
            }
        }

        var swordBeamData = new SwordBeamDatabase();
        if (swordBeamData.Records.Count != 4)
            throw new InvalidOperationException("ITEM_SWORD_BEAM did not import four directions.");
        for (int direction = 0; direction < 4; direction++)
        for (int palettePhase = 0; palettePhase < 2; palettePhase++)
        {
            Image image = swordBeamData.Texture(direction, palettePhase).GetImage();
            if (image.GetSize() != new Vector2I(32, 32) ||
                OracleGraphicsCache.PixelHash(image) == 0)
            {
                throw new InvalidOperationException(
                    $"Sword-beam direction {direction}/palette {palettePhase} did not render.");
            }
        }
        OracleRoomData beamRoom = _world.LoadRoom(0, 0x00);
        SwordBeamDatabaseRecord rightBeam = swordBeamData.Get(1);
        Vector2 beamLinkPosition = Vector2.Zero;
        bool foundBeamPath = false;
        for (int y = 16; y < beamRoom.Height - 16 && !foundBeamPath; y++)
        for (int x = 16; x < beamRoom.Width - 24; x++)
        {
            Vector2 candidate = new(x, y);
            Vector2 origin = candidate + new Vector2(
                rightBeam.OffsetX, rightBeam.OffsetY);
            if (!beamRoom.IsSolid(origin) && !beamRoom.IsSolid(origin + Vector2.Right * 3))
            {
                beamLinkPosition = candidate;
                foundBeamPath = true;
                break;
            }
        }
        if (!foundBeamPath)
            throw new InvalidOperationException("Could not find a clear sword-beam validation path.");
        var beamSounds = new List<int>();
        var beamSpawns = new List<RoomEntitySpawn>();
        var beam = new SwordBeamEffect();
        beam.Initialize(
            swordBeamData, beamRoom, beamLinkPosition, 1,
            static point => point, beamSounds.Add);
        Vector2 initialBeamPosition = beamLinkPosition + new Vector2(
            rightBeam.OffsetX, rightBeam.OffsetY);
        beam.UpdateFrame(3, beamSpawns);
        if (!beam.CollisionEnabled || beam.PrecisePosition != initialBeamPosition ||
            beamSounds.Count != 1 || beamSounds[0] != OracleSoundEngine.SndSwordBeam)
        {
            throw new InvalidOperationException(
                "Sword-beam state 0 offset, collision enable, or sound regressed.");
        }
        beam.UpdateFrame(4, beamSpawns);
        if (beam.Finished || beam.PrecisePosition != initialBeamPosition + Vector2.Right * 3 ||
            beam.PalettePhase != 1)
        {
            throw new InvalidOperationException(
                "Sword beam did not move at SPEED_300 or toggle palette on a four-update boundary.");
        }
        beam.OnEnemyCollision(beamSpawns);
        if (!beam.Finished || beamSpawns.Count != 1 ||
            beamSpawns[0] is not SwordBeamClinkSpawn { Position: var clinkPosition } ||
            clinkPosition != beam.Position)
        {
            throw new InvalidOperationException(
                "Sword-beam collision did not create INTERAC_CLINK:$81 and delete the item.");
        }
        beam.Free();

        var beamRoot = new Node();
        var beamManager = new RoomEntityManager(
            beamRoot, new NpcDatabase(), new EnemyDatabase());
        beamManager.LoadRoom(0, beamRoom);
        if (!beamManager.TrySpawnSwordBeam(beamLinkPosition, 1) ||
            beamManager.TrySpawnSwordBeam(beamLinkPosition, 1) ||
            beamManager.Entities<SwordBeamEffect>().Count != 1)
        {
            throw new InvalidOperationException(
                "ITEM_SWORD_BEAM did not retain its one-object cap.");
        }
        beamManager.Clear();
        beamRoot.Free();

        ulong[] ringBoxLayoutHashes = new ulong[3];
        for (int level = 1; level <= 3; level++)
        {
            OracleSaveData layoutSave = OracleSaveData.CreateStandardGame();
            layoutSave.WriteWramByte(0xc6cc, (byte)level);
            var layoutInventory = new InventoryState(_treasures, layoutSave);
            var layoutScreen = new RingMenuScreen
            {
                Name = $"RingBoxL{level}LayoutValidation"
            };
            AddChild(layoutScreen);
            layoutScreen.Initialize(layoutInventory);
            layoutScreen.Open(RingMenuMode.List);
            ringBoxLayoutHashes[level - 1] = layoutScreen.BackgroundHashForValidation;
            layoutScreen.Free();
        }
        ulong[] expectedRingBoxLayoutHashes =
        [
            0xe266f7d27924bb95UL,
            0xf6a1e59462846015UL,
            0x1a58a1235c7d5e95UL
        ];
        if (!ringBoxLayoutHashes.SequenceEqual(expectedRingBoxLayoutHashes))
        {
            throw new InvalidOperationException(
                "The source L-1/L-2/L-3 Ring Box substitutions no longer expose " +
                $"their 1/3/5-slot layouts ({string.Join(", ",
                    ringBoxLayoutHashes.Select(hash => $"{hash:x16}"))}).");
        }

        _ringMenu.OpenImmediatelyForValidation(RingMenuMode.List, static () => { });
        _ringMenuScreen.SetSelectingList(true);
        if (_ringMenuScreen.BackgroundHashForValidation == 0 ||
            _ringMenuScreen.ListCursorPositionForValidation != new Vector2(20, 42))
        {
            throw new InvalidOperationException(
                $"GFXH_APPRAISED_RING_LIST failed to build or used the wrong " +
                $"upper cursor OAM position " +
                $"(hash {_ringMenuScreen.BackgroundHashForValidation:x16}, " +
                $"cursor {_ringMenuScreen.ListCursorPositionForValidation}).");
        }
        _ringMenuScreen.SetPageAndCursor(0, 8);
        _ringMenuScreen.SetRingName("Power Ring L-1");
        if (_ringMenuScreen.ListCursorPositionForValidation != new Vector2(20, 66) ||
            _ringMenuScreen.DisplayedRingNameForValidation != "Power Ring L-1" ||
            _ringMenuScreen.RingNamePositionForValidation != new Vector2(24, 88))
        {
            throw new InvalidOperationException(
                "The lower ring-list cursor or centered showItemText2 name line regressed.");
        }

        var ringDescriptionDialogue = new DialogueBox();
        ringDescriptionDialogue.ShowPassiveMessage("Ring description", 0, 4);
        if (ringDescriptionDialogue.Position != new Vector2(0, 104))
        {
            throw new InvalidOperationException(
                "Ring-list text position 4 did not begin immediately below the y=88 name line.");
        }
        ringDescriptionDialogue.Free();
        _ringMenuScreen.SetPageAndCursor(0, 0);
        if (!_ringMenuScreen.BeginPageTransition(1, 0, 1))
            throw new InvalidOperationException("The ring list did not begin its rightward page scroll.");
        for (int update = 1; update < RingMenuScreen.PageScrollUpdates; update++)
        {
            _ringMenuScreen.AdvanceAnimation(1.0 / 60.0, out bool completedEarly);
            if (completedEarly || !_ringMenuScreen.PageTransitionActive ||
                _ringMenuScreen.Page != 0)
            {
                throw new InvalidOperationException(
                    "The ring-list page arrived before its original 19 movement updates.");
            }
        }
        _ringMenuScreen.AdvanceAnimation(1.0 / 60.0, out bool completedScroll);
        if (!completedScroll || _ringMenuScreen.PageTransitionActive ||
            _ringMenuScreen.Page != 1 || _ringMenuScreen.ListCursor != 0)
        {
            throw new InvalidOperationException(
                "The ring-list page did not finish after 19 8-pixel updates.");
        }
        _ringMenu.CloseImmediatelyForValidation();

        Vector2 hudPositionBeforeAppraisal = _hud.Position;
        bool hudVisibleBeforeAppraisal = _hud.Visible;
        _ringMenu.OpenImmediatelyForValidation(
            RingMenuMode.Appraisal, static () => { });
        if (_ringMenuScreen.BackgroundSizeForValidation !=
                new Vector2I(OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight) ||
            !Mathf.IsZeroApprox(_ringMenuScreen.BackgroundAlphaForValidation(
                new Vector2I(80, 8))) ||
            Mathf.IsZeroApprox(_ringMenuScreen.BackgroundAlphaForValidation(
                new Vector2I(80, 16))) ||
            _hud.Position != Vector2.Zero ||
            _hud.Visible != hudVisibleBeforeAppraisal ||
            _dialogue.Position != new Vector2(0, 96))
        {
            throw new InvalidOperationException(
                "GFXH_UNAPPRAISED_RING_LIST did not preserve the status bar at y=0-15, " +
                "draw its full map at y=16-143, or place textbox position $02 at y=96.");
        }
        _ringMenu.CloseImmediatelyForValidation();
        if (_hud.Position != hudPositionBeforeAppraisal ||
            _hud.Visible != hudVisibleBeforeAppraisal)
        {
            throw new InvalidOperationException(
                "Ring appraisal mutated the global top-HUD placement or visibility.");
        }

        OracleSaveData appraisalSave = OracleSaveData.CreateStandardGame();
        var appraisal = new InventoryState(_treasures, appraisalSave);
        appraisal.AddRupees(100);
        appraisal.GiveUnappraisedRing((int)RingId.PowerL1);
        if (!appraisal.TryBeginRingAppraisal(0, 20, out int firstRing) ||
            firstRing != (int)RingId.PowerL1 || appraisal.Rupees != 80 ||
            appraisal.RingsAppraised != 1 || appraisal.UnappraisedRingAt(0) != firstRing)
        {
            throw new InvalidOperationException(
                "Paid appraisal did not debit 20 rupees, increment c6ce, and reveal bit 6.");
        }
        RingAppraisalResult first =
            appraisal.CompleteRingAppraisal(0, 30);
        if (first.Duplicate || first.Refund != 0 ||
            !appraisal.HasAppraisedRing(firstRing) || appraisal.UnappraisedRingCount != 0)
        {
            throw new InvalidOperationException(
                "A newly appraised ring did not move from c5c0 into the c616 bitset.");
        }
        appraisal.GiveUnappraisedRing(firstRing);
        if (!appraisal.TryBeginRingAppraisal(0, 20, out _) || appraisal.Rupees != 60)
            throw new InvalidOperationException("Duplicate appraisal did not charge 20 rupees.");
        RingAppraisalResult duplicate =
            appraisal.CompleteRingAppraisal(0, 30);
        if (!duplicate.Duplicate || duplicate.Refund != 30 || appraisal.Rupees != 60)
            throw new InvalidOperationException("Duplicate appraisal did not defer its 30-rupee refund.");
        appraisal.ApplyRingAppraisalRefund(duplicate.Refund);
        if (appraisal.Rupees != 90 || appraisal.RingsAppraised != 2 ||
            appraisalSave.ReadWramByte(0xc6ce) != 2)
        {
            throw new InvalidOperationException(
                "Duplicate refund or wNumRingsAppraised persistence regressed.");
        }

        OracleSaveData boxSave = OracleSaveData.CreateStandardGame();
        boxSave.WriteWramByte(0xc6cc, 2);
        var box = new InventoryState(_treasures, boxSave);
        box.GrantAppraisedRingForDebug((int)RingId.Red);
        box.GrantAppraisedRingForDebug((int)RingId.Blue);
        if (!box.SetRingBoxSlotFromList(0, (int)RingId.Red) ||
            !box.SetRingBoxSlotFromList(1, (int)RingId.Red) ||
            box.RingAt(0) != 0xff || box.RingAt(1) != (int)RingId.Red ||
            !box.SetRingBoxSlotFromList(0, (int)RingId.Blue) ||
            !box.EquipRingAt(0) || box.ActiveRing != (int)RingId.Blue ||
            !box.SetRingBoxSlotFromList(0, (int)RingId.Blue) ||
            box.RingAt(0) != 0xff || !box.DeactivateRingIfMissingFromBox() ||
            box.ActiveRing != 0xff)
        {
            throw new InvalidOperationException(
                "Ring-list move/toggle uniqueness or active-ring cleanup regressed.");
        }
        box.SetRingBoxSlotFromList(2, (int)RingId.Blue);
        box.EquipRingAt(2);
        if (!OracleSaveData.TryDeserialize(boxSave.Serialize(), out OracleSaveData? restored) ||
            new InventoryState(_treasures, restored!).ActiveRing != (int)RingId.Blue ||
            !new InventoryState(_treasures, restored).HasAppraisedRing((int)RingId.Red))
        {
            throw new InvalidOperationException(
                "Ring list, box, and equipped selection did not survive save serialization.");
        }

        InventoryState Wearing(RingId ring)
        {
            OracleSaveData save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6cc, 1);
            save.WriteWramByte(0xc6c6, (byte)ring);
            var inventory = new InventoryState(_treasures, save);
            if (!inventory.EquipRingAt(0))
                throw new InvalidOperationException($"Could not equip ring ${(int)ring:x2} for validation.");
            return inventory;
        }

        (Player Player, ValidationRingPlayerWorld World) RingPlayer(
            RingId ring,
            int swordLevel = 0,
            int health = 12,
            int maxHealth = 12)
        {
            OracleSaveData save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6cc, 1);
            save.WriteWramByte(0xc6c6, (byte)ring);
            save.WriteWramByte(0xc6aa, (byte)health);
            save.WriteWramByte(0xc6ab, (byte)maxHealth);
            save.WriteWramByte(0xc6b2, (byte)swordLevel);
            var inventory = new InventoryState(_treasures, save);
            if (!inventory.EquipRingAt(0))
                throw new InvalidOperationException(
                    $"Could not equip ring ${(int)ring:x2} for Player validation.");
            ValidationRingPlayerWorld world = new ValidationRingPlayerWorld();
            var player = new Player();
            player.Initialize(world, inventory, new Vector2(80, 80), new OracleRandom());
            return (player, world);
        }

        InventoryState friendship = Wearing(RingId.Friendship);
        if (RingEffects.BaseSwordDamage(1) != 2 || RingEffects.BaseSwordDamage(2) != 3 ||
            RingEffects.BaseSwordDamage(3) != 5 ||
            RingEffects.SwordDamage(Wearing(RingId.PowerL1), 1, 0xff) != 3 ||
            RingEffects.SwordDamage(Wearing(RingId.PowerL2), 1, 0xff) != 4 ||
            RingEffects.SwordDamage(Wearing(RingId.PowerL3), 1, 0xff) != 5 ||
            RingEffects.SwordDamage(Wearing(RingId.ArmorL1), 1, 0xff) != 1 ||
            RingEffects.SwordDamage(Wearing(RingId.Red), 1, 0xff) != 4 ||
            RingEffects.SwordDamage(Wearing(RingId.Green), 1, 0xff) != 3 ||
            RingEffects.SwordDamage(Wearing(RingId.Cursed), 1, 0xff) != 1 ||
            RingEffects.SwordDamage(Wearing(RingId.DoubleEdged), 1, 0xff) != 10 ||
            RingEffects.SwordDamage(Wearing(RingId.Whimsical), 1, 1) != 1 ||
            RingEffects.SwordDamage(Wearing(RingId.Whimsical), 1, 0) != 12)
        {
            throw new InvalidOperationException(
                "Sword damage did not match commonCode2.s and sword.s ring arithmetic.");
        }

        if (RingEffects.IncomingDamageQuarters(friendship, 4, RingDamageSource.Generic) != 4 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.PowerL1), 4, RingDamageSource.Generic) != 5 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.PowerL2), 4, RingDamageSource.Generic) != 6 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.PowerL3), 4, RingDamageSource.Generic) != 8 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.ArmorL1), 4, RingDamageSource.Generic) != 4 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.ArmorL2), 4, RingDamageSource.Generic) != 3 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.ArmorL3), 4, RingDamageSource.Generic) != 3 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.Blue), 4, RingDamageSource.Generic) != 2 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.Green), 4, RingDamageSource.Generic) != 3 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.Cursed), 4, RingDamageSource.Generic) != 8 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.Protection), 1, RingDamageSource.Generic) != 4)
        {
            throw new InvalidOperationException(
                "Incoming damage did not match linkUpdateDamageToApplyForRings.");
        }

        if (RingEffects.IncomingDamageQuarters(Wearing(RingId.GreenLuck), 4, RingDamageSource.BladeTrap) != 2 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.BlueLuck), 4, RingDamageSource.Beam) != 2 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.GoldLuck), 4, RingDamageSource.Hole) != 2 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.RedLuck), 4, RingDamageSource.Spike) != 2 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.RedHoly), 4, RingDamageSource.OctorokProjectile) != 0 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.BlueHoly), 4, RingDamageSource.ZoraFire) != 0 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.GreenHoly), 4, RingDamageSource.Electric) != 0 ||
            RingEffects.IncomingDamageQuarters(Wearing(RingId.Bombproof), 4, RingDamageSource.OwnBomb) != 0)
        {
            throw new InvalidOperationException(
                "Luck/Holy/Bombproof source-specific protection table regressed.");
        }

        (int l1Distance, int l1Heal) = RingEffects.HeartRefill(Wearing(RingId.HeartL1));
        (int l2Distance, int l2Heal) = RingEffects.HeartRefill(Wearing(RingId.HeartL2));
        if (RingEffects.KnockbackFrames(Wearing(RingId.Steadfast), 40) != 20 ||
            RingEffects.SwordChargeStep(Wearing(RingId.Charge)) != 4 ||
            RingEffects.SwordSpinCounter(Wearing(RingId.Spin)) != 9 ||
            RingEffects.SwordSpinFrames(Wearing(RingId.Spin), 20) != 36 ||
            l1Distance != 2 << 16 || l1Heal != 0x08 ||
            l2Distance != 3 << 16 || l2Heal != 0x10 ||
            !RingEffects.EnergyBeamOnCharge(Wearing(RingId.Energy)) ||
            RingEffects.SwordBeamMaximumMissingQuarters(Wearing(RingId.LightL1)) != 8 ||
            RingEffects.SwordBeamMaximumMissingQuarters(Wearing(RingId.LightL2)) != 12)
        {
            throw new InvalidOperationException(
                "Steadfast/Charge/Spin/Heart/Energy/Light ring policy regressed.");
        }

        if (RingEffects.BombDamage(4, Wearing(RingId.Blast)) != 6 ||
            RingEffects.BoomerangDamage(1, Wearing(RingId.RangL1)) != 2 ||
            RingEffects.BoomerangDamage(1, Wearing(RingId.RangL2)) != 3 ||
            RingEffects.BombsPlacedPerUse(Wearing(RingId.Bombers)) != 2 ||
            RingEffects.BombsExplode(Wearing(RingId.Peace)) ||
            RingEffects.MapleKillThreshold(Wearing(RingId.Maples)) != 15 ||
            RingEffects.PegasusSeedTimerDecrement(Wearing(RingId.Pegasus)) != 1 ||
            !RingEffects.UsesStrongThrow(Wearing(RingId.Toss)) ||
            !RingEffects.UsesFastSwim(Wearing(RingId.Swimmers)))
        {
            throw new InvalidOperationException(
                "Blast/Rang/Bomber/Peace/Maple/Pegasus/Toss/Swimmer policy regressed.");
        }

        if (!RingEffects.IgnoresIce(Wearing(RingId.Snowshoe)) ||
            !RingEffects.ProtectsCrackedFloor(Wearing(RingId.Rocs)) ||
            !RingEffects.IgnoresQuicksand(Wearing(RingId.Quicksand)) ||
            RingEffects.DropMultiplier(Wearing(RingId.RedJoy), RingDropKind.Rupee) != 2 ||
            RingEffects.DropMultiplier(Wearing(RingId.BlueJoy), RingDropKind.Heart) != 2 ||
            RingEffects.DropMultiplier(Wearing(RingId.GreenJoy), RingDropKind.Ore) != 2 ||
            RingEffects.DropMultiplier(Wearing(RingId.GoldJoy), RingDropKind.Other) != 2 ||
            !RingEffects.DetectsSoftSoil(Wearing(RingId.Discovery)))
        {
            throw new InvalidOperationException(
                "Terrain/Joy/Discovery ring policy regressed.");
        }

        if (RingEffects.LinkTransformation(Wearing(RingId.Octo)) != 5 ||
            RingEffects.LinkTransformation(Wearing(RingId.Moblin)) != 6 ||
            RingEffects.LinkTransformation(Wearing(RingId.LikeLike)) != 7 ||
            RingEffects.LinkTransformation(Wearing(RingId.Subrosian)) != 3 ||
            RingEffects.LinkTransformation(Wearing(RingId.FirstGen)) != 4 ||
            !RingEffects.PreventsJinx(Wearing(RingId.Whisp)) ||
            RingEffects.GashaKillCredits(Wearing(RingId.Gasha)) != 2 ||
            !RingEffects.RemovesDiveTimer(Wearing(RingId.Zora)) ||
            !RingEffects.CanPunch(Wearing(RingId.Fist), bothButtonsEmpty: true) ||
            !RingEffects.CanPunch(Wearing(RingId.Experts), bothButtonsEmpty: true) ||
            !RingEffects.UsesExpertPunch(Wearing(RingId.Experts)))
        {
            throw new InvalidOperationException(
                "Transformation/Whisp/Gasha/Zora/Fist/Expert ring policy regressed.");
        }

        Rect2[] expectedPunchHitboxes =
        [
            new(new Vector2(92, 83), new Vector2(10, 10)),
            new(new Vector2(107, 95), new Vector2(10, 10)),
            new(new Vector2(98, 107), new Vector2(10, 10)),
            new(new Vector2(83, 95), new Vector2(10, 10))
        ];
        for (int direction = 0; direction < 4; direction++)
        {
            if (Player.GetSwordHitboxForValidation(
                    new Vector2(100, 100), 24 + direction) !=
                expectedPunchHitboxes[direction])
            {
                throw new InvalidOperationException(
                    $"Punch collision arc ${24 + direction:x2} regressed.");
            }
        }

        (Player fistPlayer, ValidationRingPlayerWorld fistWorld) =
            RingPlayer(RingId.Fist);
        fistPlayer.StartPunchActionForValidation(Vector2.Up);
        if (!fistPlayer.IsUsingPunch || fistPlayer.SwordDamage != 1 ||
            fistWorld.SwordHitCalls != 1 || fistWorld.LastSwordDamage != 1 ||
            !fistWorld.Sounds.Contains(OracleSoundEngine.SndStrike))
        {
            throw new InvalidOperationException(
                "Fist Ring did not begin its four-update, one-damage punch collision.");
        }
        fistPlayer.AdvancePunchForValidation(3);
        if (fistWorld.SwordHitCalls != 4 || !fistPlayer.IsUsingPunch)
            throw new InvalidOperationException("Fist Ring punch collision duration regressed.");
        fistPlayer.AdvancePunchForValidation(5);
        if (fistPlayer.IsUsingPunch || fistPlayer.PunchFrame != 0)
            throw new InvalidOperationException("Fist Ring LINK_ANIM_MODE_21 did not end at update 8.");

        (Player expertPlayer, ValidationRingPlayerWorld expertWorld) =
            RingPlayer(RingId.Experts);
        expertPlayer.StartPunchActionForValidation(Vector2.Right);
        if (!expertPlayer.IsUsingPunch || expertPlayer.SwordDamage != 4 ||
            expertWorld.ExpertTileHitCalls != 1 || expertWorld.SwordHitCalls != 1 ||
            !expertWorld.Sounds.Contains(OracleSoundEngine.SndExplosion))
        {
            throw new InvalidOperationException(
                "Expert's Ring did not apply source $03 tile breakage and four damage.");
        }
        expertPlayer.AdvancePunchForValidation(14);
        if (expertPlayer.IsUsingPunch)
            throw new InvalidOperationException(
                "Expert's Ring LINK_ANIM_MODE_34 did not end at update 14.");

        (Player transformedPlayer, ValidationRingPlayerWorld transformedWorld) =
            RingPlayer(RingId.Octo);
        transformedPlayer.RefreshTransformationForValidation();
        transformedPlayer.AdvanceTransformationForValidation(walking: true, frames: 2);
        if (transformedPlayer.ActiveTransformation != 5 ||
            transformedPlayer.TransformationFrame != 1)
        {
            throw new InvalidOperationException(
                "Octo Ring did not select SPECIALOBJECT_LINK_AS_OCTOROK or honor 2/6 timing.");
        }
        transformedWorld.RingTransformationsAllowed = false;
        transformedPlayer.RefreshTransformationForValidation();
        if (transformedPlayer.ActiveTransformation != 0)
            throw new InvalidOperationException(
                "Transformation suppression did not restore ordinary Link.");

        (Player fullBeamPlayer, ValidationRingPlayerWorld fullBeamWorld) =
            RingPlayer(RingId.Friendship, swordLevel: 2);
        fullBeamPlayer.Face(Vector2I.Up);
        fullBeamPlayer.StartSwordAttackForValidation(Vector2.Zero);
        fullBeamPlayer.AdvanceSwordForValidation(6, buttonHeld: false);
        if (fullBeamWorld.SwordBeamCalls != 1 ||
            fullBeamWorld.LastSwordBeamDirection != 0)
        {
            throw new InvalidOperationException(
                "Level-2 sword did not create a full-health beam on swing marker bit 5.");
        }

        (Player hurtBeamPlayer, ValidationRingPlayerWorld hurtBeamWorld) =
            RingPlayer(RingId.Friendship, swordLevel: 2, health: 11, maxHealth: 12);
        hurtBeamPlayer.StartSwordAttackForValidation(Vector2.Zero);
        hurtBeamPlayer.AdvanceSwordForValidation(6, buttonHeld: false);
        (Player lightBeamPlayer, ValidationRingPlayerWorld lightBeamWorld) =
            RingPlayer(RingId.LightL1, swordLevel: 2, health: 12, maxHealth: 20);
        lightBeamPlayer.StartSwordAttackForValidation(Vector2.Zero);
        lightBeamPlayer.AdvanceSwordForValidation(6, buttonHeld: false);
        (Player weakSwordPlayer, ValidationRingPlayerWorld weakSwordWorld) =
            RingPlayer(RingId.LightL2, swordLevel: 1, health: 12, maxHealth: 12);
        weakSwordPlayer.StartSwordAttackForValidation(Vector2.Zero);
        weakSwordPlayer.AdvanceSwordForValidation(6, buttonHeld: false);
        if (hurtBeamWorld.SwordBeamCalls != 0 || lightBeamWorld.SwordBeamCalls != 1 ||
            weakSwordWorld.SwordBeamCalls != 0)
        {
            throw new InvalidOperationException(
                "Base/Light Ring health thresholds or the level-2 sword beam gate regressed.");
        }

        (Player energyPlayer, ValidationRingPlayerWorld energyWorld) =
            RingPlayer(RingId.Energy, swordLevel: 1);
        energyPlayer.StartSwordAttackForValidation(Vector2.Zero);
        energyPlayer.AdvanceSwordForValidation(17, buttonHeld: true);
        energyPlayer.AdvanceSwordForValidation(41, buttonHeld: true);
        if (energyWorld.SwordBeamCalls != 1 ||
            energyPlayer.SwordState != SwordActionState.Poke ||
            energyWorld.Sounds.Contains(OracleSoundEngine.SndChargeSword))
        {
            throw new InvalidOperationException(
                "Energy Ring did not replace completed charge with a beam and sword poke.");
        }
        foreach (Player validationPlayer in new[]
        {
            fistPlayer, expertPlayer, transformedPlayer, fullBeamPlayer,
            hurtBeamPlayer, lightBeamPlayer, weakSwordPlayer, energyPlayer
        })
        {
            validationPlayer.Free();
        }

        OracleSaveData slayerSave = OracleSaveData.CreateStandardGame();
        slayerSave.WriteWramByte(0xc620, 0xe7);
        slayerSave.WriteWramByte(0xc621, 0x03);
        var slayer = new InventoryState(_treasures, slayerSave);
        slayer.RecordEnemyKill();
        slayer.RecordEnemyKill();
        OracleSaveData gashaSave = OracleSaveData.CreateStandardGame();
        gashaSave.WriteWramByte(0xc6cc, 1);
        gashaSave.WriteWramByte(0xc6c6, (byte)RingId.Gasha);
        var gasha = new InventoryState(_treasures, gashaSave);
        gasha.EquipRingAt(0);
        gasha.RecordEnemyKill();
        OracleSaveData wealthSave = OracleSaveData.CreateStandardGame();
        wealthSave.WriteWramByte(0xc627, 0x99);
        wealthSave.WriteWramByte(0xc628, 0x99);
        var wealth = new InventoryState(_treasures, wealthSave);
        wealth.AddRupees(1);
        if (slayer.TotalEnemiesKilled != 1000 || !slayerSave.HasGlobalFlag(0x00) ||
            slayerSave.ReadWramByte(0xc641) != 2 ||
            slayerSave.ReadWramByte(0xc64f) != 2 || slayerSave.GashaMaturity != 6 ||
            gashaSave.ReadWramByte(0xc641) != 1 ||
            Enumerable.Range(0, 0x10).Any(
                spot => gashaSave.ReadWramByte(0xc64f + spot) != 2) ||
            gashaSave.GashaMaturity != 3 ||
            wealth.TotalRupeesCollected != 0 || !wealthSave.HasGlobalFlag(0x01))
        {
            throw new InvalidOperationException(
                "Slayer/Rupee awards or Maple/Gasha enemy counters regressed.");
        }

        GD.Print("Validated all 64 ring IDs, appraisal/list/box/equip persistence, " +
            "L-1/L-2/L-3 slot substitutions, LCD-split selection/name rows, " +
            "cleared name-buffer tiles, centered name text, source OAM cursor rows, " +
            "damage arithmetic, protections, native punches/transformations/sword beams, " +
            "movement/item/drop policies, and Vasu/Maple/Gasha award counters.");
    }
}
