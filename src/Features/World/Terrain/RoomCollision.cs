using Godot;
using System;

namespace Oracles;

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

    private readonly Func<OracleRoomData> _room;
    private readonly Func<Vector2, bool, bool> _blocksLink;
    private readonly OracleRuntimeState _memory;
    internal OracleRuntimeState RuntimeState => _memory;
    private readonly bool _normalizeCornerWalls;
    private readonly Func<Vector2, bool> _hasNeighborFor;

    public RoomCollision(
        RoomSession rooms,
        RoomEntityManager entities,
        PushBlockController pushBlocks,
        Func<Vector2, bool> hasNeighborFor,bool normalizeCornerWalls = true)
        : this(() => rooms.CurrentRoom,
            (position, beforeInteractions) => beforeInteractions
                ? entities.BlocksLink(position, beforeInteractions: true)
                : entities.BlocksLink(position) || pushBlocks.BlocksLink(position),
            entities.RuntimeState, hasNeighborFor, normalizeCornerWalls) { }

    internal RoomCollision(Func<OracleRoomData> room,
        Func<Vector2, bool, bool> blocksLink, OracleRuntimeState memory,
        Func<Vector2, bool> hasNeighborFor, bool normalizeCornerWalls)
    {
        _room = room;
        _blocksLink = blocksLink;
        _memory = memory;
        _hasNeighborFor = hasNeighborFor;
        _normalizeCornerWalls = normalizeCornerWalls;
    }

    public bool Collides(Vector2 playerPosition)
    {
        OracleRoomData room = _room();
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
        return _blocksLink(playerPosition, false);
    }

    internal void UpdateLinkOnChest()
    {
        OracleRoomData room = _room();
        OracleRuntimeState state = _memory;
        // bank0.s:checkAndUpdateLinkOnChest. This is a shared collision-buffer
        // write, not a Link-only exception. Restore the current tile's collision
        // on departure, even if another writer replaced the chest meanwhile.
        byte active = state.ReadField("wActiveTilePos");
        byte chest = state.ReadField("wLinkOnChest");
        if (chest == 0)
        {
            if (state.ReadField("wActiveTileIndex") != ChestDatabase.ClosedTile) return;
            state.SetField("wLinkOnChest", active);
            room.SetPackedTileCollision(active, 0);
        }
        else if (active != chest)
        {
            room.SetPackedTileCollision(chest, null);
            state.SetField("wLinkOnChest", 0);
        }
    }

    internal byte UpdateActiveLinkTile(Vector2 position, bool airborne)
    {
        OracleRoomData room = _room();
        OracleRuntimeState state = _memory;
        byte previousPosition = state.ReadField("wActiveTilePos");
        state.SetField("wIsTileSlippery",0);
        if (airborne)
        {
            state.SetField("wActiveTileType",0);
            state.SetField("wStandingOnTileCounter",0);
            return previousPosition;
        }
        Vector2 feet = new(unchecked((byte)Mathf.FloorToInt(position.X)),
            unchecked((byte)(Mathf.FloorToInt(position.Y)+5)));
        byte packed = (byte)room.GetPackedPosition(feet), tile = room.ReadNativeLayoutByte(packed,state);
        int count = packed == previousPosition && tile == state.ReadField("wActiveTileIndex")
            ? state.ReadField("wStandingOnTileCounter") : 0;
        state.SetField("wActiveTilePos",packed);
        state.SetField("wActiveTileIndex",tile);
        state.SetField("wStandingOnTileCounter",unchecked((byte)(count + 1)));
        state.SetField("wLastActiveTileType",state.ReadField("wActiveTileType"));
        state.SetField("wActiveTileType",TilePropertyDatabase.For(room.AssetRoot).NativeTileType(room.ActiveCollisions,tile));
        return previousPosition;
    }

    internal void UpdateSideScrollActiveTile(Vector2 position)
    {
        OracleRoomData room = _room();
        var types = TilePropertyDatabase.For(room.AssetRoot);
        Vector2 current = new(unchecked((byte)Mathf.FloorToInt(position.X)),unchecked((byte)Mathf.FloorToInt(position.Y)));
        byte active = room.ReadNativeLayoutByte((byte)room.GetPackedPosition(current),_memory);
        current.Y = unchecked((byte)((int)current.Y + 8));
        _memory.SetField("wActiveTileIndex",active);
        _memory.SetField("wActiveTileType",types.NativeTileType(room.ActiveCollisions,active));
        _memory.SetField("wLastActiveTileType",types.NativeTileType(room.ActiveCollisions,
            room.ReadNativeLayoutByte((byte)room.GetPackedPosition(current),_memory)));
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

    internal Vector2 ResolveNativeMovement(Vector2 position, int speed, int angle, bool allowWallSlide,
        int? adjacentWalls = null)
    {
        // specialObjectUpdatePositionGivenVelocity rejects bit7 before the
        // lookup, then adjusts the angle before publishing its velocity.
        if ((angle & 0x80) != 0) return Vector2.Zero;
        if (angle is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(angle), angle, "Link movement requires a source angle $00-$1f or bit7 set.");
        // Terrain motion consumes the preceding Link wall publication before
        // linkState01 refreshes it for ordinary movement and item contacts.
        int walls = adjacentWalls ?? CalculateAdjacentWallsBitset(position);
        if (allowWallSlide && SpecialObjectMovement.TryAdjustCardinalAngle(angle, walls, out int adjustedAngle))
        {
            angle = adjustedAngle;
            walls = 0;
        }
        var velocity = NativeObjectMovement.Velocity(_memory, speed, angle);
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

    internal static bool IsPushingAgainstWall(
        int walls,
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
        return _room().GetTerrainInfo(wrapped).Collision != 0;
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
        if (!_normalizeCornerWalls) return walls;
        return walls switch
        {
            0xdb => 0xc3,
            0xee => 0xcc,
            _ => walls
        };
    }

    private bool CanApplyMovement(Vector2 position)
    {
        OracleRoomData room = _room();
        // Moving INTERAC$14 blocks clamp Link after objectApplySpeed in the
        // interaction pass. Rejecting movement against their old position
        // here adds a one-update lag compared with synchronized partners.
        // wScrollMode bit0 disables screenTransitionState2's boundary owner.
        // Native escape input deliberately walks Link beyond the viewport;
        // objectApplySpeed itself does not clamp him to the room rectangle.
        bool nativeBoundsDisabled=(_memory.ReadField("wScrollMode")&1)==0;
        return (nativeBoundsDisabled||position.X >= 0 && position.X < room.Width &&
            position.Y >= 0 && position.Y < room.Height) &&
            !_blocksLink(position, true);
    }

    private bool TileBlocksPoint(Vector2 point)
    {
        OracleRoomData room = _room();
        if (point.X < 0 || point.X >= room.Width || point.Y < 0 || point.Y >= room.Height)
        {
            // bank0.s:loadRoomCollisions fills the border with $ff. Link's
            // checkGivenCollision_allowHoles masks it to special collision
            // $1f (passable). A synthetic floor here lands side-view Link at
            // Y=$a9 before his next active/below-tile probes can respawn him.
            if ((room.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0)
                return false;
            return !_hasNeighborFor(point);
        }
        return _memory.ReadField("wLinkRaisedFloorOffset") != 0
            ? room.IsSolidForRaisedFloorLink(point)
            : room.IsSolid(point);
    }
}
