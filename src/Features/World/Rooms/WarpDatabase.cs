using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class WarpDatabase
{

    private readonly Lookup<(int Group, int Room), Warp> _warps = new();
    private readonly Lookup<(int Group, int Room), DiveWarp> _diveWarps = new();

    public WarpDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/warps.tsv",
            new GeneratedTableSchema(
                "warps",
                GeneratedTableKeySemantics.Grouped,
                [
                    "source-group", "source-room", "source-position", "edge-mask",
                    "source-transition", "dest-group", "dest-room", "dest-position",
                    "dest-parameter", "dest-transition", "source-fallback"
                ],
                ["source-group", "source-room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            Warp warp = new Warp(
                row.Decimal(0, 0, 7), row.HexByte(1),
                row.HexByteOrSentinel(2, "*", -1),
                row.UnsignedDecimal(3), row.UnsignedDecimal(4),
                row.Decimal(5, 0, 7), row.HexByte(6), row.HexByte(7),
                row.UnsignedDecimal(8), row.UnsignedDecimal(9),
                row.Boolean01(10));
            _warps.Add((warp.SourceGroup, warp.SourceRoom), warp);
            count++;
        }
        if (count != 529)
            throw new InvalidOperationException($"Expected 529 warp records, loaded {count}.");

        GeneratedTable diveTable = GeneratedTable.Load(
            "res://assets/oracle/objects/dive_warps.tsv",
            new GeneratedTableSchema(
                "dive warps",
                GeneratedTableKeySemantics.Grouped,
                [
                    "source-group", "source-room", "order", "source-position",
                    "route-index", "collision-radius", "dest-group", "dest-room",
                    "dest-position", "dest-transition", "warp-transition-2",
                    "source"
                ],
                ["source-group", "source-room"],
                headerRequired: true));
        int diveCount = 0;
        foreach (GeneratedTableRow row in diveTable.Rows)
        {
            DiveWarp diveWarp = new(
                row.Decimal(0, 0, 7), row.HexByte(1),
                row.UnsignedDecimal(2), row.HexByte(3), row.HexByte(4),
                row.UnsignedDecimal(5), row.Decimal(6, 0, 7),
                row.HexByte(7), row.HexByte(8), row.UnsignedDecimal(9),
                row.UnsignedDecimal(10), row.RequiredString(11));
            if (diveWarp.WarpTransition2 != 3)
            {
                throw new InvalidOperationException(
                    $"Unsupported {diveWarp.Source}: wWarpTransition2=" +
                    $"${diveWarp.WarpTransition2:x2}.");
            }
            _diveWarps.Add(
                (diveWarp.SourceGroup, diveWarp.SourceRoom), diveWarp);
            diveCount++;
        }
        if (diveCount != 2)
        {
            throw new InvalidOperationException(
                $"Expected 2 INTERAC_SPECIAL_WARP dive records, loaded {diveCount}.");
        }
    }

    internal bool TryGetDiveWarp(
        int group,
        int room,
        Vector2 linkPosition,
        out DiveWarp diveWarp)
    {
        if (_diveWarps.TryGetValues(
                (group, room), out IReadOnlyList<DiveWarp> diveWarps))
        {
            foreach (DiveWarp candidate in diveWarps)
            {
                if (candidate.Touches(linkPosition))
                {
                    diveWarp = candidate;
                    return true;
                }
            }
        }
        diveWarp = default;
        return false;
    }

    public bool TryGetTileWarp(int group, int room, int position, byte metatile, out Warp warp)
    {
        if (!IsWarpTile(group, metatile) ||
            !_warps.TryGetValues((group, room), out IReadOnlyList<Warp> warps))
        {
            warp = default;
            return false;
        }

        foreach (Warp candidate in warps)
        {
            if (candidate.EdgeMask == 0 && candidate.SourcePosition == position)
            {
                warp = candidate;
                return true;
            }
        }
        foreach (Warp candidate in warps)
        {
            if (candidate.EdgeMask == 0 && candidate.SourceFallback)
            {
                warp = candidate;
                return true;
            }
        }
        warp = default;
        return false;
    }

    public bool TryGetEdgeWarp(
        int group,
        int room,
        Vector2I direction,
        Vector2 position,
        Vector2 roomSize,
        out Warp warp)
    {
        if (!_warps.TryGetValues(
                (group, room), out IReadOnlyList<Warp> warps))
        {
            warp = default;
            return false;
        }

        if (direction != Vector2I.Up && direction != Vector2I.Down)
        {
            warp = default;
            return false;
        }

        float horizontalSplit = roomSize.X <= OracleRoomData.ViewportWidth ? 0x58 : 0x80;
        int preferredBit = direction == Vector2I.Up
            ? (position.X < horizontalSplit ? 0x01 : 0x02)
            : (position.X < horizontalSplit ? 0x04 : 0x08);
        foreach (Warp candidate in warps)
        {
            if ((candidate.EdgeMask & preferredBit) != 0)
            {
                warp = candidate;
                return true;
            }
        }
        warp = default;
        return false;
    }

    internal static bool IsWarpTile(int group, byte metatile)
    {
        return group switch
        {
            0 or 1 => metatile is 0xdc or 0xdd or 0xde or 0xdf or 0xed or 0xee or 0xef,
            2 or 3 => metatile is 0x34 or 0x36 or 0x44 or 0x45 or 0x46 or 0x47 or 0xaf,
            4 or 5 => metatile is 0x44 or 0x45 or 0x46 or 0x47 or 0x4f,
            _ => false
        };
    }
}

public readonly record struct Warp(
    int SourceGroup,
    int SourceRoom,
    int SourcePosition,
    int EdgeMask,
    int SourceTransition,
    int DestinationGroup,
    int DestinationRoom,
    int DestinationPosition,
    int DestinationParameter,
    int DestinationTransition,
    bool SourceFallback = false);

internal readonly record struct DiveWarp(
    int SourceGroup,
    int SourceRoom,
    int Order,
    int SourcePosition,
    int RouteIndex,
    int CollisionRadius,
    int DestinationGroup,
    int DestinationRoom,
    int DestinationPosition,
    int DestinationTransition,
    int WarpTransition2,
    string Source)
{
    internal Vector2 SourceCenter => new(
        (SourcePosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        ((SourcePosition >> 4) & 0x0f) * OracleRoomData.MetatileSize + 8);

    internal bool Touches(Vector2 linkPosition)
    {
        Vector2 delta = linkPosition - SourceCenter;
        float combinedRadius =
            CollisionRadius + NpcCharacter.LinkCollisionRadius;
        return Mathf.Abs(delta.X) < combinedRadius &&
            Mathf.Abs(delta.Y) < combinedRadius;
    }

    internal Warp ToWarp()
    {
        // specialWarp.s writes wWarpTransition2=$03, the scripted fadeout
        // counterpart of the standard source transition $02 handled by the
        // runtime controller.
        return new Warp(
            SourceGroup,
            SourceRoom,
            SourcePosition,
            EdgeMask: 0,
            SourceTransition: 2,
            DestinationGroup,
            DestinationRoom,
            DestinationPosition,
            DestinationParameter: 0,
            DestinationTransition);
    }
}
