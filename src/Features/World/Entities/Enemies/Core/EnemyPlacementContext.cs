using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Inputs consumed by checkPositionValidForEnemySpawn. Ordinary scrolling
/// excludes the three metatile rows or columns at Link's incoming edge; a
/// packed warp destination excludes the surrounding 5x5-metatile square.
/// Scrolling also retains Link's final packed position for the destination
/// room's replaceShutterForLinkEntering pass.
/// </summary>
internal readonly record struct EnemyPlacementContext(
    EnemyPlacementEntryKind Kind,
    Vector2I ScrollDirection,
    int WarpDestination,
    int EntryPackedPosition)
{
    internal static EnemyPlacementContext Unrestricted => new(
        EnemyPlacementEntryKind.Unrestricted, Vector2I.Zero, -1, -1);

    internal static EnemyPlacementContext Scrolling(
        Vector2I direction,
        int entryPackedPosition = -1)
    {
        if (direction != Vector2I.Up && direction != Vector2I.Right &&
            direction != Vector2I.Down && direction != Vector2I.Left)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction), direction, "Scroll direction must be cardinal.");
        }
        if (entryPackedPosition is < -1 or >= 0xf0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entryPackedPosition), entryPackedPosition,
                "A scrolling entry position must be a packed position below `$f0.");
        }
        return new EnemyPlacementContext(
            EnemyPlacementEntryKind.Scrolling, direction, -1, entryPackedPosition);
    }

    internal static EnemyPlacementContext Warp(int packedDestination)
    {
        if (packedDestination is < 0 or >= 0xf0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packedDestination), packedDestination,
                "A direct warp destination must be a packed position below `$f0.");
        }
        return new EnemyPlacementContext(
            EnemyPlacementEntryKind.Warp, Vector2I.Zero, packedDestination, -1);
    }

    internal static EnemyPlacementContext FromWarpDestination(int packedDestination) =>
        packedDestination >= 0xf0
            ? new EnemyPlacementContext(
                EnemyPlacementEntryKind.ScreenWarp, Vector2I.Up, packedDestination, -1)
            : Warp(packedDestination);

    internal bool Allows(OracleRoomData room, int packedPosition)
    {
        int tileY = packedPosition >> 4;
        int tileX = packedPosition & 0x0f;
        return Kind switch
        {
            EnemyPlacementEntryKind.Unrestricted => true,
            EnemyPlacementEntryKind.Warp =>
                Math.Abs(tileY - (WarpDestination >> 4)) >= 3 ||
                Math.Abs(tileX - (WarpDestination & 0x0f)) >= 3,
            EnemyPlacementEntryKind.Scrolling or EnemyPlacementEntryKind.ScreenWarp =>
                AllowsScrolling(room, tileX, tileY),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
        };
    }

    private bool AllowsScrolling(OracleRoomData room, int tileX, int tileY)
    {
        bool small = room.Group < 4;
        int minimumY = small ? 0 : 1;
        int maximumY = small ? room.HeightInTiles : room.HeightInTiles - 1;
        int minimumX = small ? 0 : 1;
        int maximumX = small ? room.WidthInTiles : room.WidthInTiles - 1;

        if (ScrollDirection == Vector2I.Up)
            maximumY = room.HeightInTiles - 3;
        else if (ScrollDirection == Vector2I.Right)
            minimumX = 3;
        else if (ScrollDirection == Vector2I.Down)
            minimumY = 3;
        else
            maximumX = small ? room.WidthInTiles - 3 : room.WidthInTiles - 4;

        return tileY >= minimumY && tileY < maximumY &&
            tileX >= minimumX && tileX < maximumX;
    }
}
