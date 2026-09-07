using Godot;
using System;

namespace oracleofages;

internal static class CompanionMovement
{
    internal static int FacingWallMask(int angle, int walls)
    {
        if (angle == 0xff) return 0;
        int mask = 0;
        if (angle is not (8 or 0x18)) mask |= ((angle >> 3) & 3) == 0 ? 0xc0 : 0x30;
        if ((angle & 15) != 0) mask |= (angle & 0x10) == 0 ? 3 : 0x0c;
        return walls & mask;
    }

    // specialObjectUpdatePosition: adjust an angle at a single blocked corner,
    // then suppress axes using the collision probes at the current position.
    internal static void ApplySpeed(ref Vector2 position, int speed, int angle, int walls)
    {
        if (angle == 0xff) return;
        int movementAngle = AdjustAngleForTileEdge(angle, walls) ?? angle;
        ReadOnlySpan<int> masks =
        [
            0xcf, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3,
            0xf3, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33,
            0x3f, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c,
            0xfc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc
        ];
        int blocked = walls & masks[movementAngle];
        Vector2 next = position;
        OracleObjectMovement.Shared.ApplySpeed(ref next, speed, movementAngle);
        if ((blocked & 0xf0) == 0) position.Y = next.Y;
        if ((blocked & 0x0f) == 0) position.X = next.X;
    }

    private static int? AdjustAngleForTileEdge(int angle, int walls)
    {
        ReadOnlySpan<int> table =
        [
            0x80, 0x80, 1, 2, 2, 2, 3, 0x24,
            0x24, 0x24, 5, 6, 6, 6, 7, 0x48,
            0x48, 0x48, 9, 10, 10, 10, 11, 0x1c,
            0x1c, 0x1c, 13, 14, 14, 14, 15, 0x80
        ];
        int entry = table[angle];
        if ((entry & 3) != 0) return null;
        if ((entry & 0x80) != 0)
        {
            if ((walls & 0xc3) == 0x80) return 8;
            if ((walls & 0xcc) == 0x40) return 0x18;
        }
        else if ((entry & 0x40) != 0)
        {
            if ((walls & 0x33) == 0x20) return 8;
            if ((walls & 0x3c) == 0x10) return 0x18;
        }
        else if ((entry & 0x20) != 0)
        {
            if ((walls & 0xc3) == 1) return 0;
            if ((walls & 0x33) == 2) return 0x10;
        }
        else
        {
            if ((walls & 0xcc) == 4) return 0;
            if ((walls & 0x3c) == 8) return 0x10;
        }
        return null;
    }

    internal static int AngleForInput(Vector2 input)
    {
        int x = Math.Sign(input.X);
        int y = Math.Sign(input.Y);
        return (x, y) switch
        {
            (0, -1) => 0x00,
            (1, -1) => 0x04,
            (1, 0) => 0x08,
            (1, 1) => 0x0c,
            (0, 1) => 0x10,
            (-1, 1) => 0x14,
            (-1, 0) => 0x18,
            (-1, -1) => 0x1c,
            _ => 0xff
        };
    }

    internal static int DirectionForAngle(int angle, int currentDirection)
    {
        if (angle == 0xff)
            return currentDirection;
        int firstDirection = (angle >> 3) & 0x03;
        if ((angle & 0x04) == 0)
            return firstDirection;
        int secondDirection = (firstDirection + 1) & 0x03;
        return currentDirection == firstDirection ||
            currentDirection == secondDirection
                ? currentDirection
                : firstDirection;
    }
}
