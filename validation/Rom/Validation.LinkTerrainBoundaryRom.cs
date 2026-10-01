using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkTerrainBoundaryRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (byte tile in new byte[] { 0xf3, 0xfa })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            var room = _rooms.CurrentRoom;
            var rom = new LinkCollisionRom();
            rom[0xcc33] = (byte)room.ActiveCollisions;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0, 0);
                rom[0xcf00 + y * 16 + x] = 0x2c;
            }
            room.SetPositionTileAndCollision(new(88, 72), tile, 0x10, 0);
            rom[0xcf45] = tile;
            rom[0xce45] = 0x10;
            // Compare the native +5 high-byte foot probe on both sides of
            // every tile edge, including fractions immediately before carry.
            for (int y = 56; y < 84; y++)
            for (int x = 76; x < 100; x++)
            {
                Vector2 position = new(x + 255 / 256.0f, y + 255 / 256.0f);
                rom.Word(0xd00a, (int)(position.Y * 256));
                rom.Word(0xd00c, (int)(position.X * 256));
                rom.Call(LinkCollisionRom.ActiveTile);
                TerrainInfo terrain = room.GetTerrainInfo(position.Floor() + Vector2.Down * 5);
                int expectedType = terrain.Hazard == HazardType.Hole ? 1 : terrain.Hazard == HazardType.Water ? 7 : 0;
                FailIf(rom[0xc200] != expectedType || rom[0xcc9a] != terrain.Tile,
                    $"ROM terrain entry tile=${tile:x2}, XY={position}: native type=${rom[0xc200]:x2}, tile=${rom[0xcc9a]:x2}; runtime={terrain}.");
            }
            _player.WarpTo(new(80.5f, 56.5f));
            rom.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256));
            rom.Word(0xd00c, (int)(_player.PrecisePosition.X * 256));
            rom[0xd004] = 1;
            rom[0xcc99] = 0;
            rom[0xcc9b] = 0;
            rom[0xcc9c] = 0;
            rom[0xcc9d] = 0;
            int updates = 0;
            int angle = 16;
            void CompareEntry()
            {
                rom.Call(LinkCollisionRom.ApplyTile);
                if (rom[0xcc5d] == 0)
                {
                    rom.Call(LinkCollisionRom.Probe);
                    // Select speed natively: holes use the grass column and
                    // suppress Pegasus, rather than an assumed SPEED_100.
                    rom.Call(LinkCollisionRom.StandardSpeed);
                    rom.Call(LinkCollisionRom.Move, rom[0xd010], angle);
                }
                Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                FailIf(_player.PrecisePosition != expected,
                    $"ROM terrain approach tile=${tile:x2}, update={updates}, batched={batched}: ROM={expected}, runtime={_player.PrecisePosition}.");
                // overworldSwimmingState1 consumes the newly set swimming
                // signal this update, enters state 2 and holds position.
                FailIf(tile == 0xfa && _player.TopDownSwimming != (rom[0xcc5d] != 0),
                    $"Water entry update boundary diverged at update {updates}.");
                updates++;
            }
            StepGameplayUpdates(4, Vector2.Down, batched: batched, afterUpdate: CompareEntry);
            FailIf(tile == 0xfa && _player.TopDownSwimmingState != 2,
                "Water entry must reach native swimming state 2 after the +5 probe crosses the edge.");
            if (tile == 0xf3)
            {
                StepGameplayUpdates(3, Vector2.Down, batched: batched, afterUpdate: CompareEntry);
                angle = 0;
                // Hole movement uses SPEED_0c0 while the pull adds one
                // vertical pixel every four updates. Ten updates escape
                // this approach, including the first floor terrain update.
                StepGameplayUpdates(10, Vector2.Up, batched: batched, afterUpdate: CompareEntry);
                FailIf(rom[0xcc9c] != 0, "Hole escape fixture did not return to ordinary floor.");
                angle = 16;
                StepGameplayUpdates(5, Vector2.Down, batched: batched, afterUpdate: CompareEntry);
                FailIf(rom[0xcc9c] != 1, "Hole re-entry fixture did not reach its second entry.");
            }
        }
        GD.Print("Validated ROM water/hole foot-probe boundaries and fractional approaches through individual/batched gameplay updates, first hole pulls and swimming entry.");
    }
}
