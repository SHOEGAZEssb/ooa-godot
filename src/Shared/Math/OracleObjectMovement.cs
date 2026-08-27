using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Game-wide owner of the original objectGetRelativeAngle and objectApplySpeed
/// paths. Positions are unsigned wrapping 8.8 words; callers keep the precise
/// word between updates and render its high byte.
/// </summary>
internal sealed class OracleObjectMovement
{
    private const int OctantCount = 8;
    private const int BandsPerOctant = 8;
    private const int DirectionCount = OctantCount * BandsPerOctant;

    private readonly OracleObjectSpeedTable _speeds = OracleObjectSpeedTable.Shared;
    private readonly byte[] _relativeAngles = new byte[DirectionCount];

    internal static OracleObjectMovement Shared { get; } = new();

    private OracleObjectMovement()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/object_relative_angles.tsv",
            new GeneratedTableSchema(
                "bank0.pushDirectionData",
                GeneratedTableKeySemantics.Unique,
                ["octant", "band", "angle", "source"],
                ["octant", "band"],
                headerRequired: true));
        if (table.Rows.Count != DirectionCount)
        {
            throw new InvalidOperationException(
                $"bank0.pushDirectionData has {table.Rows.Count} records; " +
                $"expected {DirectionCount}.");
        }

        for (int index = 0; index < table.Rows.Count; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            int octant = row.Decimal(0, 0, OctantCount - 1);
            int band = row.Decimal(1, 0, BandsPerOctant - 1);
            int expectedOctant = index / BandsPerOctant;
            int expectedBand = index % BandsPerOctant;
            if (octant != expectedOctant || band != expectedBand)
            {
                throw row.Invalid(
                    0, $"ordered octant {expectedOctant}, band {expectedBand}");
            }

            int angle = row.HexByte(2);
            if (angle >= OracleObjectSpeedTable.AngleCount)
                throw row.Invalid(2, "angle $00-$1f");
            string source = row.RequiredString(3);
            string expectedSource = $"bank0.pushDirectionData+{index:x2}";
            if (!string.Equals(source, expectedSource, StringComparison.Ordinal))
                throw row.Invalid(3, expectedSource);
            _relativeAngles[index] = (byte)angle;
        }
    }

    internal OracleObjectVelocity Velocity(int speed, int angle) =>
        _speeds.Get(speed, angle);

    internal Vector2 Delta(int speed, int angle)
    {
        OracleObjectVelocity velocity = Velocity(speed, angle);
        return new Vector2(
            velocity.XFixed / 256.0f,
            velocity.YFixed / 256.0f);
    }

    /// <summary>
    /// Returns the exact SPEED_100 direction vector. This is useful for source
    /// geometry whose radius is scaled after the original angle lookup.
    /// </summary>
    internal Vector2 Direction(int angle) => Delta(0x28, angle);

    internal OracleObjectPosition PositionFromPixels(Vector2 position) =>
        OracleObjectPosition.FromPixels(position);

    internal OracleObjectPosition ApplySpeed(
        OracleObjectPosition position,
        int speed,
        int angle)
    {
        OracleObjectVelocity velocity = Velocity(speed, angle);
        return position.Add(velocity.YFixed, velocity.XFixed);
    }

    /// <summary>
    /// Ports bank0.objectUpdateSpeedZ_sidescroll. Positive speed probes y+$06
    /// at x-$04 and x+$03 and returns before either position or speed changes
    /// when a floor is found.
    /// </summary>
    internal bool UpdateSpeedZSidescroll(
        ref OracleObjectPosition position,
        ref int speedZ,
        int gravity,
        Func<Vector2, bool> isSolidAllowingHoles)
    {
        short signedSpeed = unchecked((short)speedZ);
        if (signedSpeed >= 0)
        {
            int y = unchecked((byte)((position.YFixed >> 8) + 6));
            int x = position.XFixed >> 8;
            if (isSolidAllowingHoles(new Vector2(
                    unchecked((byte)(x - 4)), y)) ||
                isSolidAllowingHoles(new Vector2(
                    unchecked((byte)(x + 3)), y)))
            {
                return true;
            }
        }

        position = position.Add(signedSpeed, xFixed: 0);
        speedZ = unchecked((short)(signedSpeed + gravity));
        return false;
    }

    /// <summary>
    /// Applies objectApplySpeed to a retained precise position and returns the
    /// high-byte rendering position.
    /// </summary>
    internal Vector2 ApplySpeed(
        ref Vector2 precisePosition,
        int speed,
        int angle)
    {
        OracleObjectPosition position = ApplySpeed(
            PositionFromPixels(precisePosition), speed, angle);
        precisePosition = position.PrecisePosition;
        return position.PixelPosition;
    }

    internal int RelativeAngle(Vector2 origin, Vector2 target) =>
        RelativeAngle(
            OracleObjectPosition.HighByte(origin.Y),
            OracleObjectPosition.HighByte(origin.X),
            OracleObjectPosition.HighByte(target.Y),
            OracleObjectPosition.HighByte(target.X));

    /// <summary>
    /// Exact port of objectGetRelativeAngleWithTempVars. Adding eight before
    /// each subtraction intentionally moves the unsigned wrap boundary to
    /// $f8, matching the source's supported on-screen coordinate interval.
    /// </summary>
    internal int RelativeAngle(
        byte originY,
        byte originX,
        byte targetY,
        byte targetX)
    {
        int octant = 0;
        int yMagnitude = WrappedMagnitude(originY, targetY, 0x04, ref octant);
        int xMagnitude = WrappedMagnitude(originX, targetX, 0x02, ref octant);

        int maximum = xMagnitude;
        int minimum = yMagnitude;
        if (xMagnitude < yMagnitude)
        {
            octant++;
            maximum = yMagnitude;
            minimum = xMagnitude;
        }

        int threshold = (maximum >> 3) << 1;
        int band = 0;
        int accumulated = threshold;
        while (accumulated < minimum && band < 4)
        {
            band++;
            accumulated = (accumulated + threshold) & 0xff;
        }
        return _relativeAngles[octant * BandsPerOctant + band];
    }

    private static int WrappedMagnitude(
        byte origin,
        byte target,
        int negativeOctantBit,
        ref int octant)
    {
        int adjustedOrigin = (origin + 8) & 0xff;
        int adjustedTarget = (target + 8) & 0xff;
        int difference = (adjustedOrigin - adjustedTarget) & 0xff;
        if (adjustedOrigin >= adjustedTarget)
            return difference;

        octant += negativeOctantBit;
        return (-difference) & 0xff;
    }
}

/// <summary>
/// An original object's unsigned wrapping Y/X 8.8 coordinate words.
/// </summary>
internal readonly record struct OracleObjectPosition(
    ushort YFixed,
    ushort XFixed)
{
    internal Vector2 PrecisePosition => new(XFixed / 256.0f, YFixed / 256.0f);
    internal Vector2 PixelPosition => new(XFixed >> 8, YFixed >> 8);

    internal static OracleObjectPosition FromPixels(Vector2 position) => new(
        unchecked((ushort)Mathf.FloorToInt(position.Y * 256.0f)),
        unchecked((ushort)Mathf.FloorToInt(position.X * 256.0f)));

    internal static byte HighByte(float coordinate) =>
        unchecked((byte)Mathf.FloorToInt(coordinate));

    internal OracleObjectPosition Add(int yFixed, int xFixed) => new(
        unchecked((ushort)(YFixed + yFixed)),
        unchecked((ushort)(XFixed + xFixed)));
}
