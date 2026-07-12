using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class WarpDatabase
{
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
        int DestinationTransition);

    private readonly Dictionary<(int Group, int Room), List<Warp>> _warps = new();

    public WarpDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/warps.tsv");
        int count = 0;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 10)
                throw new InvalidOperationException($"Malformed warp data row: {line}");

            var warp = new Warp(
                int.Parse(columns[0]), Convert.ToInt32(columns[1], 16),
                columns[2] == "*" ? -1 : Convert.ToInt32(columns[2], 16),
                int.Parse(columns[3]), int.Parse(columns[4]), int.Parse(columns[5]),
                Convert.ToInt32(columns[6], 16), Convert.ToInt32(columns[7], 16),
                int.Parse(columns[8]), int.Parse(columns[9]));
            var key = (warp.SourceGroup, warp.SourceRoom);
            if (!_warps.TryGetValue(key, out List<Warp>? roomWarps))
            {
                roomWarps = new List<Warp>();
                _warps.Add(key, roomWarps);
            }
            roomWarps.Add(warp);
            count++;
        }
        if (count != 529)
            throw new InvalidOperationException($"Expected 529 warp records, loaded {count}.");
    }

    public bool TryGetTileWarp(int group, int room, int position, byte metatile, out Warp warp)
    {
        if (!IsWarpTile(group, metatile) || !_warps.TryGetValue((group, room), out List<Warp>? warps))
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
            if (candidate.EdgeMask == 0 && candidate.SourcePosition < 0)
            {
                warp = candidate;
                return true;
            }
        }
        warp = default;
        return false;
    }

    public bool HasEdgeWarp(int group, int room, Vector2I direction)
    {
        return TryGetEdgeWarp(group, room, direction, Vector2.Zero, new Vector2(160, 128), out _);
    }

    public bool TryGetEdgeWarp(
        int group,
        int room,
        Vector2I direction,
        Vector2 position,
        Vector2 roomSize,
        out Warp warp)
    {
        if (!_warps.TryGetValue((group, room), out List<Warp>? warps))
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

    private static bool IsWarpTile(int group, byte metatile)
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
