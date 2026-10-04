using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuWaterTilesRom()
    {
        ReinitializeGameplayForValidation();
        int comparisons = 0, platforms = 0;
        foreach (int group in new[] { 5, 7 })
        for (int roomId = 0; roomId < 256; roomId++)
        {
            if (!_world.HasRoom(group, roomId) || _world.GetDungeonIndex(group, roomId) != 7) continue;
            foreach (int waterLevel in new[] { 0x20, 0x21, 0x22, 0x82, 0x81, 0x80 })
            foreach (int flags in group == 5 && new[] { 0x4c, 0x4d, 0x5c, 0x5d, 0x71, 0x72 }.Contains(roomId)
                ? new[] { 0, 0x8f } : new[] { 0 })
            {
                _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, (byte)waterLevel);
                _saveData.SetRoomFlag(group, roomId, 0xff, false);
                _saveData.SetRoomFlag(group, roomId, (byte)flags);
                OracleRoomData actual = _rooms.GetRoom(group, roomId);
                var rom = new FrontendRom();
                for (int address = 0xc5b0; address < 0xcb00; address++) rom[address] = _saveData.ReadWramByte(address);
                rom[0xcc2d] = (byte)group; rom[0xcc30] = (byte)roomId;
                rom[0xcc39] = rom[0xcc3b] = 0xff; rom.LoadRoomTileset();
                byte[] original = (byte[])typeof(OracleRoomData).GetField("_originalLayout",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actual)!;
                int stride = actual.WidthInTiles == 15 ? 16 : actual.WidthInTiles;
                for (int y = 0; y < actual.HeightInTiles; y++)
                for (int x = 0; x < stride; x++) rom[0xcf00 + y * 16 + x] = original[y * stride + x];
                rom.ApplyRoomTileSubstitutions();
                for (int y = 0; y < actual.HeightInTiles; y++)
                for (int x = 0; x < stride; x++)
                    FailIf(actual.Layout[y * stride + x] != rom[0xcf00 + y * 16 + x],
                        $"Jabu {group:x1}:{roomId:x2} water=${waterLevel:x2}, flags=${flags:x2}, tile=${y * 16 + x:x2}: runtime=${actual.Layout[y * stride + x]:x2}, native=${rom[0xcf00 + y * 16 + x]:x2}.");
                // Independent expectations establish the fill/draw dimensions,
                // the lower staircase override, and distinct upper-room content.
                if (group == 5 && roomId is 0x4c or 0x4d && (waterLevel & 7) != 0)
                {
                    int origin = roomId == 0x4c ? 0x35 : 0x12;
                    for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 5; x++)
                    {
                        int position = origin + y * 16 + x;
                        byte expected = roomId == 0x4c && position == 0x57 ? (byte)0x45 : (byte)0xa2;
                        FailIf(actual.Layout[position] != expected,
                            $"Jabu lower platform {roomId:x2}:${position:x2} must fill $a2 then apply the $57/$45 stair override.");
                    }
                    platforms++;
                }
                if (group == 5 && roomId is 0x5c or 0x5d or 0x71 or 0x72 &&
                    (waterLevel & 7) == rom[0xcc3b])
                {
                    bool first = roomId is 0x5c or 0x71;
                    int origin = first ? 0x35 : 0x12;
                    FailIf(actual.Layout[origin] != 0xc5 || actual.Layout[origin + 4] != 0xc6 ||
                        actual.Layout[origin + 0x40] != 0xc7 || actual.Layout[origin + 0x44] != 0xc8 ||
                        actual.Layout[origin + 0x22] != (first ? 0x10 : 0xdb),
                        $"Jabu upper platform {roomId:x2} lost its five-by-five border or distinct central tile.");
                    platforms++;
                }
                comparisons++;
            }
        }
        FailIf(comparisons == 0 || platforms == 0, "Jabu reconstruction did not reach both platform forms.");
        GD.Print($"Validated {comparisons} clean-US Jabu room reconstruction passes, all mapped headers, water/floor gates, group dispatch, flag precedence, platform rectangles/staircase, padding and cache restoration ({platforms} platform passes).");
    }

    private void ValidateJabuWaterTilePairsRom()
    {
        ReinitializeGameplayForValidation();
        var tiles = new JabuWaterTileDatabase();
        // Exercise every source pair and neighboring tiles independently from
        // the real layouts' distribution. The native routine receives the same
        // synthetic floor; expected replacement values are source literals.
        foreach (int water in new[] { 0x20, 0x21, 0x22, 0x80, 0x81, 0x82 })
        foreach (int roomId in new[] { 0x4c, 0x5c, 0x71 })
        {
            _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, (byte)water);
            OracleRoomData room = _rooms.GetRoom(5, roomId);
            FailIf(!_rooms.DungeonMaps.GetDungeon(7).TryGetRoom(roomId, out DungeonCell cell),
                "Jabu source-pair fixture has no native floor.");
            var rom = new FrontendRom();
            rom[0xcc39] = 7; rom[0xcc34] = room.TilesetFlags; rom[0xcc3b] = (byte)cell.Floor;
            rom[0xc6e9] = (byte)water;
            for (int y = 0; y < room.HeightInTiles; y++)
            for (int x = 0; x < room.WidthInTiles; x++)
            {
                int position = y * 16 + x;
                byte input = (byte)((position * 17 + x) & 0xff);
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), input, null, 0);
                rom[0xcf00 + position] = input;
            }
            byte[] probes = [0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0x47, 0x48, 0x49, 0x4a, 0x4b, 0x4c];
            for (int x = 0; x < probes.Length; x++)
            {
                room.SetPositionTileAndCollision(new(x * 16 + 8, 8), probes[x], null, 0);
                rom[0xcf00 + x] = probes[x];
            }
            rom.ApplyJabuTileSubstitutions();
            tiles.Apply(5, 7, cell.Floor, room, _saveData, 0);
            for (int y = 0; y < room.HeightInTiles; y++)
            for (int x = 0; x < room.WidthInTiles; x++)
                FailIf(room.Layout[y * 16 + x] != rom[0xcf00 + y * 16 + x],
                    $"Jabu replacement room=${roomId:x2}, water=${water:x2}, tile=${y * 16 + x:x2} differs from native replaceTiles.");
            for (int x = 0; x < probes.Length; x++)
            {
                byte expected = probes[x];
                if ((water & 7) == cell.Floor)
                    expected = expected is >= 0xf3 and <= 0xf7 ? (byte)0xfa :
                        expected is >= 0x48 and <= 0x4b ? (byte)0xfc : expected;
                FailIf(room.Layout[x] != expected,
                    $"Jabu source-pair boundary ${probes[x]:x2} must select ${expected:x2} at water=${water:x2}, floor=${cell.Floor:x2}.");
            }
        }
        GD.Print("Validated all nine native Jabu hole/warp-hole replacement pairs, unchanged neighboring tiles, complete playable buffers and first-dry-floor gates.");
    }
}
