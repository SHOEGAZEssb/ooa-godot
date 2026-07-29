using Godot;

namespace oracleofages;

/// <summary>
/// Shared four-corner enemy movement used by the original Zol/Gel handlers.
/// Species state machines still choose the angle, speed, and hole policy.
/// </summary>
internal sealed class EnemyTerrainMovement(Node2D entity, OracleRoomData room)
{
    public HazardType Hazard =>
        room.GetTerrainInfo(entity.Position).Hazard;

    public bool MoveAtAngle(int angle, int speed, bool allowHoles)
    {
        Vector2 movement = OracleObjectMovement.Shared.Delta(speed, angle);
        if (movement == Vector2.Zero)
            return false;

        Vector2 destination = entity.Position + movement;
        if (CanOccupy(destination, allowHoles))
            entity.Position = destination;
        else if (movement.X != 0 && movement.Y != 0 &&
            CanOccupy(entity.Position + new Vector2(movement.X, 0), allowHoles))
            entity.Position += new Vector2(movement.X, 0);
        else if (movement.X != 0 && movement.Y != 0 &&
            CanOccupy(entity.Position + new Vector2(0, movement.Y), allowHoles))
            entity.Position += new Vector2(0, movement.Y);
        else
            return false;
        entity.QueueRedraw();
        return true;
    }

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

    private bool CanOccupy(Vector2 center, bool allowHoles)
    {
        return CanOccupySample(center + new Vector2(-5, -4), allowHoles) &&
            CanOccupySample(center + new Vector2(5, -4), allowHoles) &&
            CanOccupySample(center + new Vector2(-5, 6), allowHoles) &&
            CanOccupySample(center + new Vector2(5, 6), allowHoles);
    }

    private bool CanOccupySample(Vector2 sample, bool allowHoles)
    {
        if (sample.X < 0 || sample.X >= room.Width ||
            sample.Y < 0 || sample.Y >= room.Height || room.IsSolid(sample))
        {
            return false;
        }

        return allowHoles || room.GetTerrainInfo(sample).Hazard !=
            HazardType.Hole;
    }

    private bool IsAdjacentWallCollision(
        Vector2I point,
        bool allowHoles)
    {
        if (point.X < 0 || point.X >= room.Width ||
            point.Y < 0 || point.Y >= room.Height ||
            room.IsSolid(point))
        {
            return true;
        }
        return !allowHoles &&
            room.GetTerrainInfo(point).Hazard == HazardType.Hole;
    }
}
