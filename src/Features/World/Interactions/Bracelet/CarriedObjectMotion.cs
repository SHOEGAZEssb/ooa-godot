using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared bracelet-parent held, release, lateral throw, and bounce state.
/// Object handlers retain their source-specific update order and landing state.
/// </summary>
internal struct CarriedObjectMotion
{
    internal Vector2 GroundPosition;
    private int _angle;
    private bool _angleAssigned;
    internal int Angle
    {
        get => _angleAssigned ? _angle : 0xff;
        set
        {
            if (value is not (>= 0 and < 32) && value != 0xff)
                throw new ArgumentOutOfRangeException(nameof(value));
            _angle = value; _angleAssigned = true;
        }
    }
    internal Vector2I Direction
    {
        get => Angle == 0xff ? Vector2I.Zero : (Vector2I)OracleObjectMath.StrictCardinalVector(Angle & ObjectAngle.CardinalMask);
        set => Angle = value == Vector2I.Zero ? 0xff : DirectionAngle(value);
    }
    internal int ZFixed;
    internal int SpeedZ;
    internal int SpeedRaw;
    private ItemTilePassage? _tilePassage;

    internal CarriedObjectMotion(Vector2 position)
    {
        _angle = 0xff; _angleAssigned = true;
        GroundPosition = position;
        Direction = Vector2I.Zero;
        ZFixed = 0;
        SpeedZ = 0;
        SpeedRaw = ObjectSpeed.Speed0;
        _tilePassage = null;
    }

    internal void Hold(Player player)
    {
        Vector2I offset = HeldOffset(player);
        GroundPosition = player.Position + new Vector2(offset.X, 0);
        ZFixed = offset.Y << 8;
    }

    internal void Release(
        Player player,
        Vector2I releaseDirection,
        BraceletDatabaseRecord bracelet)
    {
        Vector2I offset = HeldOffset(player);
        GroundPosition =
            player.Position + new Vector2(offset.X, 0) + player.FacingVector;
        ZFixed = offset.Y << 8;
        SpeedZ = releaseDirection == Vector2I.Zero
            ? 0
            : bracelet.InitialSpeedZ;
        SpeedRaw = releaseDirection == Vector2I.Zero
            ? ObjectSpeed.Speed0
            : RingEffects.UsesStrongThrow(player.Inventory)
                ? bracelet.TossSpeedRaw
                : bracelet.SpeedRaw;
        Direction = releaseDirection;
        _tilePassage = null; // A release creates a fresh native bracelet child.
        player.EndCarriedObjectPose();
    }

    internal bool AdvanceHorizontal(
        BombRecord throwing,
        OracleRoomData room,
        Func<OracleObjectPosition, int, int, OracleObjectPosition> move,
        bool breakOnCollision = false)
    {
        if (Angle == 0xff)
            return false;
        // commonBombAndBraceletCode.s:itemUpdateThrowingLaterally. These
        // related-object proxies use weight $00 or $40, not weight $30's
        // four-corner tile path. Probe only high XY, wrapping both bytes.
        Vector2I offset = throwing.EdgeOffsets[(Angle & ObjectAngle.CardinalMask) >> 3];
        Vector2 edge = new(
            (byte)(OracleObjectPosition.HighByte(GroundPosition.X) + offset.X),
            (byte)(OracleObjectPosition.HighByte(GroundPosition.Y) + offset.Y));
        bool blocked = edge.Y < 0xb0 && room.IsSolid(edge) &&
            !(_tilePassage ??= new ItemTilePassage()).CanPass(room, edge, Angle);
        if (blocked)
        {
            if (breakOnCollision) return true;
            Direction = Vector2I.Zero;
        }
        // A newly blocked nonbreakable object retains its speed byte and
        // falls through objectApplySpeed with angle $ff (zero publication).
        // Subsequent $ff updates return above without publishing again.
        if ((room.TilesetFlags & (int)TilesetFlags.Sidescroll) == 0 || (Angle & 0x0f) != 0)
            GroundPosition = move(
                OracleObjectPosition.FromPixels(GroundPosition), SpeedRaw, Angle).PrecisePosition;
        return blocked;
    }

    internal bool AdvanceVertical(BraceletDatabaseRecord bracelet) =>
        OracleObjectMath.UpdateSpeedZ(
            ref ZFixed, ref SpeedZ, bracelet.Gravity);

    /// <returns>True while another bounce remains; false when settled.</returns>
    internal bool Bounce(BombRecord throwing)
    {
        int rebound = (-SpeedZ) >> 1;
        if (rebound > -0x80)
        {
            ZFixed = 0;
            SpeedZ = 0;
            SpeedRaw = ObjectSpeed.Speed0;
            Direction = Vector2I.Zero;
            return false;
        }

        SpeedZ = rebound;
        SpeedRaw = throwing.ReducedBounceSpeed(SpeedRaw);
        // itemBounce writes speed only. Retain the angle even when speed is
        // zero: the next live lateral visit still publishes zero velocity.
        return true;
    }

    internal static Rect2 CollisionBounds(
        Vector2 pixelPosition,
        BraceletDatabaseRecord bracelet) => new(
        pixelPosition - new Vector2(bracelet.RadiusX, bracelet.RadiusY),
        new Vector2(bracelet.RadiusX * 2, bracelet.RadiusY * 2));

    internal static Vector2I HeldOffset(Player player)
    {
        int frame = player.CarriedObjectAnimationFrame == 0 ? 2 : 3;
        return player.BraceletEntityOffset ??
            BraceletWeightDatabase.Shared.LiftOffset(
                player.BraceletObjectWeight, frame, DirectionIndex(player.FacingVector));
    }

    internal static Vector2 ThrowCollisionOffset(Vector2I direction) =>
        direction == Vector2I.Up ? new Vector2(0, -3)
        : direction == Vector2I.Right ? new Vector2(3, 0)
        : direction == Vector2I.Down ? new Vector2(0, 7)
        : direction == Vector2I.Left ? new Vector2(-3, 0)
        : Vector2.Zero;

    internal static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0
        : direction == Vector2I.Right ? 1
        : direction == Vector2I.Down ? 2
        : direction == Vector2I.Left ? 3
        : throw new ArgumentOutOfRangeException(nameof(direction));

    private static int DirectionAngle(Vector2I direction) =>
        direction == Vector2I.Up ? 0x00
        : direction == Vector2I.Right ? 0x08
        : direction == Vector2I.Down ? 0x10
        : direction == Vector2I.Left ? 0x18
        : throw new ArgumentOutOfRangeException(nameof(direction));
}
