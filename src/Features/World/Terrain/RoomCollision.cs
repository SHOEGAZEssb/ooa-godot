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
            if (TileBlocksPoint(sample))
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
        if (allowWallSlide && SpecialObjectMovement.TryAdjustCardinalAngle(angle, walls, out int adjustedAngle))
        {
            angle = adjustedAngle;
            movement = DirectionForAngle(angle) * movement.Length();
            walls = 0; // specialObjectUpdatePositionGivenVelocity clears e.
        }

        return ResolveMaskedMovement(playerPosition, movement, angle, walls);
    }

    internal Vector2 ResolveNativeMovement(Vector2 position, int speed, int angle, bool allowWallSlide)
    {
        // specialObjectUpdatePositionGivenVelocity rejects bit7 before the
        // lookup, then adjusts the angle before publishing its velocity.
        if ((angle & 0x80) != 0) return Vector2.Zero;
        if (angle is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(angle), angle, "Link movement requires a source angle $00-$1f or bit7 set.");
        int walls = CalculateAdjacentWallsBitset(position);
        if (allowWallSlide && SpecialObjectMovement.TryAdjustCardinalAngle(angle, walls, out int adjustedAngle))
        {
            angle = adjustedAngle;
            walls = 0;
        }
        var velocity = NativeObjectMovement.Velocity(_entities.RuntimeState, speed, angle);
        Vector2 movement = new(velocity.XFixed / 256.0f, velocity.YFixed / 256.0f);
        return ResolveMaskedMovement(position, movement, angle, walls);
    }

    private Vector2 ResolveMaskedMovement(Vector2 playerPosition, Vector2 movement, int angle, int walls)
    {
        int relevantWalls = walls & SpecialObjectMovement.BitsToCheck(angle);
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

    /// <summary>
    /// Matches bombsBraceletParent's full directional
    /// w1Link.adjacentWallsBitset test. The Bracelet requires both endpoint
    /// bits on the faced edge before it derives the metatile probe position.
    /// </summary>
    internal bool HasFullWall(Vector2 playerPosition, Vector2I facing)
    {
        int requiredWalls = facing == Vector2I.Up ? 0xc0
            : facing == Vector2I.Right ? 0x03
            : facing == Vector2I.Down ? 0x30
            : facing == Vector2I.Left ? 0x0c
            : 0;
        return requiredWalls != 0 &&
            (CalculateAdjacentWallsBitset(playerPosition) & requiredWalls) ==
                requiredWalls;
    }

    internal int AdjacentWallsBitset(Vector2 playerPosition) =>
        CalculateAdjacentWallsBitset(playerPosition);

    internal bool TileBlocksPointForSidePlatform(Vector2 point)
    {
        // sidescrollPlatform_getTileCollisionBehindLink / Func_5b51 call
        // getTileCollisionsAtPosition: byte coordinates select a whole
        // metatile's raw collision byte, without quarter-tile normalization.
        var wrapped = new Vector2(
            unchecked((byte)Mathf.FloorToInt(point.X)),
            unchecked((byte)Mathf.FloorToInt(point.Y)));
        return _rooms.CurrentRoom.GetTerrainInfo(wrapped).Collision != 0;
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
            ObjectAngle.Up => Vector2.Up,
            ObjectAngle.Right => Vector2.Right,
            ObjectAngle.Down => Vector2.Down,
            ObjectAngle.Left => Vector2.Left,
            _ => Vector2.Zero
        };
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
        // Moving INTERAC$14 blocks clamp Link after objectApplySpeed in the
        // interaction pass. Rejecting movement against their old position
        // here adds a one-update lag compared with synchronized partners.
        return position.X >= 0 && position.X < room.Width &&
            position.Y >= 0 && position.Y < room.Height &&
            !_entities.BlocksLink(position);
    }

    private bool TileBlocksPoint(Vector2 point)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        if (point.X < 0 || point.X >= room.Width || point.Y < 0 || point.Y >= room.Height)
            return !_hasNeighborFor(point);
        return _entities.RuntimeState.ReadWramByte(WramAddress.wLinkRaisedFloorOffset) != 0
            ? room.IsSolidForRaisedFloorLink(point)
            : room.IsSolid(point);
    }
}
