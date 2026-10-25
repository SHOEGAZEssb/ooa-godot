using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePollutedWaterTilesRom()
    {
        var tiles = new PollutedWaterTileDatabase();
        int comparisons = 0;
        // Exercise the flag and both outdoor/indoor tileset gates with repeated
        // pollution, unchanged neighbors, and the first/last playable positions.
        foreach (var location in new[] { (1, 0xa3), (3, 0xb1), (3, 0xef), (5, 0x4c) })
        foreach (bool clean in new[] { false, true })
        {
            _saveData.SetGlobalFlag(0x30, clean);
            OracleRoomData room = _rooms.GetRoom(location.Item1, location.Item2);
            var rom = new FrontendRom();
            rom[0xc6d6] = (byte)(clean ? 1 : 0);
            rom[0xcc34] = room.TilesetFlags;
            byte[] inputs = [0xeb, 0xea, 0xeb, 0xec, 0x3a, 0x3b, 0xfc];
            for (int y = 0; y < room.HeightInTiles; y++)
            for (int x = 0; x < room.WidthInTiles; x++)
            {
                byte input = x == 0 && y == 0 || x == room.WidthInTiles - 1 && y == room.HeightInTiles - 1
                    ? (byte)0xeb : inputs[(y * room.WidthInTiles + x) % inputs.Length];
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), input, null, 0);
                rom[0xcf00 + y * 16 + x] = input;
            }
            rom.ApplyPollutedWaterTileSubstitutions();
            tiles.Apply(room, _saveData, 0);
            for (int y = 0; y < room.HeightInTiles; y++)
            for (int x = 0; x < room.WidthInTiles; x++)
            {
                byte input = x == 0 && y == 0 || x == room.WidthInTiles - 1 && y == room.HeightInTiles - 1
                    ? (byte)0xeb : inputs[(y * room.WidthInTiles + x) % inputs.Length];
                byte expected = input == 0xeb && clean && (room.TilesetFlags & 1) != 0
                    ? (byte)((room.TilesetFlags & 0x40) == 0 ? 0xfc : 0x3b) : input;
                FailIf(room.GetMetatile(new(x * 16 + 8, y * 16 + 8)) != expected ||
                    rom[0xcf00 + y * 16 + x] != expected,
                    $"Pollution gate {location.Item1:x}:{location.Item2:x2}/${y * 16 + x:x2}, flags=${room.TilesetFlags:x2}, clean={clean}: expected ${expected:x2} from source $eb->$fc/$3b.");
            }
            comparisons++;
        }

        // Ordinary entry, preload and re-entry rebuild cached source layouts.
        // These real rooms include surface and underwater seas outside the montage.
        foreach (var location in new[] { (1, 0xa3), (1, 0xa5), (1, 0xd2), (2, 0xb6), (3, 0xb1), (3, 0xb0) })
        {
            _saveData.SetGlobalFlag(0x30, false);
            OracleRoomData room = _rooms.GetRoom(location.Item1, location.Item2);
            byte[] polluted = (byte[])room.Layout.Clone();
            byte[] mappings = room.CaptureBackgroundMappings();
            foreach (bool clean in new[] { true, false, true })
            {
                _saveData.SetGlobalFlag(0x30, clean);
                room = _rooms.GetRoom(location.Item1, location.Item2);
                var rom = new FrontendRom();
                for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = _saveData.ReadWramByte(address);
                rom[0xcc2d] = (byte)location.Item1; rom[0xcc30] = (byte)location.Item2;
                rom[0xcc39] = rom[0xcc3b] = 0xff; rom.LoadRoomTileset();
                int stride = room.WidthInTiles == 15 ? 16 : room.WidthInTiles;
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < stride; x++) rom[0xcf00 + y * 16 + x] = polluted[y * stride + x];
                rom.ApplyRoomTileSubstitutions();
                for (int y = 0; y < room.HeightInTiles; y++)
                for (int x = 0; x < stride; x++)
                    FailIf(room.Layout[y * stride + x] != rom[0xcf00 + y * 16 + x],
                        $"Pollution reload {location.Item1:x}:{location.Item2:x2}/${y * 16 + x:x2}, clean={clean} differs from clean-US substitution order.");
                FailIf(!polluted.Contains((byte)0xeb),
                    $"Pollution reload fixture {location.Item1:x}:{location.Item2:x2} must contain original $eb.");
                FailIf(mappings.SequenceEqual(room.CaptureBackgroundMappings()) == clean,
                    $"Pollution reload {location.Item1:x}:{location.Item2:x2} failed to replace/restore its water graphics mappings, clean={clean}.");
                comparisons++;
            }
        }
        GD.Print($"Validated {comparisons} clean-US pollution passes: flag $30, outdoor/underwater gates, $eb->$fc/$3b boundaries, neighboring tiles, real-room reload order, cache restoration and graphics.");
    }
}
