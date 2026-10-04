using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{

    private void ValidateMapMenuRom(bool interior, bool largeInterior = false, bool compareOam = false,
        bool comparePixels = false, bool compareFramePixels = false)
    {
        int hostCase1 = 0;
        foreach (bool linked in largeInterior ? new[] { false, true } : new[] { false })
        foreach (int portalGroup in compareOam ? new[] { 0xff, 0, 1 } : new[] { 0xff })
        foreach (var location in largeInterior ? new[] { (Group: 5, Room: 0xb0), (Group: 5, Room: 0xb6),
            (Group: 4, Room: 0xe0), (Group: 4, Room: 0xe6) } : interior ? new[] { (Group: 0, Room: 0x38), (Group: 1, Room: 0x38),
            (Group: 2, Room: 0x4e), (Group: 2, Room: 0x4f) } : new[] { (Group: 0, Room: 0x45), (Group: 1, Room: 0x45) })
        // Remembered group and room are independent loader inputs. Cover each
        // value without replaying their entire product with every portal/link.
        foreach (var remembered in interior
            ? new[] { (Group: 0, Room: 0x00), (Group: 1, Room: 0x45),
                (Group: 2, Room: 0xcd), (Group: 5, Room: 0x45) }
            : new[] { (Group: location.Group, Room: location.Room) })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            if (interior && portalGroup != 0xff && remembered.Group != 1) continue;
            if (linked && remembered.Group != 0) continue;
            int rememberedGroup = remembered.Group, rememberedRoom = remembered.Room;
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay();
            _saveData.SetLinkedGame(linked);
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(location.Group, location.Room);
            if (interior) _saveData.SetMinimapLocation(rememberedGroup, rememberedRoom);
            if (compareOam)
            {
                _saveData.WriteWramByte(0xc63e, (byte)portalGroup);
                _saveData.WriteWramByte(0xc63f, 0x45);
            }
            var rom = new MenuRom(_saveData, _currentRoom);
            if (largeInterior)
            {
                rom.LoadRoomTileset();
                FailIf(rom[0xcc39] != (location.Group == 5 ? 0x0e : 0x0f) ||
                    (rom[0xcc34] & 0x18) != 0x18 || rom[0xcc34] != _currentRoom.TilesetFlags,
                    "Large-interior fixture missed native dungeon/large-interior flags or room era.");
            }
            _mapMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(2);
            var sounds = _sound.AttachPlayRequestAudit();
            Vector2 position = _player.Position;
            int edge = 0, update = 0;
            Texture2D? lastBackground = null;
            Texture2D? lastFrameBackground = null;
            byte[]? lastFrameCommands = null;
            void Step(int updates = 1, int pressed = 0, int? held = null)
            {
                edge = pressed;
                application.Step(updates, Vector2.Right, MenuRomActions(held ?? pressed),
                    MenuRomActions(pressed), batched, () =>
                {
                    int frame = _saveData.ReadWramByte(0xc622);
                    rom.Update(edge, held ?? pressed, frame);
                    edge = 0;
                    update++;
                    FailIf(_player.Position != position || !_gameplayPause.IsLeased,
                        "Map input leaked movement through modal ownership.");
                    FailIf((int)_mapScreen.Mode != rom[0xcbb3] || _mapScreen.CursorRoom != rom[0xcbb6] ||
                        _mapScreen.PopupSize != rom[0xcbbd] || _mapScreen.PopupPrimary != rom[0xcbbe] ||
                        _mapScreen.PopupAlternate != rom[0xcbc0],
                        $"Map ${location.Group:x1}:${location.Room:x2} minimap=${rememberedGroup:x1}:${rememberedRoom:x2} update {update}: cursor/popup runtime=${_mapScreen.CursorRoom:x2}/{_mapScreen.PopupSize}/{_mapScreen.PopupPrimary}/{_mapScreen.PopupAlternate}, ROM=${rom[0xcbb6]:x2}/{rom[0xcbbd]}/{rom[0xcbbe]}/{rom[0xcbc0]}.");
                    FailIf(_mapScreen.LocationArrowVisible != ((frame & 0x20) == 0),
                        "Map Link marker lost the native global-frame phase.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Map update {update}: sound requests differ from ROM.");
                    if (compareOam) CompareMapMenuOamRom(rom,
                        $"Map OAM ${location.Group:x1}:${location.Room:x2} update={update} batch={batched}");
                    if (comparePixels) CompareMapBackgroundPixelsRom(rom, ref lastBackground,
                        $"Map background ${location.Group:x1}:${location.Room:x2} update={update} batch={batched}");
                    if (compareFramePixels) CompareMapFramePixelsRom(rom, ref lastFrameBackground, ref lastFrameCommands,
                        $"Map frame ${location.Group:x1}:${location.Room:x2} update={update} batch={batched}");
                });
            }
            if (interior)
            {
                int expectedRoom = (location.Room == 0x38 && location.Group is 0 or 1) ? 0x38 : rememberedRoom;
                // roomsInAltWorld adds PAST to $5:$b0 and $4:$e0 after
                // tileset loading; both base tilesets have flags $18.
                int expectedMode = location.Group == 1 || location.Room is 0x4f or 0xb0 or 0xe0 ? 1 : 0;
                FailIf(_mapScreen.CursorRoom != expectedRoom || (int)_mapScreen.Mode != expectedMode ||
                    rom[0xcbb6] != expectedRoom || rom[0xcbb3] != expectedMode,
                    $"Map initialization ${location.Group:x1}:${location.Room:x2} remembered=${rememberedGroup:x1}:${rememberedRoom:x2} linked={linked}: expected mode/room={expectedMode}/${expectedRoom:x2}, runtime={(int)_mapScreen.Mode}/${_mapScreen.CursorRoom:x2}, native={rom[0xcbb3]}/${rom[0xcbb6]:x2}, flags=${rom[0xcc34]:x2}.");
            }
            Step(55); // Expansion and both popup-alternation boundaries.
            Step(1, 0x17); // Direction wins A/B/Select.
            Step(43, held: 0x10); // Retained repeat bytes, first repeat and interval.
            Step(1, 0xf0);
            for (int direction = 0x10; direction <= 0x80; direction <<= 1)
                for (int count = 0; count < 17; count++) Step(1, direction);
            Step(8);
            _mapMenu.CloseImmediatelyForValidation();
            _mapMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(2);
            sounds.Clear();
            Step();
            // Unvisited A consumes B/Select but cannot open text or close.
            int group = _mapScreen.Mode == MapMode.Past ? 1 : 0;
            int unvisited = Enumerable.Range(0, 0x100).First(room =>
                (room & 15) < 14 && (room >> 4) < 14 && (_saveData.GetRoomFlags(group, room) & 0x10) == 0);
            while (_mapScreen.CursorRoom != unvisited)
            {
                int key = (_mapScreen.CursorRoom & 15) != (unvisited & 15) ? 0x10 : 0x80;
                Step(1, key);
            }
            Step(1, 7);
            FailIf(_dialogue.IsOpen || !_mapMenu.IsOpen || rom[0xcbcc] != 1 || rom[0xcba0] != 0,
                "Unvisited map A+B+Select changed native text/close priority.");
            if (compareOam) CloseMapMenuOamRom(rom, batched, $"Map ${location.Group:x1}:${location.Room:x2} portal=${portalGroup:x2}", compareFramePixels);
            else _mapMenu.CloseImmediatelyForValidation();
        }
        GD.Print($"Validated clean-US present/past maps, interior/Maku variants={interior}, large-interior dungeon layouts $0e/$0f={largeInterior}: linked-mode/tileset selection, remembered minimap, popup expansion/alternation/shrink, edge wrapping, input priority, repeat, unvisited text gate and re-entry in split/batched application updates.");
    }
}
