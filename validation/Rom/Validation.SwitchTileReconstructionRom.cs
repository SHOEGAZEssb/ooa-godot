using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareSwitchTileReconstructionRom()
    {
        static Vector2 Point(int packed) => new((packed&15)*16+8,(packed>>4)*16+8);
        // Independent Ages branch rows, including replacements that have no
        // PART_SWITCH or INTERAC$78 owner at the written position.
        var rows = new (int Room,int Mask,int Position,int Tile)[] {
            (0x2f,2,0x79,0x0b),(0x2f,2,0x6c,0x5a),(0x3b,0x20,0x79,0xaf),
            (0x4c,1,0x38,0x0b),(0x4e,2,0x68,0x0b),(0x53,4,0x6a,0x0b),
            (0x72,1,0x8d,0xaf),(0x89,4,0x62,0x0b),(0x89,4,0x67,0x5d),
            (0x8f,8,0x81,0x0b),(0x8f,8,0x52,0x5e),(0xc7,1,0x68,0x0b)
        };
        ReinitializeGameplayForValidation();
        foreach (int group in new[] { 4,5 })
        foreach (int roomId in rows.Select(row => row.Room).Distinct())
        {
            int mask = rows.First(row => row.Room == roomId).Mask;
            foreach (byte state in new byte[] { 0,(byte)mask,0x80,(byte)(0x80|mask),0xff })
            {
                _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,state);
                // RoomSession.GetRoom does not construct or allocate actors.
                var room = _rooms.GetRoom(group,roomId);
                var rom = new FrontendRom();
                for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = _saveData.ReadWramByte(address);
                rom[0xcc2d] = (byte)group; rom[0xcc30] = (byte)roomId;
                rom[0xcc05] = 0xff; rom[0xcd00] = 1; rom[0xcdd3] = state;
                rom.LoadRoomTileset();
                for (int p = 0; p < 0xb0; p++)
                    rom[0xcf00+p] = room.GetOriginalMetatile(Point(p));
                rom.ApplyRoomTileSubstitutions();
                foreach (var row in rows.Where(row => row.Room == roomId))
                {
                    byte tile = room.GetMetatile(Point(row.Position));
                    FailIf(tile != rom[0xcf00+row.Position] || group == 4 && (state&row.Mask) != 0 && tile != row.Tile,
                        $"replaceSwitchTiles room${group:x1}:${roomId:x2}, state${state:x2}, position${row.Position:x2}: runtime${tile:x2}, ROM${rom[0xcf00+row.Position]:x2}; restoration must precede object allocation.");
                }
            }
        }
    }
}
