using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Coordinate, angle, and fixed-point motion operations shared by
/// original-engine objects. Object angles use 32 clockwise steps with $00
/// facing up; rendered coordinates use the high byte of the original 8.8
/// fixed-point position without rounding.
/// </summary>
internal static class OracleObjectMath
{
    public static Vector2 ToPixelPosition(Vector2 position) => new(
        Mathf.Floor(position.X),
        Mathf.Floor(position.Y));

    /// <summary>
    /// Mirrors objectUpdateSpeedZ_paramC: integrates an object's 8.8 Z
    /// position, applies gravity only while airborne, and clamps Z to zero on
    /// landing. Impact speed is retained for caller-specific bounce behavior.
    /// </summary>
    public static bool UpdateSpeedZ(ref int zFixed, ref int speedZ, int gravity)
    {
        zFixed += speedZ;
        if (zFixed < 0)
        {
            speedZ += gravity;
            return false;
        }

        zFixed = 0;
        return true;
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

    /// <summary>
    /// Converts the source renderer's byte-relative screen position to the
    /// signed edge interval produced by Game Boy OAM wrapping. Coordinates
    /// $f8-$ff are the partially visible -8 through -1 interval; values
    /// already represented as negative host coordinates remain unchanged.
    /// </summary>
    public static Vector2 NormalizeSourceScreenPosition(Vector2 position) => new(
        NormalizeSourceScreenCoordinate(position.X),
        NormalizeSourceScreenCoordinate(position.Y));

    public static Vector2 SourceOamWrapOffset(Vector2 screenPosition) =>
        NormalizeSourceScreenPosition(screenPosition) - screenPosition;

    private static float NormalizeSourceScreenCoordinate(float coordinate) =>
        coordinate is >= 0xf8 and < 0x100
            ? coordinate - 0x100
            : coordinate;
}
