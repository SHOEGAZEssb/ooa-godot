using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed runtime view of checkLinkJumpingOffCliff, cliffTilesTable,
/// landableTileFromCliffExceptions, and LINK_STATE_JUMPING_DOWN_LEDGE.
/// </summary>
internal sealed class LedgeJumpDatabase
{
    private readonly HashSet<int> _cliffTiles = new();
    private readonly HashSet<int> _landableSolidTiles = new();
    private readonly LedgeJumpDirectionRecord[] _directions =
        new LedgeJumpDirectionRecord[4];
    private readonly int[] _speedRawByLength = new int[11];
    private readonly Dictionary<string, int> _constants = new();

    internal int InitialSpeedZ => Constant("initial-speed-z");
    internal int TransitionSpeedZ => Constant("transition-speed-z");
    internal int Gravity => Constant("gravity");
    internal int JumpSound => Constant("jump-sound");
    internal int LandSound => Constant("land-sound");
    internal int FeetOffset => Constant("feet-offset");
    internal int ScanStep => Constant("scan-step");
    internal int MaxSpeedLength => Constant("max-speed-length");
    internal int[] AnimationPhaseDurations =>
    [
        Constant("animation-phase-0"),
        Constant("animation-phase-1"),
        Constant("animation-phase-2")
    ];

    internal LedgeJumpDatabase()
    {
        LoadCliffTiles();
        LoadLandableTiles();
        LoadDirections();
        LoadSpeeds();
        LoadConstants();
        Validate();
    }

    internal bool IsCliffTile(
        int activeCollisions,
        byte tile,
        int angle) =>
        _cliffTiles.Contains(
            (activeCollisions << 16) | (tile << 8) | angle);

    internal bool IsLandableSolidTile(int activeCollisions, byte tile) =>
        _landableSolidTiles.Contains((activeCollisions << 8) | tile);

    internal LedgeJumpDirectionRecord Direction(Vector2I direction)
    {
        int index = direction == Vector2I.Up ? 0
            : direction == Vector2I.Right ? 1
            : direction == Vector2I.Down ? 2
            : direction == Vector2I.Left ? 3
            : throw new ArgumentOutOfRangeException(nameof(direction));
        return _directions[index];
    }

    internal int SpeedRaw(int cliffLength)
    {
        int index = Math.Clamp(cliffLength, 1, MaxSpeedLength) - 1;
        return _speedRawByLength[index];
    }

    private void LoadCliffTiles()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/ledge_cliff_tiles.tsv",
            new GeneratedTableSchema(
                "ledge cliff tiles",
                GeneratedTableKeySemantics.Unique,
                ["active-collisions", "tile", "angle"],
                ["active-collisions", "tile", "angle"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int activeCollisions = row.UnsignedDecimal(0);
            int tile = row.HexByte(1);
            int angle = row.HexByte(2);
            _cliffTiles.Add(
                (activeCollisions << 16) | (tile << 8) | angle);
        }
    }

    private void LoadLandableTiles()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/ledge_landable_tiles.tsv",
            new GeneratedTableSchema(
                "ledge landable solid tiles",
                GeneratedTableKeySemantics.Unique,
                ["active-collisions", "tile"],
                ["active-collisions", "tile"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int activeCollisions = row.UnsignedDecimal(0);
            int tile = row.HexByte(1);
            _landableSolidTiles.Add((activeCollisions << 8) | tile);
        }
    }

    private void LoadDirections()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/ledge_jump_directions.tsv",
            new GeneratedTableSchema(
                "ledge jump directions",
                GeneratedTableKeySemantics.Unique,
                [
                    "direction", "angle", "wall-mask",
                    "probe1-y", "probe1-x", "probe2-y", "probe2-x"
                ],
                ["direction"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int direction = row.UnsignedDecimal(0);
            if ((uint)direction >= _directions.Length ||
                _directions[direction] != default)
            {
                throw row.Invalid(0, "one row for each direction 0-3");
            }
            _directions[direction] = new LedgeJumpDirectionRecord(
                direction,
                row.HexByte(1),
                row.HexByte(2),
                new Vector2I(row.Decimal(4), row.Decimal(3)),
                new Vector2I(row.Decimal(6), row.Decimal(5)));
        }
    }

    private void LoadSpeeds()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/ledge_jump_speeds.tsv",
            new GeneratedTableSchema(
                "ledge jump speeds",
                GeneratedTableKeySemantics.Unique,
                ["length", "speed-raw"],
                ["length"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int length = row.UnsignedDecimal(0);
            if (length is < 1 or > 11 ||
                _speedRawByLength[length - 1] != 0)
            {
                throw row.Invalid(0, "one row for each cliff length 1-11");
            }
            _speedRawByLength[length - 1] = row.HexByte(1);
        }
    }

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/ledge_jump_constants.tsv",
            new GeneratedTableSchema(
                "ledge jump constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));
    }

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Ledge jump constant '{key}' was not imported.");

    private void Validate()
    {
        if (_cliffTiles.Count != 38 ||
            _landableSolidTiles.Count != 6 ||
            Array.Exists(_directions, record => record == default) ||
            Array.Exists(_speedRawByLength, speed => speed == 0) ||
            _directions[0] != new LedgeJumpDirectionRecord(
                0, 0x00, 0xc0,
                new Vector2I(-3, -4), new Vector2I(2, -4)) ||
            _directions[1] != new LedgeJumpDirectionRecord(
                1, 0x08, 0x03,
                new Vector2I(4, 0), new Vector2I(4, 5)) ||
            _directions[2] != new LedgeJumpDirectionRecord(
                2, 0x10, 0x30,
                new Vector2I(-3, 8), new Vector2I(2, 8)) ||
            _directions[3] != new LedgeJumpDirectionRecord(
                3, 0x18, 0x0c,
                new Vector2I(-5, 0), new Vector2I(-5, 5)) ||
            !_speedRawByLength.AsSpan().SequenceEqual(
                [0x14, 0x19, 0x23, 0x2d, 0x37, 0x41,
                    0x50, 0x5a, 0x64, 0x6e, 0x78]) ||
            InitialSpeedZ != -0x1c0 ||
            TransitionSpeedZ != -0x100 ||
            Gravity != 0x20 ||
            JumpSound != OracleSoundEngine.SndJump ||
            LandSound != OracleSoundEngine.SndLand ||
            FeetOffset != 5 ||
            ScanStep != 8 ||
            MaxSpeedLength != 11 ||
            !AnimationPhaseDurations.AsSpan().SequenceEqual([9, 9, 6]))
        {
            throw new InvalidOperationException(
                "Imported Ages ledge-jump tables are incomplete or inconsistent.");
        }
    }
}

internal readonly record struct LedgeJumpDirectionRecord(
    int Direction,
    int Angle,
    int WallMask,
    Vector2I Probe1,
    Vector2I Probe2);
