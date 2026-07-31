using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal static class DungeonShutterEntry
{
    internal const int FirstNormalShutterTile = 0x78;
    internal const int LastNormalShutterTile = 0x7b;
    internal const int FirstMinecartShutterTile = 0x7c;
    internal const int LastMinecartShutterTile = 0x7f;

    internal static int MinecartOpenTile(int closedTile)
    {
        if (closedTile is < FirstMinecartShutterTile or
            > LastMinecartShutterTile)
        {
            throw new ArgumentOutOfRangeException(nameof(closedTile));
        }
        return ((closedTile - FirstMinecartShutterTile) & 1) == 0
            ? 0x5e
            : 0x5d;
    }

    /// <summary>
    /// Mirrors replaceShutterForLinkEntering's eight-row @shutterData table.
    /// Normal shutters become floor $a0. Minecart shutters retain their rail:
    /// up/down doors become vertical track $5e and right/left doors become
    /// horizontal track $5d.
    /// </summary>
    internal static bool TryGetReplacement(
        EnemyPlacementContext placementContext,
        int packedPosition,
        int tile,
        int normalOpenTile,
        out int replacement)
    {
        int doorDirection;
        if (tile is >= FirstNormalShutterTile and <= LastNormalShutterTile)
        {
            doorDirection = tile - FirstNormalShutterTile;
            replacement = normalOpenTile;
        }
        else if (tile is >= FirstMinecartShutterTile and <= LastMinecartShutterTile)
        {
            doorDirection = tile - FirstMinecartShutterTile;
            replacement = MinecartOpenTile(tile);
        }
        else
        {
            replacement = 0;
            return false;
        }

        return Matches(placementContext, packedPosition, doorDirection);
    }

    internal static bool Matches(
        EnemyPlacementContext placementContext,
        int packedPosition,
        int doorDirection)
    {
        if (placementContext.Kind != EnemyPlacementEntryKind.Scrolling ||
            placementContext.EntryPackedPosition != packedPosition)
        {
            return false;
        }

        int incomingDoorDirection = placementContext.ScrollDirection switch
        {
            var direction when direction == Vector2I.Up => 2,
            var direction when direction == Vector2I.Right => 3,
            var direction when direction == Vector2I.Down => 0,
            var direction when direction == Vector2I.Left => 1,
            _ => throw new ArgumentOutOfRangeException(
                nameof(placementContext), placementContext.ScrollDirection,
                "Scroll direction must be cardinal.")
        };
        return doorDirection == incomingDoorDirection;
    }
}
