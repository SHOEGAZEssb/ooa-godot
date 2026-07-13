using Godot;
using System;

namespace oracleofages;

public sealed class RoomCollision
{
    private static readonly Vector2[] LinkSamples =
    {
        new(-5, -2), new(5, -2), new(-5, 5), new(5, 5)
    };
    private static readonly Vector2[] AdjacentWallSamples =
    {
        new(-3, -3), new(2, -3), new(-3, 7), new(2, 7),
        new(-5, 0), new(-5, 5), new(4, 0), new(4, 5)
    };

    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly PushBlockController _pushBlocks;
    private readonly Func<Vector2, bool> _hasNeighborFor;

    public RoomCollision(
        RoomSession rooms,
        RoomEntityManager entities,
        PushBlockController pushBlocks,
        Func<Vector2, bool> hasNeighborFor)
    {
        _rooms = rooms;
        _entities = entities;
        _pushBlocks = pushBlocks;
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
        return _entities.BlocksLink(playerPosition) || _pushBlocks.BlocksLink(playerPosition);
    }

    public Vector2 ResolveMovement(Vector2 playerPosition, Vector2 movement, bool allowWallSlide)
    {
        if (movement == Vector2.Zero)
            return movement;

        int angle = GetMovementAngle(movement);
        int walls = CalculateAdjacentWallsBitset(playerPosition);
        if (allowWallSlide && TryAdjustCardinalAngle(angle, walls, out int adjustedAngle))
        {
            angle = adjustedAngle;
            movement = DirectionForAngle(angle) * movement.Length();
            walls = 0; // specialObjectUpdatePositionGivenVelocity clears e.
        }

        int relevantWalls = walls & BitsToCheck(angle);
        Vector2 resolved = movement;
        if ((relevantWalls & 0xf0) != 0)
            resolved.Y = 0.0f;
        if ((relevantWalls & 0x0f) != 0)
            resolved.X = 0.0f;
        if (resolved == Vector2.Zero || !CanApplyMovement(playerPosition + resolved))
            return Vector2.Zero;
        return resolved;
    }

    public bool IsPushingAgainstWall(
        Vector2 playerPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        bool pressingTowardWall = facing == Vector2I.Up && movementInput.Y < 0.0f
            || facing == Vector2I.Right && movementInput.X > 0.0f
            || facing == Vector2I.Down && movementInput.Y > 0.0f
            || facing == Vector2I.Left && movementInput.X < 0.0f;
        if (!pressingTowardWall)
            return false;

        int requiredWalls = facing == Vector2I.Up ? 0xc0
            : facing == Vector2I.Right ? 0x03
            : facing == Vector2I.Down ? 0x30
            : 0x0c;
        int walls = CalculateAdjacentWallsBitset(playerPosition);
        return (walls & requiredWalls) == requiredWalls;
    }

    private static int GetMovementAngle(Vector2 movement)
    {
        int horizontal = Mathf.Sign(movement.X);
        int vertical = Mathf.Sign(movement.Y);
        if (vertical < 0) return horizontal < 0 ? 28 : horizontal > 0 ? 4 : 0;
        if (vertical > 0) return horizontal < 0 ? 20 : horizontal > 0 ? 12 : 16;
        return horizontal < 0 ? 24 : 8;
    }

    private static Vector2 DirectionForAngle(int angle)
    {
        return angle switch
        {
            0 => Vector2.Up,
            8 => Vector2.Right,
            16 => Vector2.Down,
            24 => Vector2.Left,
            _ => Vector2.Zero
        };
    }

    private static int BitsToCheck(int angle)
    {
        return angle switch
        {
            0 => 0xcf,
            4 => 0xc3,
            8 => 0xf3,
            12 => 0x33,
            16 => 0x3f,
            20 => 0x3c,
            24 => 0xfc,
            28 => 0xcc,
            _ => 0xff
        };
    }

    private static bool TryAdjustCardinalAngle(int angle, int walls, out int adjustedAngle)
    {
        adjustedAngle = angle;
        switch (angle)
        {
            case 0:
                if ((walls & 0xc3) == 0x80) adjustedAngle = 8;
                else if ((walls & 0xcc) == 0x40) adjustedAngle = 24;
                else return false;
                return true;
            case 8:
                if ((walls & 0xc3) == 0x01) adjustedAngle = 0;
                else if ((walls & 0x33) == 0x02) adjustedAngle = 16;
                else return false;
                return true;
            case 16:
                if ((walls & 0x33) == 0x20) adjustedAngle = 8;
                else if ((walls & 0x3c) == 0x10) adjustedAngle = 24;
                else return false;
                return true;
            case 24:
                if ((walls & 0xcc) == 0x04) adjustedAngle = 0;
                else if ((walls & 0x3c) == 0x08) adjustedAngle = 16;
                else return false;
                return true;
            default:
                return false;
        }
    }

    private int CalculateAdjacentWallsBitset(Vector2 playerPosition)
    {
        int walls = 0;
        foreach (Vector2 offset in AdjacentWallSamples)
        {
            walls <<= 1;
            if (TileBlocksPoint(playerPosition + offset))
                walls |= 1;
        }

        // Intentional original-engine quirk: these are endpoint probes, not a
        // continuous outline. At Link y=$3f, room 0:56's four-pixel $1a rail
        // can sit between a side pair and be crossed. Keep this for parity;
        // a future optional collision-polish mode could fill those gaps.

        // Ages normalizes these two asymmetric patterns after calculating the
        // bitset (specialObjectUpdateAdjacentWallsBitset@data).
        return walls switch
        {
            0xdb => 0xc3,
            0xee => 0xcc,
            _ => walls
        };
    }

    private bool CanApplyMovement(Vector2 position)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        return position.X >= 0 && position.X < room.Width &&
            position.Y >= 0 && position.Y < room.Height &&
            !_entities.BlocksLink(position) &&
            !_pushBlocks.BlocksLink(position);
    }

    private bool TileBlocksPoint(Vector2 point)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        if (point.X < 0 || point.X >= room.Width || point.Y < 0 || point.Y >= room.Height)
            return !_hasNeighborFor(point);
        return room.IsSolid(point);
    }
}
