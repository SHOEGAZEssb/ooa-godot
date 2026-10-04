using Godot;

namespace oracleofages;

// bank0.s:preventObjectHFromPassingObjectD, for an interaction and Link.
internal static class LinkObjectBlocking
{
    internal static bool PreventPassing(Player player, Vector2 obstacle, float collisionRadiusY, float collisionRadiusX)
    {
        if (player.PassesNpcs) return false;
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        float radiusY = collisionRadiusY + 6;
        float radiusX = collisionRadiusX + 6;
        float differenceY = Mathf.Abs(link.Y - obstacle.Y);
        float differenceX = Mathf.Abs(link.X - obstacle.X);
        // The unsigned comparison includes -radius and excludes +radius.
        if (((int)(link.Y - obstacle.Y + radiusY) & 0xff) >= radiusY * 2 ||
            ((int)(link.X - obstacle.X + radiusX) & 0xff) >= radiusX * 2)
            return false;
        // CP's equal-overlap path resolves horizontally. Only the coordinate
        // high byte is written; a fractional approach retains its low byte.
        bool horizontal = radiusY - differenceY >= radiusX - differenceX;
        float coordinate = horizontal ? link.X : link.Y;
        float origin = horizontal ? obstacle.X : obstacle.Y;
        int side = coordinate > origin ? 1 : -1;
        player.SetScriptedCoordinateHigh(horizontal, unchecked((byte)(Mathf.FloorToInt(origin) +
            side * Mathf.RoundToInt(horizontal ? radiusX : radiusY))));
        return true;
    }
}
