using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal static class DungeonShutterEntry
{
    internal const int FirstNormalShutterTile = 0x78;
    internal const int LastNormalShutterTile = 0x7b;

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
