using Godot;
using System;

namespace oracleofages;

internal static class CompanionMovement
{
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
