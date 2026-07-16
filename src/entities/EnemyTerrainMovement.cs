using Godot;

namespace oracleofages;

/// <summary>
/// Shared four-corner enemy movement used by the original Zol/Gel handlers.
/// Species state machines still choose the angle, speed, and hole policy.
/// </summary>
internal sealed class EnemyTerrainMovement(Node2D entity, OracleRoomData room)
{
    public bool IsOnHazard =>
        room.GetTerrainInfo(entity.Position).Hazard != OracleRoomData.HazardType.None;

    public void MoveAtAngle(int angle, float speed, bool allowHoles)
    {
        Vector2 movement = OracleObjectMath.VectorFromAngle32(angle) * speed;
        Vector2 destination = entity.Position + movement;
        if (CanOccupy(destination, allowHoles))
            entity.Position = destination;
        else if (CanOccupy(entity.Position + new Vector2(movement.X, 0), allowHoles))
            entity.Position += new Vector2(movement.X, 0);
        else if (CanOccupy(entity.Position + new Vector2(0, movement.Y), allowHoles))
            entity.Position += new Vector2(0, movement.Y);
        entity.QueueRedraw();
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
            OracleRoomData.HazardType.Hole;
    }
}
