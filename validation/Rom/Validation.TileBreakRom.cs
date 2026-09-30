using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTileMutationRom()
    {
        LoadValidationRoom(0, 0x33); _entities.Clear();
        var database = new BreakableTileDatabase();
        var rom = new TileBreakRom();
        Vector2 point = new(72, 72);
        int cases = 0;
        for (int tile = 0; tile < 256; tile++)
        for (int source = 0; source < 20; source++)
        {
            if (!database.TryGet(0, tile, out var record) || !record.AllowsSource(source)) continue;
            var save = OracleSaveData.CreateStandardGame();
            _currentRoom.SetPositionTileAndCollision(point, (byte)tile, null, 0);
            _currentRoom.SetUnderlyingMetatile(point, 0x3a);
            rom.Reset(save); rom[0xcc30] = 0x33;
            rom.SetTile(0, tile, 0x3a, _currentRoom);
            bool expected = rom.Break(source);
            var actual = database.TryBreak(_currentRoom, source, point, save, 0, () => 0, null, out var result);
            FailIf(!expected || actual != BreakableTileBreakStatus.Broken || _currentRoom.GetMetatile(point) != rom[0xcf44] ||
                _currentRoom.GetTerrainInfo(point).Collision != rom[0xce44] || _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying,
                $"ROM tile mutation tile=${tile:x2}, source=${source:x2}: native tile/collision/underlying=${rom[0xcf44]:x2}/${rom[0xce44]:x2}/${rom.Underlying:x2}, runtime=${_currentRoom.GetMetatile(point):x2}/${_currentRoom.GetTerrainInfo(point).Collision:x2}/${_currentRoom.GetUnderlyingMetatile(point):x2}.");
            for (int address = 0xc5b0; address < 0xcb00; address++)
                FailIf(save.ReadWramByte(address) != rom[address],
                    $"ROM tile=${tile:x2}, source=${source:x2}: persistent byte ${address:x4}, native=${rom[address]:x2}, runtime=${save.ReadWramByte(address):x2}.");
            if (tile != 0xda)
                FailIf(result.Record.Drop != rom[0xff8d] || result.Record.Effect != rom[0xff8e],
                    $"ROM tile=${tile:x2} break effect/drop metadata differs.");
            cases++;
        }
        GD.Print($"Validated {cases} ROM tile mutations, collision replacements, underlying layout and entire save-image side effects.");
        foreach (byte count in new byte[] { 0xfe, 0xff })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc626, count);
            _currentRoom.SetPositionTileAndCollision(point, 0xf2, null, 0);
            rom.Reset(save); rom.SetTile(0, 0xf2, room: _currentRoom);
            rom.Break(2);
            database.TryBreak(_currentRoom, 2, point, save, 0, () => 0, null, out _);
            FailIf(save.ReadWramByte(0xc626) != rom[0xc626], "ROM sign counter saturation differs.");
            bool repeated = rom.Break(2);
            var status = database.TryBreak(_currentRoom, 2, point, save, 0, () => 0, null, out _);
            FailIf(repeated || status == BreakableTileBreakStatus.Broken || save.ReadWramByte(0xc626) != rom[0xc626],
                "A repeated sign hit must not repeat its persistent side effects.");
        }
        LoadValidationRoom(4, 0xa8); _entities.Clear();
        for (int underlying = 0; underlying < 256; underlying++)
        {
            var save = OracleSaveData.CreateStandardGame();
            _currentRoom.SetPositionTileAndCollision(point, 0x10, null, 0);
            _currentRoom.SetUnderlyingMetatile(point, (byte)underlying);
            rom.Reset(save); rom.SetTile(_currentRoom.ActiveCollisions, 0x10, underlying, _currentRoom);
            bool native = rom.Break(0);
            var status = database.TryBreak(_currentRoom, 0, point, save, 4, () => 0, null, out _);
            FailIf(!native || status != BreakableTileBreakStatus.Broken || _currentRoom.GetMetatile(point) != rom[0xcf44] ||
                _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying || _currentRoom.GetTerrainInfo(point).Collision != rom[0xce44],
                $"ROM moving-pot restoration differs for live underlying tile ${underlying:x2}.");
        }
        // loadRoom's standard substitutions use ROOMFLAG $80. Compare the
        // saved input with executed breaking, then exercise live room re-entry.
        foreach (byte tile in new byte[] { 0xc5, 0xcf })
        {
            var save = OracleSaveData.CreateStandardGame();
            var rooms = new RoomSession(0, 0x33, () => 0, () => { }, save);
            bool found = false;
            for (int roomId = 0; roomId < 256 && !found; roomId++)
            {
                if (!rooms.World.HasRoom(0, roomId)) continue;
                var room = rooms.Load(0, roomId);
                if (room.ActiveCollisions != 0) continue;
                for (int y = 8; y < room.Height && !found; y += 16)
                for (int x = 8; x < room.Width && !found; x += 16)
                {
                    Vector2 position = new(x, y);
                    if (room.GetMetatile(position) != tile) continue;
                    found = true;
                    int source = tile == 0xc5 ? 1 : 0x0c;
                    rom.Reset(save); rom[0xcc30] = (byte)roomId;
                    rom.SetTile(0, tile, tile, room); rom.Break(source);
                    database.TryBreak(room, source, position, save, 0, () => 0, null, out _);
                    for (int address = 0xc5b0; address < 0xcb00; address++)
                        FailIf(save.ReadWramByte(address) != rom[address], $"ROM re-entry save input differs at ${address:x4}.");
                    rooms.Load(0, roomId == 0x33 ? 0x34 : 0x33);
                    var reentered = rooms.Load(0, roomId);
                    byte expected = tile == 0xcf && (rom[0xc700 + roomId] & 0x80) != 0 ? rom[0xcf44] : tile;
                    FailIf(reentered.GetMetatile(position) != expected,
                        $"Room 0:${roomId:x2} did not reapply ROM-derived break persistence for tile ${tile:x2}.");
                }
            }
            FailIf(!found, $"No real overworld fixture found for tile ${tile:x2} re-entry.");
        }
    }

    private void ValidateBreakableSourceMasksRom()
    {
        var rom = new TileBreakRom();
        var database = new BreakableTileDatabase();
        var save = OracleSaveData.CreateStandardGame();
        for (int collision = 0; collision < 6; collision++)
        for (int tile = 0; tile < 256; tile++)
        for (int source = 0; source < 20; source++)
        {
            rom.Reset(save); rom.SetTile(collision, tile);
            bool native = rom.Break(source, query: true);
            bool actual = database.TryGet(collision, tile, out var record) && record.AllowsSource(source);
            FailIf(actual != native, $"ROM breakability collision=${collision:x2}, tile=${tile:x2}, source=${source:x2}: native={native}, runtime={actual}.");
            FailIf(rom[0xcf44] != tile || rom.RandomCalls != 0 || rom[0xcce0] != 0,
                "A native breakability query must not mutate tiles, consume RNG or enqueue graphics.");
        }
        GD.Print("Validated all 30,720 tile/source/collision-set breakability combinations against the ROM.");
    }

    private void ValidateDropSelectionRom()
    {
        var rom = new TileBreakRom();
        var database = new ItemDropDatabase();
        int cases = 0;
        for (int ownership = 0; ownership < 3; ownership++)
        {
            var save = OracleSaveData.CreateStandardGame();
            var inventory = new InventoryState(_treasures, save);
            foreach (int treasure in new[] { 3, 0x20, 0x21, 0x22, 0x23, 0x24 })
            {
                inventory.LoseTreasure(treasure);
                if (ownership == 2 || ownership == 1 && (treasure & 1) == 0) inventory.GiveTreasure(treasure, 0);
            }
            for (int index = 0; index < 144; index++)
            for (int seedIndex = 0; seedIndex < 512; seedIndex++)
            {
                int seed = (seedIndex * 129 + 0x1234) & 0xffff;
                rom.Reset(save, seed);
                var random = new OracleRandom();
                random.RestoreState(random.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
                long before = random.CaptureState().Calls;
                int? expected = rom.DecideDrop(index);
                int? actual = database.DecideDrop(index, random, inventory, save);
                var after = random.CaptureState();
                FailIf(actual != expected || after.Rng1 != rom[0xff94] || after.Rng2 != rom[0xff95] || after.Calls - before != rom.RandomCalls,
                    $"ROM drop index=${index:x2}, seed=${seed:x4}, ownership={ownership}: native={expected}, runtime={actual}, calls={rom.RandomCalls}/{after.Calls - before}.");
                cases++;
            }
        }
        GD.Print($"Validated {cases} ROM drop decisions, ownership gates and exact shared RNG consumption across every enemy/breakable table entry.");
    }
}
