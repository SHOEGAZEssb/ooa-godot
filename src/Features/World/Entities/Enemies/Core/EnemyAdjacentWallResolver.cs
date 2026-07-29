using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Common ecom_getAdjacentWallsBitset and ecom_bounceOffWalls* terrain probe.
/// The source table stores four cumulative signed Y/X deltas for each rounded
/// angle octant; its first two result bits block Y and its last two block X.
/// </summary>
internal sealed class EnemyAdjacentWallResolver
{
    internal const int OctantCount = 8;
    internal const int ProbesPerOctant = 4;
    internal const int BounceAngleCount = 48;

    internal static EnemyAdjacentWallResolver Shared { get; } = new();

    private readonly EnemyAdjacentWallOffsetRecord[,] _offsets =
        new EnemyAdjacentWallOffsetRecord[OctantCount, ProbesPerOctant];
    private readonly EnemyAdjacentWallOffsetRecord[,] _topDownOffsets =
        new EnemyAdjacentWallOffsetRecord[OctantCount, ProbesPerOctant];
    private readonly EnemyBounceAngleRecord[] _bounceAngles =
        new EnemyBounceAngleRecord[BounceAngleCount];

    internal EnemyAdjacentWallResolver()
    {
        GeneratedTable offsets = GeneratedTable.Load(
            "res://assets/oracle/metadata/enemy_adjacent_wall_offsets.tsv",
            new GeneratedTableSchema(
                "enemy adjacent-wall offsets",
                GeneratedTableKeySemantics.Unique,
                ["octant", "probe", "y-delta", "x-delta", "source"],
                ["octant", "probe"],
                headerRequired: true));
        foreach (GeneratedTableRow row in offsets.Rows)
        {
            int octant = row.UnsignedDecimal(0);
            int probe = row.UnsignedDecimal(1);
            if (octant >= OctantCount || probe >= ProbesPerOctant)
            {
                throw new InvalidOperationException(
                    $"Enemy adjacent-wall offset index {octant}:{probe} is out of range.");
            }
            _offsets[octant, probe] = new EnemyAdjacentWallOffsetRecord(
                octant,
                probe,
                row.Decimal(2),
                row.Decimal(3),
                row.RequiredString(4));
        }
        if (offsets.Rows.Count != OctantCount * ProbesPerOctant)
        {
            throw new InvalidOperationException(
                $"Expected 32 enemy adjacent-wall offsets, got {offsets.Rows.Count}.");
        }

        GeneratedTable topDownOffsets = GeneratedTable.Load(
            "res://assets/oracle/metadata/enemy_topdown_adjacent_wall_offsets.tsv",
            new GeneratedTableSchema(
                "top-down enemy adjacent-wall offsets",
                GeneratedTableKeySemantics.Unique,
                ["octant", "probe", "y-delta", "x-delta", "source"],
                ["octant", "probe"],
                headerRequired: true));
        foreach (GeneratedTableRow row in topDownOffsets.Rows)
        {
            int octant = row.UnsignedDecimal(0);
            int probe = row.UnsignedDecimal(1);
            if (octant >= OctantCount || probe >= ProbesPerOctant)
            {
                throw new InvalidOperationException(
                    $"Top-down enemy adjacent-wall offset index " +
                    $"{octant}:{probe} is out of range.");
            }
            _topDownOffsets[octant, probe] =
                new EnemyAdjacentWallOffsetRecord(
                    octant,
                    probe,
                    row.Decimal(2),
                    row.Decimal(3),
                    row.RequiredString(4));
        }
        if (topDownOffsets.Rows.Count != OctantCount * ProbesPerOctant)
        {
            throw new InvalidOperationException(
                $"Expected 32 top-down enemy adjacent-wall offsets, got " +
                $"{topDownOffsets.Rows.Count}.");
        }

        GeneratedTable angles = GeneratedTable.Load(
            "res://assets/oracle/metadata/enemy_bounce_angles.tsv",
            new GeneratedTableSchema(
                "enemy bounce angles",
                GeneratedTableKeySemantics.Unique,
                ["index", "angle", "source"],
                ["index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in angles.Rows)
        {
            int index = row.UnsignedDecimal(0);
            if (index >= BounceAngleCount)
            {
                throw new InvalidOperationException(
                    $"Enemy bounce-angle index {index} is out of range.");
            }
            _bounceAngles[index] = new EnemyBounceAngleRecord(
                index,
                row.HexByte(1),
                row.RequiredString(2));
        }
        if (angles.Rows.Count != BounceAngleCount)
        {
            throw new InvalidOperationException(
                $"Expected 48 enemy bounce angles, got {angles.Rows.Count}.");
        }
    }

    internal EnemyAdjacentWallProbe Probe(
        Vector2 position,
        int angle,
        Func<Vector2I, bool> collides) =>
        Probe(_offsets, position, angle, collides);

    internal EnemyAdjacentWallProbe ProbeTopDown(
        Vector2 position,
        int angle,
        Func<Vector2I, bool> collides) =>
        Probe(_topDownOffsets, position, angle, collides);

    private static EnemyAdjacentWallProbe Probe(
        EnemyAdjacentWallOffsetRecord[,] offsets,
        Vector2 position,
        int angle,
        Func<Vector2I, bool> collides)
    {
        ArgumentNullException.ThrowIfNull(collides);
        int octant = OctantForAngle(angle);
        Vector2I point = new(
            Mathf.FloorToInt(position.X),
            Mathf.FloorToInt(position.Y));
        int bitset = 0;
        for (int probe = 0; probe < ProbesPerOctant; probe++)
        {
            EnemyAdjacentWallOffsetRecord offset = offsets[octant, probe];
            point += new Vector2I(offset.XDelta, offset.YDelta);
            bitset <<= 1;
            if (collides(point))
                bitset |= 1;
        }
        return new EnemyAdjacentWallProbe(
            bitset,
            YBlocked: (bitset & 0x0c) != 0,
            XBlocked: (bitset & 0x03) != 0);
    }

    internal int BounceAngle(int angle, EnemyAdjacentWallProbe probe)
    {
        ValidateAngle(angle);
        if (probe.XBlocked && probe.YBlocked)
            return (angle + 0x10) & 0x1f;
        if (probe.XBlocked)
            return _bounceAngles[0x10 + angle].Angle;
        if (probe.YBlocked)
            return _bounceAngles[angle].Angle;
        return angle;
    }

    internal int BounceAngle(
        Vector2 position,
        int angle,
        Func<Vector2I, bool> collides) =>
        BounceAngle(angle, Probe(position, angle, collides));

    internal EnemyAdjacentWallOffsetRecord Offset(int octant, int probe)
    {
        if (octant is < 0 or >= OctantCount)
            throw new ArgumentOutOfRangeException(nameof(octant));
        if (probe is < 0 or >= ProbesPerOctant)
            throw new ArgumentOutOfRangeException(nameof(probe));
        return _offsets[octant, probe];
    }

    internal EnemyAdjacentWallOffsetRecord TopDownOffset(
        int octant,
        int probe)
    {
        if (octant is < 0 or >= OctantCount)
            throw new ArgumentOutOfRangeException(nameof(octant));
        if (probe is < 0 or >= ProbesPerOctant)
            throw new ArgumentOutOfRangeException(nameof(probe));
        return _topDownOffsets[octant, probe];
    }

    internal EnemyBounceAngleRecord BounceAngleRecord(int index)
    {
        if (index is < 0 or >= BounceAngleCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _bounceAngles[index];
    }

    internal static int OctantForAngle(int angle)
    {
        ValidateAngle(angle);
        int doubled = angle * 2;
        int tableOffset = (doubled & 0x0f) == 0
            ? doubled
            : (doubled & 0xf0) + 8;
        return tableOffset / 8;
    }

    private static void ValidateAngle(int angle)
    {
        if (angle is < 0 or >= 0x20)
            throw new ArgumentOutOfRangeException(nameof(angle));
    }
}

internal readonly record struct EnemyAdjacentWallOffsetRecord(
    int Octant,
    int Probe,
    int YDelta,
    int XDelta,
    string Source);

internal readonly record struct EnemyBounceAngleRecord(
    int Index,
    int Angle,
    string Source);

internal readonly record struct EnemyAdjacentWallProbe(
    int Bitset,
    bool YBlocked,
    bool XBlocked);
