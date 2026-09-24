using Godot;
using System;

namespace oracleofages;

// Executed bank0 movement helpers. Pure geometry keeps using
// OracleObjectMovement; room-owned updates supply their authoritative WRAM.
internal static class NativeObjectMovement
{
    internal static Vector2 ApplySpeed(OracleRuntimeState? memory, ref Vector2 precisePosition, int speed, int angle)
    {
        var velocity = Velocity(memory, speed, angle);
        var position = OracleObjectPosition.FromPixels(precisePosition).Add(velocity.YFixed, velocity.XFixed);
        precisePosition = position.PrecisePosition;
        return position.PixelPosition;
    }

    internal static OracleObjectVelocity Velocity(OracleRuntimeState? memory, int speed, int angle)
    {
        OracleObjectVelocity velocity = speed == 0 || (angle & 0x80) != 0
            ? new(speed, 0, angle, 0, 0)
            : OracleObjectMovement.Shared.Velocity(speed, angle);
        Write(memory, velocity.YFixed, velocity.XFixed);
        return velocity;
    }

    internal static Vector2I CircleArcOffset(OracleRuntimeState? memory, int distance, int angle)
    {
        if (distance is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(distance));
        var velocity = Velocity(memory, 0x28, angle);
        // getScaledPositionOffsetForVelocity multiplies each word modulo
        // $10000; objectSetPositionInCircleArc adds only the high bytes.
        short y = unchecked((short)(velocity.YFixed * distance));
        short x = unchecked((short)(velocity.XFixed * distance));
        Write(memory, y, x);
        return new(unchecked((sbyte)(x >> 8)), unchecked((sbyte)(y >> 8)));
    }

    private static void Write(OracleRuntimeState? memory, int y, int x)
    {
        if (memory is null) return; // Standalone object fixtures have no room WRAM.
        memory.SetWramByte(0xcec0, unchecked((byte)y));
        memory.SetWramByte(0xcec1, unchecked((byte)(y >> 8)));
        memory.SetWramByte(0xcec2, unchecked((byte)x));
        memory.SetWramByte(0xcec3, unchecked((byte)(x >> 8)));
    }
}
