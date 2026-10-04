using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDungeonMapRom()
    {
        int hostCase1 = 0;
        foreach (int equipment in new[] { 0, 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wDungeonMaps, (byte)((equipment & 1) != 0 ? 4 : 0));
            save.WriteWramByte(WramAddress.wDungeonCompasses, (byte)((equipment & 2) != 0 ? 4 : 0));
            InitializeTransientSession(save);
            var application = new ApplicationValidationFixture(this);
            LoadValidationRoom(4, 0x46);
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.LoadDungeon(2);
            _mapMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(2);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(count, Vector2.Right, MenuRomActions(held ?? pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, held ?? pressed, _saveData.ReadWramByte(0xc622));
                    edge = 0;
                    update++;
                    FailIf(_mapScreen.Mode != MapMode.Dungeon ||
                        _mapScreen.DisplayedDungeonFloor != rom[0xcc40] - 1 - rom[0xcbb7] ||
                        _mapScreen.DungeonScrollY != rom[0xcbb8] || _mapScreen.IsScrolling != (rom[0xcbce] != 0),
                        $"Dungeon $02 equipment={equipment} update {update}: floor/scroll runtime={_mapScreen.DisplayedDungeonFloor}/{_mapScreen.DungeonScrollY}/{_mapScreen.IsScrolling}, ROM={rom[0xcc40] - 1 - rom[0xcbb7]}/{rom[0xcbb8]}/{rom[0xcbce]}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Dungeon $02 update {update}: direction/scroll sounds differ from ROM.");
                });
            }
            FailIf(_mapScreen.DisplayedDungeonFloor != 1 || rom[0xcbb7] != 0,
                "Dungeon $02:$46 must begin on top floor 1, source display index $00.");
            Step(1, 0x30); // Horizontal keys are consumed but do not scroll.
            Step(1, 0xc0); // Down takes priority over Up.
            Step(10, 0x40); // Scrolling blocks a new direction edge.
            Step(); // Zero update ends the scroll without another pixel.
            Step(1, 0x40);
            Step(11);
            Step(1, 0x80);
            Step(44, held: 0x80);
            _mapMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated clean-US dungeon $02 native layout/floor initialization, map/compass/visit gates, Down/Up priority, blocked scrolling input and ten-pixel/eleven-update floor changes in split/batched updates.");
    }
}
