using Godot;
using System;

namespace oracleofages;

public sealed class RoomCollision
{
    private static readonly Vector2[] LinkSamples =
    {
        new(-5, -2), new(5, -2), new(-5, 5), new(5, 5)
    };

    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly Func<Vector2, bool> _hasNeighborFor;

    public RoomCollision(
        RoomSession rooms,
        RoomEntityManager entities,
        Func<Vector2, bool> hasNeighborFor)
    {
        _rooms = rooms;
        _entities = entities;
        _hasNeighborFor = hasNeighborFor;
    }

    public bool Collides(Vector2 playerPosition)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        foreach (Vector2 offset in LinkSamples)
        {
            Vector2 sample = playerPosition + offset;
            if (sample.X < 0 || sample.X >= room.Width || sample.Y < 0 || sample.Y >= room.Height)
            {
                if (!_hasNeighborFor(sample))
                    return true;
                continue;
            }
            if (room.IsSolid(sample))
                return true;
        }
        return _entities.BlocksLink(playerPosition);
    }
}
