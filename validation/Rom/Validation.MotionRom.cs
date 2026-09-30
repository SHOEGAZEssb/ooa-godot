using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateOracleMovementRom()
    {
        var rom = new ObjectMotionRom();
        OracleObjectMovement movement = OracleObjectMovement.Shared;
        int cases = 0;
        // Every supported speed/direction and every fractional byte, with high
        // bytes straddling unsigned and signed boundaries. Complement X's low
        // byte to exercise independent carry chains. Rotate all object slots.
        foreach (int high in new[] { 0x00, 0x7f, 0x80, 0xff })
        for (int speed = 0; speed <= 0x78; speed += 5)
        for (int angle = 0; angle < 32; angle++)
        for (int fraction = 0; fraction < 256; fraction++)
        {
            int slot = (fraction & 3) * 0x40;
            int obj = ObjectMotionRom.Object + slot;
            var before = new OracleObjectPosition((ushort)((high << 8) | fraction),
                (ushort)(((high ^ 0xff) << 8) | (fraction ^ 0xff)));
            rom[obj + 9] = (byte)angle;
            rom[obj + 0x10] = (byte)speed;
            rom.Word(obj + 0x0a, before.YFixed);
            rom.Word(obj + 0x0c, before.XFixed);
            rom.Call(ObjectMotionRom.ApplySpeed, slot: slot);
            OracleObjectPosition actual = movement.ApplySpeed(before, speed, angle);
            FailIf(actual.YFixed != rom.Word(obj + 0x0a) || actual.XFixed != rom.Word(obj + 0x0c),
                $"objectApplySpeed $00:$201d speed=${speed:x2}, angle=${angle:x2}, " +
                $"YX=${before.YFixed:x4}/${before.XFixed:x4}: ROM=${rom.Word(obj + 0x0a):x4}/${rom.Word(obj + 0x0c):x4}, " +
                $"runtime=${actual.YFixed:x4}/${actual.XFixed:x4}.");
            cases++;
        }
        // Raw, unaligned speed bytes are used by PART $49. Compare their
        // displacements independently with the ROM, including table row crossings.
        for (int speed = 5; speed <= 0x78; speed++)
        for (int angle = 0; angle < 32; angle++)
        {
            int obj = ObjectMotionRom.Object;
            rom[obj + 9] = (byte)angle;
            rom[obj + 0x10] = (byte)speed;
            rom.Word(obj + 0x0a, 0);
            rom.Word(obj + 0x0c, 0);
            rom.Call(ObjectMotionRom.ApplySpeed);
            OracleObjectVelocity velocity = OracleObjectSpeedTable.Shared.GetRaw(speed, angle);
            FailIf(unchecked((ushort)velocity.YFixed) != rom.Word(obj + 0x0a) ||
                unchecked((ushort)velocity.XFixed) != rom.Word(obj + 0x0c),
                $"getPositionOffsetForVelocity $00:$2041 raw speed=${speed:x2}, angle=${angle:x2} diverged.");
            cases++;
        }
        // Exercise the retained Vector2 overload too: fractions must survive
        // repeated calls while the displayed position uses only the high byte.
        for (int speed = 0; speed <= 0x78; speed += 5)
        for (int angle = 0; angle < 32; angle++)
        {
            int obj = ObjectMotionRom.Object;
            rom[obj + 9] = (byte)angle;
            rom[obj + 0x10] = (byte)speed;
            rom.Word(obj + 0x0a, 0x01f0);
            rom.Word(obj + 0x0c, 0xff80);
            Vector2 precise = new(0xff80 / 256.0f, 0x01f0 / 256.0f);
            for (int update = 0; update < 300; update++)
            {
                rom.Call(ObjectMotionRom.ApplySpeed);
                Vector2 pixels = movement.ApplySpeed(ref precise, speed, angle);
                int y = rom.Word(obj + 0x0a), x = rom.Word(obj + 0x0c);
                FailIf(precise != new Vector2(x / 256.0f, y / 256.0f) ||
                    pixels != new Vector2(x >> 8, y >> 8),
                    $"objectApplySpeed $00:$201d repeated speed=${speed:x2}, angle=${angle:x2}, update={update} lost fraction or pixel position.");
                cases++;
            }
        }
        GD.Print($"Validated {cases} clean-ROM movement cases: all supported speed/angle/fraction combinations, wrapping, slots and raw speeds.");
    }

    private static void ValidateOracleAnglesRom()
    {
        var rom = new ObjectMotionRom();
        OracleObjectMovement movement = OracleObjectMovement.Shared;
        int cases = 0;
        // Adjusted $00/$ff corners exhaust every magnitude pair in each sign
        // quadrant. Interior origins exercise byte translation and the $f8 seam.
        (byte Y, byte X)[] origins = [(0xf8, 0xf8), (0xf8, 0xf7), (0xf7, 0xf8), (0xf7, 0xf7),
            (0, 0), (0x40, 0x50), (0x7f, 0x80), (0xff, 0xff)];
        foreach ((byte y, byte x) in origins)
        for (int targetY = 0; targetY < 256; targetY++)
        for (int targetX = 0; targetX < 256; targetX++)
        {
            int slot = (targetX & 3) * 0x40;
            int obj = ObjectMotionRom.Object + slot;
            rom[obj + 0x0b] = y;
            rom[obj + 0x0d] = x;
            rom.Call(ObjectMotionRom.RelativeAngle, targetY: targetY, targetX: targetX, slot: slot);
            int actual = movement.RelativeAngle(y, x, (byte)targetY, (byte)targetX);
            FailIf(actual != rom.Result,
                $"objectGetRelativeAngle $00:$1ea4 YX=${y:x2}/${x:x2}->${targetY:x2}/${targetX:x2}: " +
                $"ROM=${rom.Result:x2}, runtime=${actual:x2}.");
            cases++;
        }
        GD.Print($"Validated {cases} clean-ROM relative angles: all signed magnitude pairs, equal positions, band thresholds and coordinate wrap.");
    }

    private static void ValidateOracleVerticalMotionRom()
    {
        var rom = new ObjectMotionRom();
        int cases = 0;
        (int Z, int Speed, bool Landed) Compare(int initialZ, int initialSpeed, int gravity)
        {
            int slot = (cases & 3) * 0x40;
            int obj = ObjectMotionRom.Object + slot;
            rom.Word(obj + 0x0e, initialZ);
            rom.Word(obj + 0x14, initialSpeed);
            rom.Call(ObjectMotionRom.UpdateSpeedZ, gravity, slot: slot);
            int z = initialZ, speed = initialSpeed;
            bool landed = OracleObjectMath.UpdateSpeedZ(ref z, ref speed, gravity);
            int expectedZ = unchecked((short)rom.Word(obj + 0x0e));
            int expectedSpeed = unchecked((short)rom.Word(obj + 0x14));
            FailIf(z != expectedZ || speed != expectedSpeed || landed != rom.Zero,
                $"objectUpdateSpeedZ $00:$1f45 Z=${unchecked((ushort)initialZ):x4}, " +
                $"speedZ=${unchecked((ushort)initialSpeed):x4}, gravity=${gravity:x2}: " +
                $"ROM=({expectedZ},{expectedSpeed},{rom.Zero}), runtime=({z},{speed},{landed}).");
            cases++;
            return (z, speed, landed);
        }
        for (int z = short.MinValue; z <= short.MaxValue; z++)
        foreach (int speed in new[] { -0x8000, -0x301, -1, 0, 1, 0x301, 0x7fff })
            Compare(z, speed, 0x20);
        for (int speed = short.MinValue; speed <= short.MaxValue; speed++)
        foreach (int z in new[] { -0x8000, -0x100, -1, 0 })
            Compare(z, speed, 0x20);
        for (int gravity = 0; gravity < 256; gravity++)
        foreach (int speed in new[] { -0x8000, -0x301, -1, 0, 1, 0x7f00, 0x7fff })
        foreach (int z in new[] { -0x8000, -0x7fff, -0x301, -0x100, -1, 0, 1, 0x7fff })
            Compare(z, speed, gravity);
        // Follow launches through the exact landing update and one update
        // afterward. The caller may reset/bounce speed; this routine retains it.
        foreach (int launch in new[] { -1, -0x100, -0x301, -0x600 })
        foreach (int gravity in new[] { 1, 0x0e, 0x20, 0xff })
        {
            int z = 0, speed = launch;
            bool landed = false;
            for (int update = 0; update < 4096 && !landed; update++)
                (z, speed, landed) = Compare(z, speed, gravity);
            FailIf(!landed, $"ROM vertical trajectory launch={launch}, gravity=${gravity:x2} never landed.");
            Compare(z, speed, gravity);
        }
        GD.Print($"Validated {cases} clean-ROM vertical motion cases: every Z/speed word, gravity byte, landing flag and signed wrap boundaries.");
    }
}
