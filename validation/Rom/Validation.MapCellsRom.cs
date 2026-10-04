using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapCellsRom()
    {
        // Each era has 14x14 source text entries. Popup tables are ordered
        // searches with duplicate rooms, and conditional types read other
        // rooms/global flags rather than just the selected room's visit bit.
        int hostCase1 = 0;
        foreach (int group in new[] { 0, 1 })
        foreach (int variant in new[] { 0, 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay();
            LoadValidationRoom(group, 0x45);
            for (int era = 0; era < 2; era++)
            for (int room = 0; room < 256; room++)
            {
                byte flags = (byte)(variant == 0 ? 0 : variant == 1 ? 0x10 : 0x99);
                _saveData.WriteWramByte(0xc700 + era * 256 + room, flags);
            }
            for (int dungeonGroup = 4; dungeonGroup <= 5; dungeonGroup++)
            for (int room = 0; room < 256; room++)
                _saveData.SetRoomFlag(dungeonGroup, room, 0x10, variant >= 2);
            for (int spot = 0; spot < 16; spot++)
                _saveData.SetGashaSpotPlanted(spot, variant >= 2);
            _saveData.SetGlobalFlag(GlobalFlag.MoblinsKeepDestroyed, variant >= 2);
            _saveData.SetGlobalFlag(GlobalFlag.MakuGivesAdviceFromPresentMap, variant >= 2);
            _saveData.SetGlobalFlag(GlobalFlag.MakuGivesAdviceFromPastMap, variant >= 2);
            _saveData.SetMakuMapTextPresent(0x4f);
            _saveData.SetMakuMapTextPast(0xd6);
            _saveData.SetRoomFlag(0, 0xba, 0x40, variant >= 2);
            _saveData.SetRoomFlag(0, 0x90, 0x40, variant == 3);
            var rom = new MenuRom(_saveData, _currentRoom);
            _mapMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(2);
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int pressed = 0, int count = 1)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622));
                    edge = 0;
                    FailIf(_mapScreen.CursorRoom != rom[0xcbb6] ||
                        _mapScreen.PopupPrimary != rom[0xcbbe] ||
                        _mapScreen.PopupAlternate != rom[0xcbc0] ||
                        _mapScreen.PopupSize != rom[0xcbbd],
                        $"Map ${group:x1}:${_mapScreen.CursorRoom:x2} variant={variant}: source popup/search/state differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Map ${group:x1}:${_mapScreen.CursorRoom:x2} variant={variant}: native sound order differs.");
                });
            }
            // Reach every cell through the real menu's direction handler.
            while ((_mapScreen.CursorRoom & 15) != 0) Step(0x20);
            while ((_mapScreen.CursorRoom >> 4) != 0) Step(0x40);
            for (int row = 0; row < 14; row++)
            {
                for (int column = 0; column < 14; column++)
                {
                    int room = row * 16 + (row % 2 == 0 ? column : 13 - column);
                    FailIf(_mapScreen.CursorRoom != room,
                        $"Map exhaustive traversal missed room ${group:x1}:${room:x2}.");
                    bool hasText = _mapScreen.TryGetSelectedAreaText(out MapText text);
                    Step(1);
                    FailIf(hasText != (rom[0xcba0] != 0) ||
                        hasText && text.TextId != ((rom[0xcba3] - 4) << 8 | rom[0xcba2]) ||
                        _dialogue.IsOpen != hasText,
                        $"Map ${group:x1}:${room:x2} variant={variant}: area text runtime=${text.TextId:x4}/{hasText}, native=${rom[0xcba3] - 4:x2}{rom[0xcba2]:x2}/{rom[0xcba0] != 0}.");
                    // This scenario compares dispatch, not the already-tested
                    // standard text thread. Cancel at the declared text boundary.
                    _dialogue.Close();
                    rom[0xcba0] = rom[0xcbae] = 0;
                    Step(count: 2);
                    if (column < 13) Step(row % 2 == 0 ? 0x10 : 0x20);
                }
                if (row < 13) Step(0x80);
            }
            _mapMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated every clean-US present/past map cell's native text/popup dispatch across unvisited, visited, unlocked/advice/planted and final-tower variants through split/batched menu updates.");
    }
}
