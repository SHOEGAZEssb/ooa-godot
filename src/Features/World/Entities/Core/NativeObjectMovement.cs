using Godot;
using System;

namespace Oracles;

// Executed bank0 movement helpers. Pure geometry keeps using
// OracleObjectMovement; room-owned updates supply their authoritative WRAM.
internal static class NativeObjectMovement
{
    internal static Vector2 ApplySpeed(OracleRuntimeState? memory, ref Vector2 precisePosition, int speed, int angle)
    {
        var position = ApplySpeed(memory, OracleObjectPosition.FromPixels(precisePosition), speed, angle);
        precisePosition = position.PrecisePosition;
        return position.PixelPosition;
    }

    internal static OracleObjectPosition ApplySpeed(OracleRuntimeState? memory, OracleObjectPosition position, int speed, int angle)
    {
        var velocity = Velocity(memory, speed, angle);
        return position.Add(velocity.YFixed, velocity.XFixed);
    }

    internal static OracleObjectVelocity Velocity(OracleRuntimeState? memory, int speed, int angle)
    {
        OracleObjectVelocity velocity = speed == 0 || (angle & 0x80) != 0
            ? new(speed, 0, angle, 0, 0)
            : angle >= 32 || speed % 5 != 0
                ? OracleObjectMovement.Shared.RawVelocity(speed, angle)
                : OracleObjectMovement.Shared.Velocity(speed, angle);
        Write(memory, velocity.YFixed, velocity.XFixed);
        return velocity;
    }

    internal static Vector2I CircleArcOffset(OracleRuntimeState? memory, int distance, int angle)
    {
        var velocity = ScaledVelocity(memory, 0x28, angle, distance);
        return new(unchecked((sbyte)(velocity.XFixed >> 8)), unchecked((sbyte)(velocity.YFixed >> 8)));
    }

    internal static OracleObjectVelocity ScaledVelocity(OracleRuntimeState? memory, int speed, int angle, int scale)
    {
        if (scale is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(scale));
        var velocity = Velocity(memory, speed, angle);
        // getScaledPositionOffsetForVelocity multiplies each word modulo
        // $10000; objectSetPositionInCircleArc adds only the high bytes.
        short y = unchecked((short)(velocity.YFixed * scale));
        short x = unchecked((short)(velocity.XFixed * scale));
        Write(memory, y, x);
        return velocity with { YFixed = y, XFixed = x };
    }

    private static void Write(OracleRuntimeState? memory, int y, int x)
    {
        if (memory is null) return; // Standalone object fixtures have no room WRAM.
        memory.SetWramByte(AgesWramAddress.wTmpcec0, unchecked((byte)y));
        memory.SetWramByte(AgesWramAddress.wTmpcec0 + 1, unchecked((byte)(y >> 8)));
        memory.SetWramByte(AgesWramAddress.wTmpcec0 + 2, unchecked((byte)x));
        memory.SetWramByte(AgesWramAddress.wTmpcec0 + 3, unchecked((byte)(x >> 8)));
    }
}
