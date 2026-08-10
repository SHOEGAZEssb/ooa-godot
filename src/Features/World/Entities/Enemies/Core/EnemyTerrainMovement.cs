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
        Vector2 velocity = OracleObjectMovement.Shared.Delta(speed, angle);
        Vector2 movement = Vector2.Zero;

        int yWalls = walls.Bitset & 0x0c;
        if (yWalls == 0)
        {
            movement.Y += velocity.Y;
        }
        else if (yWalls != 0x0c)
        {
            bool firstProbeBlocked = (yWalls & 0x08) != 0;
            int testedAngle = firstProbeBlocked ? angle : angle ^ 0x10;
            if (testedAngle < 0x11)
                movement.X += firstProbeBlocked ? 0.375f : -0.375f;
        }

        int xWalls = walls.Bitset & 0x03;
        if (xWalls == 0)
        {
            movement.X += velocity.X;
        }
        else if (xWalls != 0x03)
        {
            bool lastProbeBlocked = (xWalls & 0x01) != 0;
            int testedAngle =
                ((lastProbeBlocked ? angle - 0x10 : angle) + 0x08) & 0x1f;
            if (testedAngle < 0x11)
                movement.Y += lastProbeBlocked ? -0.375f : 0.375f;
        }

        if (movement == Vector2.Zero)
            return false;
        entity.Position += movement;
        entity.QueueRedraw();
        return true;
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
