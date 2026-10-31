using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_SPECIAL_WARP $1f:$00 is Ages' diving interaction, outside native tile-warp tables.
internal sealed class DiveWarpDatabase
{
    private readonly Lookup<(int Group, int Room), DiveWarp> _diveWarps = new();
    internal DiveWarpDatabase(string assetRoot)
    {
        GeneratedTable diveTable = GeneratedTable.Load(
            assetRoot + "objects/dive_warps.tsv",
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

}

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
            SourceTransition: WarpSourceTransition.FadeOut,
            DestinationGroup,
            DestinationRoom,
            DestinationPosition,
            DestinationParameter: 0,
            DestinationTransition,
            DirectFadeOut: true);
    }
}
