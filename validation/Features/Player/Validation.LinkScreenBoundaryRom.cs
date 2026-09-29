using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkScreenBoundaryRom()
    {
        foreach (bool disabled in new[] { false, true })
        for (int direction = 0; direction < 4; direction++)
        for (int offset = -1; offset <= 1; offset++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            var room = _rooms.CurrentRoom;
            var rom = new LinkCollisionRom();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0, 0);
                rom[0xcf00 + y * 16 + x] = 0x2c;
            }
            Vector2 input = direction switch { 0 => Vector2.Up, 1 => Vector2.Right, 2 => Vector2.Down, _ => Vector2.Left };
            Vector2 start = direction switch
            {
                0 => new(80.5f, 6.5f + offset),
                1 => new(153.5f + offset, 64.5f),
                2 => new(80.5f, 120.5f + offset),
                _ => new(6.5f + offset, 64.5f)
            };
            _player.WarpTo(start);
            _transitions.ScreenTransitionsDisabledSource = () => disabled;
            rom[0xd000] = 1;
            rom[0xd004] = 1;
            rom[0xd009] = (byte)(direction * 8);
            rom[0xcc2b] = (byte)(direction * 8); // wLinkAngle, sampled separately by the boundary gate
            rom.Word(0xd00a, (int)(start.Y * 256));
            rom.Word(0xd00c, (int)(start.X * 256));
            rom[0xcc30] = 0x33;
            rom[0xcc33] = (byte)room.ActiveCollisions;
            rom[0xcc34] = (byte)room.TilesetFlags;
            rom[0xcc91] = (byte)(disabled ? 1 : 0);
            rom[0xcd0c] = 154;
            rom[0xcd0d] = 121;
            rom[0xcd04] = 2;
            rom.Call(LinkCollisionRom.Probe);
            rom.Call(LinkCollisionRom.Move, 0x28, direction * 8);
            rom.Call(LinkCollisionRom.ScreenBoundary, bank: 1);
            StepGameplayUpdates(1, input);
            Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
            FailIf(_player.PrecisePosition != expected || _transitions.IsTransitioning != (rom[0xcd04] == 3),
                $"ROM screen boundary direction={direction}, offset={offset}, disabled={disabled}: ROM={expected}/state=${rom[0xcd04]:x2}, runtime={_player.PrecisePosition}/transition={_transitions.IsTransitioning}.");
            FailIf(rom[0xcd04] == 3 && rom[0xcd02] != direction,
                "Native screen boundary selected an unexpected transition direction.");
        }
        GD.Print("Validated ROM room-edge thresholds, fractional clamps and disabled-transition gates in all four directions through gameplay updates.");
    }
}
