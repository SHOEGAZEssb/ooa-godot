using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePatternKeyPlacementRom(bool blockPatterns = false)
    {
        // mainData places each controller unconditionally. ROOMFLAG_ITEM is
        // checked by its first interaction dispatch, after parsing all rows.
        var cases = blockPatterns ? new[] { (Room:0x64,Sub:0x09),(Room:0x61,Sub:0x0e) } :
            new[] { (Room:0x7b,Sub:0x10),(Room:0x2e,Sub:0x01),(Room:0x42,Sub:0x05) };
        foreach (var test in cases)
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4,test.Room,OracleSaveData.RoomFlagItem);
            LoadValidationRoom(4,test.Room);
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,24,24);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var frontend = SomariaPrivate<FrontendRom>(rom,"_rom");
            rom[0xcc05] = 0xff; rom[0xcd00] = 4; frontend.ApplyRoomTileSubstitutions();
            rom[0xcd00] = 8; frontend.Call(0x55b7,0x12);
            int CountNative() => Enumerable.Range(0xd2,14).Count(page =>
                rom[page*256+0x40] != 0 && rom[page*256+0x41] == 0x21 && rom[page*256+0x42] == test.Sub);
            int CountSource() => blockPatterns ? _entities.Entities<DungeonTilePatternFallingKeyRoomEntity>().Count +
                _entities.Entities<MoonlitGrottoFallingKeyRoomEntity>().Count : _entities.Entities<DungeonPatternKeyRoomEntity>().Count;
            FailIf(CountNative() != 1 || CountSource() != 1,
                $"Original collected key$4:${test.Room:x2}/$21:${test.Sub:x2} must still allocate before its first dispatch.");
            if (test.Room == 0x64)
            {
                var controller = _entities.Entities<DungeonTilePatternFallingKeyRoomEntity>().Single();
                int address = (0xd0+_entities.InteractionSlot(controller))*256+0x40;
                FailIf(rom[address] != 1 || rom[address+1] != 0x21 || rom[address+2] != 9 ||
                    rom[address+0xb] != 0x68 || rom[address+0xd] != 0xb8,
                    "Original room$04:$64 must preserve its single controller's physical slot and exact placement.");
            }
            // Other parsed producers and RNG/slot counts are excluded here;
            // the native per-controller capacity/retry fixture is separate.
            _entities.Update(1.0/60.0,_player); rom.AdvanceInteractions(_entities.FrameCounter);
            FailIf(CountNative() != 0 || CountSource() != 0,
                "Collected pattern controller must delete on its first pass, without spawning a treasure.");
        }
    }
}
