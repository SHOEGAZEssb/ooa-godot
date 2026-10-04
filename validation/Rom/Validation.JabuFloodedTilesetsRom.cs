using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuFloodedTilesetsRom()
    {
        var rom = new FrontendRom();
        int comparisons = 0, flooded = 0, sideView = 0;
        foreach (int group in new[] { 5, 7 })
        for (int room = 0; room < 256; room++)
        {
            if (!_world.HasRoom(group, room) || _world.GetDungeonIndex(group, room) != 7) continue;
            // Revisit each variant in reverse order as well: cached room
            // instances must follow the current authoritative save byte.
            foreach (int waterLevel in new[] { 0x20, 0x21, 0x22, 0x82, 0x81, 0x80 })
            {
                _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, (byte)waterLevel);
                OracleRoomData actual = _rooms.GetRoom(group, room);
                rom[0xcc2d] = (byte)group; rom[0xcc30] = (byte)room;
                rom[0xc6e9] = (byte)waterLevel;
                rom[0xcc39] = rom[0xcc3b] = 0xff;
                rom.LoadRoomTileset();
                FailIf(actual.TilesetId != (rom[0xff8d] & 0x7f) || actual.TilesetFlags != rom[0xcc34] ||
                    actual.ActiveCollisions != rom[0xcc33] || actual.AnimationGroup != rom[0xcd25],
                    $"Jabu tileset {group:x1}:{room:x2}, water=${waterLevel:x2}: runtime=${actual.TilesetId:x2}/${actual.TilesetFlags:x2}/{actual.ActiveCollisions}/{actual.AnimationGroup}, native=${rom[0xff8d] & 0x7f:x2}/${rom[0xcc34]:x2}/{rom[0xcc33]}/{rom[0xcd25]}.");
                bool scrolling = (actual.TilesetFlags & 0x20) != 0;
                if (scrolling)
                {
                    sideView++;
                    FailIf(rom[0xcc3b] != 0xff || actual.TilesetId != _world.GetTilesetId(group, room),
                        "Jabu side-view tilesets must skip flood-floor lookup and the tileset increment.");
                }
                else
                {
                    FailIf(!_rooms.DungeonMaps.GetDungeon(7).TryGetRoom(room, out DungeonCell cell) ||
                        cell.Floor != rom[0xcc3b], $"Jabu {group:x1}:{room:x2}: native floor search differs.");
                    bool expectedFlood = (waterLevel & 7) == 1 && cell.Floor == 0 ||
                        (waterLevel & 7) == 2 && cell.Floor <= 1;
                    FailIf(((actual.TilesetFlags & 0x40) != 0) != expectedFlood ||
                        actual.TilesetId != (expectedFlood ? 0x3f : 0x3e),
                        $"Jabu {group:x1}:{room:x2}: source $00/$01/$03 floor masks must select tileset $3e/$3f.");
                    if (expectedFlood) flooded++;
                }
                comparisons++;
            }
        }
        FailIf(comparisons == 0 || flooded == 0 || sideView == 0,
            "Jabu flood regression did not execute flooded, dry and side-view branches.");
        _saveData.WriteWramByte(WramAddress.wJabuWaterLevel, 0x23);
        bool rejected = false;
        try { _rooms.GetRoom(5, 0x4c); }
        catch (NotSupportedException error) { rejected = error.Message.Contains("@checkJabuFlooded") && error.Message.Contains("$03"); }
        FailIf(!rejected, "Unsupported Jabu water level $03 must retain its source-aware diagnostic.");
        Godot.GD.Print($"Validated {comparisons} native Jabu tileset loads, flooded/dry floor masks, side-view skips, era flags and cache reuse; unsupported water-level diagnostics.");
    }
}
