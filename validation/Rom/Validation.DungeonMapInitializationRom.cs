using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{

    private void RunDungeonMapInitializationRom(bool alternate, bool compareOam = false,
        bool comparePixels = false, bool compareFramePixels = false)
    {
        // dungeonDataTable: the ordinary $00-$08 dungeons and final $0a.
        // $0b-$0d have real dungeon-map room tilesets; $0e/$0f use
        // overworld menus through TILESETFLAG_LARGE_INDOORS. $09 aliases
        // $00's layout but
        // no supported room tileset selects it. Linked mode does not choose
        // layouts: loadTilesetData takes the dungeon nibble from tilesetData.
        int[] indices = alternate ? [0x0b, 0x0c, 0x0d] : [0, 1, 2, 3, 4, 5, 6, 7, 8, 0x0a];
        int[] floors = alternate ? [2, 2, 1] : [1, 1, 2, 2, 2, 2, 1, 3, 4, 1];
        var database = new DungeonMapDatabase();
        int cases = 0;
        int hostCase1 = 0;
        foreach (bool linked in alternate ? new[] { false, true } : new[] { false })
        foreach (int equipment in Enumerable.Range(0, 4))
        foreach (int index in indices)
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            DungeonInfo info = database.GetDungeon(index);
            int expectedFloors = floors[System.Array.IndexOf(indices, index)];
            FailIf(info.FloorCount != expectedFloors,
                $"Dungeon ${index:x2}: floor count differs from independent source header.");
            foreach (int floor in Enumerable.Range(0, expectedFloors))
            {
                // Select a reachable top-down room on each source floor. The
                // native loader independently finds its first layout occurrence.
                DungeonCell cell = info.Cells.First(candidate => candidate.Floor == floor && candidate.Room != 0 &&
                    info.TryGetRoom(candidate.Room, out var first) && first.Floor == floor &&
                    _rooms.World.HasRoom(info.Group, candidate.Room) &&
                    _rooms.World.GetDungeonIndex(info.Group, candidate.Room) == index &&
                    (_rooms.World.LoadRoom(info.Group, candidate.Room).TilesetFlags & 0x38) == 0x08);
                var save = OracleSaveData.CreateStandardGame();
                save.SetLinkedGame(linked);
                save.WriteWramByte(0xc686 + index / 8, (byte)((equipment & 1) != 0 ? 1 << (index & 7) : 0));
                save.WriteWramByte(0xc684 + index / 8, (byte)((equipment & 2) != 0 ? 1 << (index & 7) : 0));
                if (compareOam) save.WriteWramByte(0xc682 + index / 8,
                    (byte)((equipment & 2) != 0 ? 1 << (index & 7) : 0));
                int keys = new[] { 0, 1, 9 }[(index + equipment) % 3];
                save.WriteWramByte(0xc672 + index, (byte)keys);
                foreach (var room in info.Cells.Where(entry => entry.Room != 0).Select(entry => entry.Room).Distinct())
                {
                    save.SetRoomFlag(info.Group, room, 0x10, equipment == 3 || equipment != 0 && (room & 1) == 0);
                    save.SetRoomFlag(info.Group, room, 0x20, (room & 2) != 0);
                    save.SetRoomFlag(info.Group, room, 0x0f, false);
                    save.SetRoomFlag(info.Group, room, (byte)(room & 0x0f), true);
                }
                InitializeTransientSession(save);
                LoadValidationRoom(info.Group, cell.Room); _entities.Clear();
                var rom = new MenuRom(_saveData, _currentRoom);
                // PALH_09 replaces only BG palettes 2-5. Preserve the actual
                // PALH_0f gameplay palette 0 used by boss-room tile $83.
                if (comparePixels || compareFramePixels) rom.LoadHudGraphics();
                rom.LoadRoomTileset();
                FailIf(rom[0xcc39] != index || rom[0xcc34] != _currentRoom.TilesetFlags,
                    $"Room ${info.Group:x1}:${cell.Room:x2}: native tileset dungeon/flags differ from the runtime selection.");
                rom.LoadDungeon(index);
                string context = $"Dungeon ${index:x2} floor={floor} room=${info.Group:x1}:${cell.Room:x2} equipment={equipment} linked={linked} batch={batched}";
                FailIf(rom[0xcc40] != expectedFloors || rom[0xcc3b] != floor ||
                    _saveData.MinimapDungeonFloor != rom[0xcc3b] ||
                    _saveData.MinimapDungeonPosition != rom[0xcc3a] ||
                    info.BaseFloor != rom[0xcc41] || info.CompassFloors != rom[0xcc42],
                    context + ": source header or independently resolved minimap floor/cell differs.");
                _mapMenu.OpenImmediatelyForValidation(); rom.OpenImmediately(2);
                var sounds = _sound.AttachPlayRequestAudit();
                int update = 0;
                Texture2D? lastBackground = null;
                Texture2D? lastFrameBackground = null;
                byte[]? lastFrameCommands = null;
                void Compare()
                {
                    FailIf(_mapScreen.Mode != MapMode.Dungeon ||
                        _mapScreen.DisplayedDungeonFloor != rom[0xcc40] - 1 - rom[0xcbb7] ||
                        _mapScreen.DungeonScrollY != rom[0xcbb8] || _mapScreen.IsScrolling != (rom[0xcbce] != 0),
                        context + $" update={update}: initial or scrolled floor/offset differs.");
                    // Compare all screen tile commands, including floor names,
                    // key digits, hidden/visited/chest/boss rooms and adjacent
                    // floors that intersect the 18-row scrolling window.
                    for (int y = 0; y < 18; y++)
                    for (int x = 0; x < 20; x++)
                        FailIf(_mapScreen.DungeonScreenTileAt(x, y) != rom.MapTile(x, y),
                            context + $" update={update} tile=({x},{y}): runtime=${_mapScreen.DungeonScreenTileAt(x, y):x2}, native=${rom.MapTile(x, y):x2}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": floor selection sound order differs.");
                    if (compareOam) CompareMapMenuOamRom(rom, context + $" update={update}");
                    if (comparePixels) CompareMapBackgroundPixelsRom(rom, ref lastBackground, context + $" update={update}");
                    if (compareFramePixels) CompareMapFramePixelsRom(rom, ref lastFrameBackground, ref lastFrameCommands,
                        context + $" frame update={update}");
                }
                void Step(int count = 1, int pressed = 0)
                {
                    int edge = pressed;
                    StepGameplayUpdates(count, Vector2.Right, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                    {
                        rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622)); edge = 0; update++;
                        Compare();
                    });
                }
                Compare();
                Step(1, 0x80); Step(expectedFloors * 10 + 2);
                Step(1, 0x40); Step(expectedFloors * 10 + 2);
                _mapMenu.CloseImmediatelyForValidation();
                _mapMenu.OpenImmediatelyForValidation(); rom.OpenImmediately(2);
                sounds.Clear(); Compare(); Step(3);
                if (compareOam) CloseMapMenuOamRom(rom, batched, context, compareFramePixels);
                else _mapMenu.CloseImmediatelyForValidation();
                cases++;
            }
        }
        // All source floors/equipment combinations plus the first layout's
        // floors repeated with multiple updates in one host frame.
        int expectedCases = alternate ? 42 : 77;
        FailIf(cases != expectedCases, $"Dungeon map initialization expected {expectedCases} source floor/equipment/batch cases, got {cases}.");
        GD.Print($"Validated {cases} clean-US dungeon map initialization cases: {(alternate ? "three alternate layouts in both linked modes" : "ten ordinary/final layouts")}, native tileset selection, all source floors, map/compass combinations, sparse/all visits, chest flags, key counts, full tile commands, scrolling, close/reopen and fixed-update batching.");
    }
}
