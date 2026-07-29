using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Typed runtime view of bank3.objectSpeedTable. Each record is the exact
/// signed 8.8 Y/X displacement consumed by objectApplySpeed for one original
/// speed byte and 32-step angle.
/// </summary>
internal sealed class OracleObjectSpeedTable
{
    internal const int SpeedCount = 24;
    internal const int AngleCount = 32;
    internal const int RecordCount = SpeedCount * AngleCount;

    private readonly OracleObjectVelocity[] _velocities =
        new OracleObjectVelocity[RecordCount];

    internal static OracleObjectSpeedTable Shared { get; } = new();

    private OracleObjectSpeedTable()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/object_speed_vectors.tsv",
            new GeneratedTableSchema(
                "bank3.objectSpeedTable vectors",
                GeneratedTableKeySemantics.Unique,
                [
                    "speed-code", "speed-fixed", "angle",
                    "y-fixed", "x-fixed", "source"
                ],
                ["speed-code", "angle"],
                headerRequired: true));
        if (table.Rows.Count != RecordCount)
        {
            throw new InvalidOperationException(
                $"bank3.objectSpeedTable has {table.Rows.Count} records; " +
                $"expected {RecordCount}.");
        }

        for (int index = 0; index < table.Rows.Count; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            int speedIndex = index / AngleCount;
            int expectedSpeed = (speedIndex + 1) * 5;
            int expectedFixed = (speedIndex + 1) * 0x20;
            int expectedAngle = index % AngleCount;
            int speed = row.HexByte(0);
            int speedFixed = row.UnsignedDecimal(1);
            int angle = row.HexByte(2);
            int yFixed = row.Decimal(3, short.MinValue, short.MaxValue);
            int xFixed = row.Decimal(4, short.MinValue, short.MaxValue);
            string source = row.RequiredString(5);
            if (speed != expectedSpeed ||
                speedFixed != expectedFixed ||
                angle != expectedAngle)
            {
                throw row.Invalid(
                    0,
                    $"ordered speed ${expectedSpeed:x2}, fixed ${expectedFixed:x3}, " +
                    $"angle ${expectedAngle:x2}");
            }
            string expectedSource = $"bank3.objectSpeedTable:SPEED_{SpeedName(speedIndex)}";
            if (!string.Equals(source, expectedSource, StringComparison.Ordinal))
                throw row.Invalid(5, expectedSource);

            _velocities[index] = new OracleObjectVelocity(
                speed, speedFixed, angle, yFixed, xFixed);
        }

        for (int speedIndex = 0; speedIndex < SpeedCount; speedIndex++)
        {
            int speed = (speedIndex + 1) * 5;
            int magnitude = (speedIndex + 1) * 0x20;
            EnsureCardinal(speed, 0x00, -magnitude, 0);
            EnsureCardinal(speed, 0x08, 0, magnitude);
            EnsureCardinal(speed, 0x10, magnitude, 0);
            EnsureCardinal(speed, 0x18, 0, -magnitude);
        }
    }

    internal OracleObjectVelocity Get(int speed, int angle)
    {
        if (angle is < 0 or >= AngleCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(angle), angle,
                "Original object angle must be in the range $00-$1f.");
        }
        if (speed == 0)
            return new OracleObjectVelocity(0, 0, angle, 0, 0);
        if (speed < 5 || speed > 0x78 || speed % 5 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed), speed,
                "Original object speed must be zero or a multiple of five from $05-$78.");
        }
        return _velocities[((speed / 5) - 1) * AngleCount + angle];
    }

    internal Vector2 Delta(int speed, int angle)
    {
        OracleObjectVelocity velocity = Get(speed, angle);
        return new Vector2(
            velocity.XFixed / 256.0f,
            velocity.YFixed / 256.0f);
    }

    private void EnsureCardinal(
        int speed,
        int angle,
        int expectedY,
        int expectedX)
    {
        OracleObjectVelocity velocity = Get(speed, angle);
        if (velocity.YFixed != expectedY || velocity.XFixed != expectedX)
        {
            throw new InvalidOperationException(
                $"bank3.objectSpeedTable speed ${speed:x2}, angle ${angle:x2} " +
                $"is ({velocity.YFixed},{velocity.XFixed}); expected " +
                $"({expectedY},{expectedX}).");
        }
    }

    private static string SpeedName(int index) => index switch
    {
        0 => "20",
        1 => "40",
        2 => "60",
        3 => "80",
        4 => "a0",
        5 => "c0",
        6 => "e0",
        7 => "100",
        8 => "120",
        9 => "140",
        10 => "160",
        11 => "180",
        12 => "1a0",
        13 => "1c0",
        14 => "1e0",
        15 => "200",
        16 => "220",
        17 => "240",
        18 => "260",
        19 => "280",
        20 => "2a0",
        21 => "2c0",
        22 => "2e0",
        23 => "300",
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

internal readonly record struct OracleObjectVelocity(
    int Speed,
    int SpeedFixed,
    int Angle,
    int YFixed,
    int XFixed);
