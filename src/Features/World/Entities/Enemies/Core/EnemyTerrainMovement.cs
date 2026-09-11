using Godot;

namespace oracleofages;

/// <summary>
/// Shared adjacent-wall enemy movement. Species state machines still choose
/// the angle, speed, hole policy, and source collision-box variant.
/// </summary>
internal sealed class EnemyTerrainMovement(Node2D entity, OracleRoomData room)
{
    public HazardType Hazard =>
        room.GetTerrainInfo(entity.Position).Hazard;

    public bool MoveAtAngle(int angle, int speed, bool allowHoles) =>
        MoveUsingAdjacentWalls(
            angle,
            speed,
            allowHoles,
            topDown: false);

    /// <summary>
    /// Applies ecom_applyGivenVelocityGivenAdjacentWalls with either the
    /// side-view or top-down cumulative probe table. A one-sided collision
    /// contributes the source's signed $0060 wall-slide component.
    /// </summary>
    internal bool MoveUsingAdjacentWalls(
        int angle,
        int speed,
        bool allowHoles,
        bool topDown)
    {
        EnemyAdjacentWallProbe walls = topDown
            ? EnemyAdjacentWallResolver.Shared.ProbeTopDown(
                entity.Position,
                angle,
                point => IsAdjacentWallCollision(point, allowHoles))
            : EnemyAdjacentWallResolver.Shared.Probe(
                entity.Position,
                angle,
                point => IsAdjacentWallCollision(point, allowHoles));
        return MoveGivenAdjacentWalls(angle, speed, walls);
    }

    // ecom_applyGivenVelocityGivenAdjacentWalls returns hFF8D, not whether
    // the coordinates changed. In particular, a slide at SPEED_140 or above
    // still moves the enemy but does not keep a blocked charge running.
    internal bool MoveGivenAdjacentWalls(
        int angle,
        int speed,
        EnemyAdjacentWallProbe walls)
    {
        OracleObjectVelocity velocity = OracleObjectMovement.Shared.Velocity(speed, angle);
        Vector2 position = entity.Position;
        bool moved = false;

        int yWalls = walls.Bitset & 0x0c;
        if (yWalls == 0)
        {
            moved |= ApplySpeedComponent(ref position.Y, velocity.YFixed, speed);
        }
        else if (yWalls != 0x0c)
        {
            bool firstProbeBlocked = (yWalls & 0x08) != 0;
            int testedAngle = firstProbeBlocked ? angle : angle ^ 0x10;
            if (testedAngle < 0x11)
            {
                ApplyComponent(ref position.X, firstProbeBlocked ? 0x60 : -0x60);
                moved |= speed < 0x32;
            }
        }

        int xWalls = walls.Bitset & 0x03;
        if (xWalls == 0)
        {
            moved |= ApplySpeedComponent(ref position.X, velocity.XFixed, speed);
        }
        else if (xWalls != 0x03)
        {
            bool lastProbeBlocked = (xWalls & 0x01) != 0;
            int testedAngle =
                ((lastProbeBlocked ? angle - 0x10 : angle) + 0x08) & 0x1f;
            if (testedAngle < 0x11)
            {
                ApplyComponent(ref position.Y, lastProbeBlocked ? -0x60 : 0x60);
                moved |= speed < 0x32;
            }
        }

        entity.Position = position;
        entity.QueueRedraw();
        return moved;
    }

    private static bool ApplySpeedComponent(ref float position, int component, int speed)
    {
        int before = Mathf.FloorToInt(position);
        ApplyComponent(ref position, component);
        // @applySpeedComponent tests the change to the high byte first, then
        // the unsigned LOW byte of the velocity against $20/$60. The sign of
        // a fractional component therefore matters even without a pixel carry.
        return ((Mathf.FloorToInt(position) - before) & 0xff) != 0 ||
            (component & 0xff) >= (speed < 0x32 ? 0x20 : 0x60);
    }

    private static void ApplyComponent(ref float position, int component)
    {
        position = ((Mathf.FloorToInt(position * 256) + component) & 0xffff) / 256.0f;
    }

    private bool IsAdjacentWallCollision(
        Vector2I point,
        bool allowHoles)
    {
        if (point.X < 0 || point.X >= room.Width ||
            point.Y < 0 || point.Y >= room.Height ||
            room.IsSolidForEnemyMovement(
                point,
                holesAreWalls: !allowHoles))
        {
            return true;
        }
        return false;
    }
}
