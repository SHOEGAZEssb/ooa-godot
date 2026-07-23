using Godot;

namespace oracleofages;

/// <summary>
/// Shared Link-to-metatile geometry used by original interactable-tile push handlers.
/// </summary>
internal static class InteractableTilePushGeometry
{
    public static bool TryGetCardinalInput(Vector2 input, out Vector2I direction)
    {
        const float threshold = 0.01f;
        bool horizontal = Mathf.Abs(input.X) > threshold;
        bool vertical = Mathf.Abs(input.Y) > threshold;
        if (horizontal == vertical)
        {
            direction = Vector2I.Zero;
            return false;
        }

        direction = horizontal
            ? (input.X > 0 ? Vector2I.Right : Vector2I.Left)
            : (input.Y > 0 ? Vector2I.Down : Vector2I.Up);
        return true;
    }

    public static bool IsAlignedForPush(Vector2 position)
    {
        static bool AxisIsAwayFromCorner(float coordinate)
        {
            int withinTile = Mathf.PosMod(Mathf.FloorToInt(coordinate),
                OracleRoomData.MetatileSize);
            return withinTile is >= 3 and <= 13;
        }

        // interactableTiles.s:@func_433f accepts the push when at least one
        // coordinate is in $03-$0d, preventing Link from pushing while both
        // coordinates place him in a metatile corner.
        return AxisIsAwayFromCorner(position.Y) ||
            AxisIsAwayFromCorner(position.X);
    }

    public static Vector2 FrontTileOffset(Vector2I direction) => direction switch
    {
        var d when d == Vector2I.Up => new Vector2(0, -4),
        var d when d == Vector2I.Right => new Vector2(7, 0),
        var d when d == Vector2I.Down => new Vector2(0, 8),
        _ => new Vector2(-8, 0)
    };

    public static int DirectionIndex(Vector2I direction) => direction switch
    {
        var d when d == Vector2I.Up => 0,
        var d when d == Vector2I.Right => 1,
        var d when d == Vector2I.Down => 2,
        _ => 3
    };
}
