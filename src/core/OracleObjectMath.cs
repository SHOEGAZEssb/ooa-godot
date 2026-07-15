using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Coordinate and angle operations shared by original-engine objects. Object
/// angles use 32 clockwise steps with $00 facing up; rendered coordinates use
/// the high byte of the original 8.8 fixed-point position without rounding.
/// </summary>
internal static class OracleObjectMath
{
    public static Vector2 ToPixelPosition(Vector2 position) => new(
        Mathf.Floor(position.X),
        Mathf.Floor(position.Y));

    public static Vector2 VectorFromAngle32(int angle)
    {
        float radians = angle * Mathf.Pi / 16.0f;
        return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
    }

    public static int AngleToward(Vector2 origin, Vector2 target)
    {
        Vector2 difference = target - origin;
        float radians = Mathf.Atan2(difference.X, -difference.Y);
        return Mathf.PosMod(Mathf.RoundToInt(radians * 32.0f / Mathf.Tau), 32);
    }

    /// <summary>
    /// Selects the cardinal octant used by objects that intentionally ignore
    /// the low three angle bits.
    /// </summary>
    public static Vector2 CardinalVector(int angle) => (angle & 0x18) switch
    {
        0x00 => Vector2.Up,
        0x08 => Vector2.Right,
        0x10 => Vector2.Down,
        _ => Vector2.Left
    };

    /// <summary>
    /// Decodes an imported angle that must already be exactly cardinal.
    /// </summary>
    public static Vector2 StrictCardinalVector(int angle) => angle switch
    {
        0x00 => Vector2.Up,
        0x08 => Vector2.Right,
        0x10 => Vector2.Down,
        0x18 => Vector2.Left,
        _ => throw new InvalidOperationException(
            $"Unsupported cardinal object angle ${angle:x2}.")
    };

    public static bool IsInsideOriginalScreenBoundary(Vector2 position) =>
        position.Y >= -7 && position.Y < 136 &&
        position.X >= -7 && position.X < 168;
}
