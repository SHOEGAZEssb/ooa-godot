using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{

    private void RunDungeonMapRetainedMinimapRom(bool compareOam, bool comparePixels = false, bool compareFramePixels = false)
    {
        int cases = 0;
        int hostCase1 = 0;
        foreach (int equipment in Enumerable.Range(0, 4))
        foreach (bool finalBattle in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            int index = finalBattle ? 0x0a : 2, group = finalBattle ? 5 : 4;
            int currentRoom = finalBattle ? 0xf5 : 0x27;
            DungeonInfo info = _rooms.DungeonMaps.GetDungeon(index);
            var cells = info.Cells.Where(cell => cell.Room != 0 &&
                info.TryGetRoom(cell.Room, out var first) && first.Floor == cell.Floor &&
                _rooms.World.HasRoom(group, cell.Room) && _rooms.World.GetDungeonIndex(group, cell.Room) == index &&
                (_rooms.World.LoadRoom(group, cell.Room).TilesetFlags & 0x38) == 0x08)
                .GroupBy(cell => cell.Floor).SelectMany(floor =>
                {
                    var ordered = floor.OrderBy(cell => cell.Y * 8 + cell.X).ToArray();
                    return new[] { ordered[0], ordered[ordered.Length / 2], ordered[^1] };
                }).ToArray();
            FailIf(cells.Length != (finalBattle ? 3 : 6), "Retained map fixture missed a source dungeon floor.");
            foreach (DungeonCell remembered in cells)
            {
                var save = OracleSaveData.CreateStandardGame();
                save.WriteWramByte(0xc686 + index / 8, (byte)((equipment & 1) != 0 ? 1 << (index & 7) : 0));
                save.WriteWramByte(0xc684 + index / 8, (byte)((equipment & 2) != 0 ? 1 << (index & 7) : 0));
                InitializeTransientSession(save);
                LoadValidationRoom(group, remembered.Room); _entities.Clear();
                var preceding = new MenuRom(_saveData, _currentRoom); preceding.LoadDungeon(index);
                FailIf(_saveData.MinimapDungeonPosition != preceding[0xcc3a] ||
                    _saveData.MinimapDungeonFloor != preceding[0xcc3b],
                    "Remembered top-down minimap position differs from independent native room search.");
                int retainedPosition = _saveData.MinimapDungeonPosition, retainedFloor = _saveData.MinimapDungeonFloor;
                LoadValidationRoom(group, currentRoom); _entities.Clear();
                if (!finalBattle)
                    FailIf((_currentRoom.TilesetFlags & 0x20) == 0 ||
                        _saveData.MinimapDungeonPosition != retainedPosition || _saveData.MinimapDungeonFloor != retainedFloor,
                        "Side-view room $4:$27 replaced its preceding top-down minimap cell/floor.");
                var rom = new MenuRom(_saveData, _currentRoom); rom.LoadDungeon(index);
                if (comparePixels || compareFramePixels) rom.LoadHudGraphics();
                _mapMenu.OpenImmediatelyForValidation(); rom.OpenImmediately(2);
                var sounds = _sound.AttachPlayRequestAudit();
                string context = $"Retained map ${group:x1}:${currentRoom:x2} from ${remembered.Room:x2} equipment={equipment} batch={batched}";
                int update = 0;
                Texture2D? lastBackground = null;
                Texture2D? lastFrameBackground = null;
                byte[]? lastFrameCommands = null;
                void Compare()
                {
                    int cursor = rom[0xcbbb];
                    FailIf(finalBattle && cursor != 0x13, context + ": native Ganon map cursor missed literal $13 override.");
                    FailIf(_mapScreen.Mode != MapMode.Dungeon ||
                        _mapScreen.DisplayedDungeonFloor != rom[0xcc40] - 1 - rom[0xcbb7] ||
                        _mapScreen.DungeonScrollY != rom[0xcbb8] || _mapScreen.IsScrolling != (rom[0xcbce] != 0) ||
                        _mapScreen.DungeonLinkIconPosition != new Vector2(80 + (cursor & 7) * 8, 32 + (cursor >> 3) * 8),
                        context + $" update={update}: retained floor/scroll/cursor/icon position differs.");
                    bool flicker = (bool)typeof(MapScreen).GetField("_dungeonFlicker", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_mapScreen)!;
                    FailIf(flicker != (rom[0xcbb9] != 0), context + $" update={update}: retained dungeon flicker counter differs.");
                    for (int y = 0; y < 18; y++)
                    for (int x = 0; x < 20; x++)
                        FailIf(_mapScreen.DungeonScreenTileAt(x, y) != rom.MapTile(x, y),
                            context + $" update={update}: dungeon tile ({x},{y}) differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": direction sound order differs.");
                    if (compareOam) CompareMapMenuOamRom(rom, context + $" update={update}");
                    if (comparePixels) CompareMapBackgroundPixelsRom(rom, ref lastBackground, context + $" update={update}");
                    if (compareFramePixels) CompareMapFramePixelsRom(rom, ref lastFrameBackground, ref lastFrameCommands,
                        context + $" frame update={update}");
                }
                void Step(int count = 1, int pressed = 0)
                {
                    int edge = pressed;
                    StepGameplayUpdates(count, Vector2.Left, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                    {
                        rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622)); edge = 0; update++;
                        Compare();
                    });
                }
                Compare(); Step(65);
                Step(1, 0x80); Step(22);
                Step(1, 0x40); Step(22);
                _mapMenu.CloseImmediatelyForValidation();
                _mapMenu.OpenImmediatelyForValidation(); rom.OpenImmediately(2); sounds.Clear();
                Compare(); Step(33);
                if (compareOam) CloseMapMenuOamRom(rom, batched, context, compareFramePixels);
                else _mapMenu.CloseImmediatelyForValidation();
                cases++;
            }
        }
        FailIf(cases != 42, $"Expected 42 retained side-view/Ganon map cases, got {cases}.");
        GD.Print($"Validated {cases} clean-US side-view retained top-down minimap cells/floors and Ganon $13 override, map/compass gates, all screen tiles, icon/flicker/scroll state, sounds and reopening through split/batched gameplay.");
    }
}
