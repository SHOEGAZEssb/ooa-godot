using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateMapDisassemblyFidelity()
    {
        Vector2[] crowdedOam = Enumerable.Repeat(new Vector2(-8, 0), 11).ToArray();
        ushort[] selectedScanlines = MapScreen.SelectOamScanlines(crowdedOam);
        FailIf(selectedScanlines[0] != 0 || selectedScanlines.Skip(1).Any(mask => mask != 0xffff),
            "Map OAM did not retain only the first ten source sprites on a scanline, including offscreen X.");
        crowdedOam[0] = new Vector2(0, 8);
        selectedScanlines = MapScreen.SelectOamScanlines(crowdedOam);
        FailIf(selectedScanlines[0] != 0xff00,
            "Map OAM scanline limit discarded non-overlapping rows of its eleventh sprite.");
        FailIf(_scene.MenuFade.Material is not CanvasItemMaterial fadeMaterial ||
            fadeMaterial.BlendMode != CanvasItemMaterial.BlendModeEnum.Add,
            "Menu fade does not add and saturate source palette channels.");
        var fadeRect = new ColorRect();
        var fade = new FixedUpdateFadeController(fadeRect);
        fade.Begin(Direction.ToWhite);
        for (int i = 1; i <= 11; i++)
        {
            fade.AdvanceOneUpdate();
            FailIf(!Mathf.IsEqualApprox(fadeRect.Color.A, System.Math.Min(31, i * 3) / 31.0f),
                $"fastFadeoutToWhite lost RGB offset ${i * 3:x2} at update {i}.");
        }
        fade.Begin(Direction.FromWhite);
        for (int i = 1; i <= 11; i++)
        {
            fade.AdvanceOneUpdate();
            FailIf(!Mathf.IsEqualApprox(fadeRect.Color.A, System.Math.Max(0, 32 - i * 3) / 31.0f),
                $"fastFadeinFromWhite lost $20-minus-speed arithmetic at update {i}.");
        }
        fadeRect.Free();
        int frame = 0x20;
        _mapScreen.Initialize(_rooms, _inventory, () => frame);
        LoadValidationRoom(0, 0x45);
        _mapMenu.OpenImmediatelyForValidation();
        FailIf(_mapScreen.LocationArrowVisible || _mapScreen.PopupSize != 1 || _sound.MusicVolume != 2,
            "mapMenu_state0 reset the global $20 arrow phase or omitted its first popup draw.");
        for (int i = 0; i < 5; i++) Tick();
        FailIf(_mapScreen.PopupSize != 3, "Popup expanded before its seventh source draw.");
        Tick();
        FailIf(_mapScreen.PopupSize != 4, "Popup did not reach size $04 on its seventh draw.");
        for (int i = 0; i < 22; i++) Tick();
        FailIf(_mapScreen.PopupAlternate != 0, "Popup alternated before the initial $17 counter expired.");
        Tick();
        FailIf(_mapScreen.PopupAlternate != 1, "Popup did not alternate on the $17 zero update.");
        for (int i = 0; i < 23; i++) Tick();
        FailIf(_mapScreen.PopupAlternate != 1, "Popup repeated before the subsequent $18 counter expired.");
        Tick();
        FailIf(_mapScreen.PopupAlternate != 0, "Popup did not alternate after $18 subsequent updates.");
        Tick(["move_left", "attack", "item", "map"], ["move_left", "attack", "item", "map"]);
        FailIf(!_mapMenu.IsOpen || _dialogue.IsOpen || _mapScreen.CursorRoom != 0x44 || _mapScreen.PopupSize != 3,
            "Overworld direction priority or same-update popup shrink disagrees with bank2.s.");
        Tick();
        FailIf(_mapScreen.PopupSize != 3, "Popup shrink skipped its two-update delay.");
        Tick();
        FailIf(_mapScreen.PopupSize != 2, "Popup shrink failed to reach size $02.");
        _mapMenu.CloseImmediatelyForValidation();

        LoadValidationRoom(0, 0x45);
        _mapMenu.OpenImmediatelyForValidation();
        int closeSounds = _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu);
        Tick(["item"], ["item"]);
        int closingPopupSize = _mapScreen.PopupSize;
        FailIf(_mapMenu.IsOpen || _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) != closeSounds + 1,
            "closeMenu did not request SND_CLOSEMENU $55 on the B edge.");
        for (int i = 0; i < 10; i++) Tick();
        FailIf(_mapScreen.PopupSize != closingPopupSize || !_mapScreen.Visible || _sound.MusicVolume != 2,
            "menuStateFadeOutOfMenu animated retained OAM or restored music before the return fade.");
        for (int i = 0; i < 12; i++) Tick();
        FailIf(_mapMenu.IsActive || _sound.MusicVolume != 3,
            "menuStateFadeIntoGame failed to restore music volume $03 after both closing fades.");

        _mapMenu.BeginOpeningForValidation();
        Tick();
        Tick(["inventory", "map"], ["inventory"]);
        FailIf(_mapMenu.IsActive || !_inventoryMenu.IsActive ||
            !_gameplayPause.IsOwnedBy(_inventoryMenu) || _player.IsPhysicsProcessing() || _mapScreen.Visible,
            "Held Start+Select failed to transfer map opening and its pause lease to MENU_SAVEQUIT.");
        for (int i = 0; i < 20; i++) _inventoryMenu.Update(1.0 / 60);
        FailIf(!_inventoryMenu.SaveMenuOpen, "Transferred MENU_SAVEQUIT failed to finish the existing opening fade.");
        _inventoryMenu.CloseImmediatelyForValidation();

        LoadValidationRoom(0, 0x45);
        _mapMenu.OpenImmediatelyForValidation();
        Tick(["attack", "item"], ["attack", "item"]);
        FailIf(!_dialogue.IsOpen || !_mapMenu.IsOpen || _dialogue.Position.Y != 96 ||
            _dialogue.TextboxFlagsForValidation != 0x09,
            "mapGetRoomTextOrReturn lost upper-cell position $03, flags $09, or A priority over B.");
        _dialogue.Close();
        _mapMenu.CloseImmediatelyForValidation();
        LoadValidationRoom(0, 0x95);
        _mapMenu.OpenImmediatelyForValidation();
        Tick(["attack"], ["attack"]);
        FailIf(!_dialogue.IsOpen || _dialogue.Position.Y != 8 || _dialogue.TextboxFlagsForValidation != 0x09,
            "Lower-map description did not force textbox position $00 at screen Y=8.");
        _dialogue.Close();
        _mapMenu.CloseImmediatelyForValidation();

        LoadValidationRoom(0, 0x00);
        _mapMenu.OpenImmediatelyForValidation();
        Tick();
        Tick(["move_right"], ["move_right"]);
        for (int i = 0; i < 39; i++) Tick(["move_right"]);
        FailIf(_mapScreen.CursorRoom != 0x01, "Map direction repeated before $28 held handler calls.");
        Tick(["move_right"]);
        FailIf(_mapScreen.CursorRoom != 0x02, "Map direction did not repeat at $28.");
        for (int i = 0; i < 3; i++) Tick(["move_right"]);
        FailIf(_mapScreen.CursorRoom != 0x02, "Map repeated before its four-update cadence.");
        Tick(["move_right"]);
        FailIf(_mapScreen.CursorRoom != 0x03, "Map repeat lost its four-update cadence.");
        Tick(["move_up", "move_down"], ["move_up", "move_down"]);
        FailIf(_mapScreen.CursorRoom != 0xd3, "Overworld Up/Down priority or vertical wrap changed.");
        _mapMenu.CloseImmediatelyForValidation();

        LoadValidationRoom(4, 0x46);
        FailIf(_saveData.MinimapGroup != 4 || _saveData.MinimapRoom != 0x46 ||
            _saveData.MinimapDungeonFloor != 1 || _saveData.MinimapDungeonPosition != 0x2b ||
            _saveData.DungeonVisitedFloors(2) != 2,
            "checkUpdateDungeonMinimap did not save dungeon $02 floor $01/cell $2b and visited bit $02.");
        _saveData.SetRoomFlag(4, 0x46, 0x02);
        _mapMenu.OpenImmediatelyForValidation();
        FailIf(_mapScreen.DungeonTileAt(3, 5) != 0xb7 ||
            _mapScreen.DungeonScreenTileAt(10, 15) != 0xad || _mapScreen.Navigate(Vector2I.Down),
            "Dungeon map omitted saved door bits or revealed an unavailable floor.");
        _mapMenu.CloseImmediatelyForValidation();
        // A floor can stay revealed independently of its per-room visit bits.
        _saveData.WriteWramByte(0xc664, 3);
        _mapMenu.OpenImmediatelyForValidation();
        Tick(["move_up", "move_down"], ["move_up", "move_down"]);
        FailIf(_mapScreen.DisplayedDungeonFloor != 0 || _mapScreen.DungeonScrollY != 0 || !_mapScreen.IsScrolling,
            "dungeonMap_scrollingState0 lost Down priority or moved pixels on the selection update.");
        for (int i = 1; i <= 10; i++)
        {
            Tick(["move_up"], ["move_up"]);
            FailIf(_mapScreen.DungeonScrollY != i || !_mapScreen.IsScrolling || _mapScreen.DisplayedDungeonFloor != 0,
                $"Dungeon scroll did not advance one tile at update ${i:x2}, or accepted input mid-scroll.");
        }
        Tick(["move_up"], ["move_up"]);
        FailIf(_mapScreen.IsScrolling || _mapScreen.DungeonScrollY != 10 || _mapScreen.DisplayedDungeonFloor != 0,
            "Dungeon scroll zero update failed to unlock without consuming new direction input.");
        _mapMenu.CloseImmediatelyForValidation();

        // Hidden side-view cells must not replace the retained top-down cell.
        LoadValidationRoom(4, 0x27);
        FailIf((_rooms.CurrentRoom.TilesetFlags & 0x20) == 0 ||
            _saveData.MinimapDungeonPosition != 0x2b || _saveData.MinimapDungeonFloor != 1,
            "Dungeon $02 side-view room $27 overwrote the preceding top-down minimap position.");
        _mapMenu.OpenImmediatelyForValidation();
        FailIf(_mapScreen.DisplayedDungeonFloor != 1 || _mapScreen.DungeonLinkIconPosition != new Vector2(104, 72),
            "Side-view map failed to display retained floor $01/cell $2b.");
        _mapMenu.CloseImmediatelyForValidation();

        // Skip two unavailable middle floors: 30 moving updates plus the zero update.
        DungeonInfo dungeon8 = _rooms.DungeonMaps.GetDungeon(8);
        DungeonCell top = dungeon8.Cells.First(c => c.Floor == 3 && c.Properties != 0x60);
        LoadValidationRoom(5, top.Room);
        _saveData.WriteWramByte(0xc66a, 9);
        _mapMenu.OpenImmediatelyForValidation();
        FailIf(!_mapScreen.Navigate(Vector2I.Down) || _mapScreen.DisplayedDungeonFloor != 0,
            "Dungeon $08 did not skip unrevealed floors $02/$01.");
        for (int i = 0; i < 30; i++) Tick();
        FailIf(!_mapScreen.IsScrolling || _mapScreen.DungeonScrollY != 30,
            "Three-floor scroll did not preserve its $1e movement updates.");
        Tick();
        FailIf(_mapScreen.IsScrolling, "Three-floor scroll did not finish on update $1f.");
        _mapMenu.CloseImmediatelyForValidation();

        // loadMinimapDisplayRoom uses the tileset era and hardcodes Maku $38.
        LoadValidationRoom(0, 0x38);
        FailIf((_rooms.CurrentRoom.TilesetFlags & 2) == 0, "Room $0:38 lost TILESETFLAG_MAKU.");
        _saveData.SetMinimapLocation(1, 0x11);
        _mapMenu.OpenImmediatelyForValidation();
        FailIf(_mapScreen.CursorRoom != 0x38 || _mapScreen.Mode != MapMode.Present,
            "Maku map did not select source room $38 and tileset era independently of wMinimapGroup.");
        _mapMenu.CloseImmediatelyForValidation();
        OracleRoomData pastIndoor = _rooms.GetRoom(2, 0x4f);
        OracleRoomData presentIndoor = _rooms.GetRoom(2, 0x4e);
        FailIf((pastIndoor.TilesetFlags & 0x80) == 0 || (presentIndoor.TilesetFlags & 0x80) != 0,
            "roomsInAltWorldGroup2 dbrev did not distinguish $2:4f from $2:4e.");
        _rooms.Load(2, 0x4f);
        _saveData.SetMinimapLocation(0, 0x45);
        _mapMenu.OpenImmediatelyForValidation();
        FailIf(_mapScreen.Mode != MapMode.Past,
            "Indoor map era followed the retained minimap group instead of roomsInAltWorldGroup2.");
        _mapMenu.CloseImmediatelyForValidation();
        GD.Print("Validated MENU_MAP source input priority/repeat, popup and scroll counters, textbox flags, saved floor masks, doors, and side-view position.");

        void Tick(string[]? held = null, string[]? pressed = null)
        {
            frame = (frame + 1) & 0xff;
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot(held ?? [], pressed ?? [], Vector2.Zero));
            try { _mapMenu.Update(1.0 / 60); }
            finally { Input.EndOriginalUpdate(); }
        }
    }
}
