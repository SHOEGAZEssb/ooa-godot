using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFeatherAirMovementRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int startAngle in Enumerable.Range(0, 9).Select(i => i == 8 ? 0xff : i * 4))
        foreach (int change in new[] { 0, 8, 16, 0xff })
        foreach (int pegasus in new[] { 0, 0x80, 20 })
        {
            FeatherRom rom = PrepareFeatherRom(fraction: 255 / 256f);
            _runtimeState.SetWramByte(0xcc6c, (byte)pegasus);
            rom.Word(0xcc6c, pegasus);
            int nextAngle = change == 0xff ? 0xff : startAngle == 0xff ? change : (startAngle + change) & 0x1f;
            StepFeatherRom(rom, 1, batched, startAngle, held: true, pressed: true);
            StepFeatherRom(rom, 13, batched, nextAngle);
            FailIf(rom.SpeedZ != -0x20 || rom[0xd009] != startAngle,
                "Feather must retain its takeoff angle through the last rising update, regardless of new input.");
            StepFeatherRom(rom, 1, batched, nextAngle);
            FailIf(rom.SpeedZ != 0,
                "Feather must start descending velocity convergence on update15, when speedZ reaches zero.");
            StepFeatherRom(rom, 16, batched, nextAngle);
            FailIf(rom.Airborne, "Feather steering must not change the update31 landing boundary.");
            StepFeatherRom(rom, 3, batched, nextAngle);
            FailIf(_runtimeState.ReadWramByte(0xcc6c) != rom[0xcc6c] ||
                _runtimeState.ReadWramByte(0xcc6d) != rom[0xcc6d],
                "Feather flight and landing must retain native Pegasus timer ownership.");
        }
        foreach (bool batched in new[] { false, true })
        foreach (int angle in Enumerable.Range(0, 8).Select(i => i * 4))
        foreach (byte collision in new byte[] { 0x0f, 0x03 })
        {
            FeatherRom rom = PrepareFeatherRom(fraction: 0.5f);
            Vector2 input = OracleObjectMovement.Shared.Delta(0x28, angle).Normalized();
            Vector2 wall = _player.Position + input * 24;
            _currentRoom.SetPositionTileAndCollision(wall, 0x20, collision, 0);
            rom.CopyRoom(_currentRoom);
            StepFeatherRom(rom, 1, batched, angle, held: true, pressed: true);
            StepFeatherRom(rom, 30, batched, angle);
            StepFeatherRom(rom, 3, batched, angle);
            FailIf(_currentRoom.IsSolid(_player.Position),
                $"Feather angle=${angle:x2} must land outside its wall's solid collision quarters.");
        }
        GD.Print("Validated executed-ROM Feather rising momentum, descending turns/reversal/release, Pegasus speed/expiration and full/partial wall collision in eight directions through individual/batched gameplay updates.");
    }
}
