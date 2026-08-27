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
    internal Vector2I Direction;
    internal int ZFixed;
    internal int SpeedZ;
    internal int SpeedRaw;

    internal CarriedObjectMotion(Vector2 position)
    {
        GroundPosition = position;
        Direction = Vector2I.Zero;
        ZFixed = 0;
        SpeedZ = 0;
        SpeedRaw = 0;
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
            ? 0
            : RingEffects.UsesStrongThrow(player.Inventory)
                ? bracelet.TossSpeedRaw
                : bracelet.SpeedRaw;
        Direction = releaseDirection;
        player.EndCarriedObjectPose();
    }

    internal bool AdvanceHorizontal(
        BombRecord throwing,
        Func<Vector2, bool> blocksMovement)
    {
        if (Direction == Vector2I.Zero)
            return false;
        Vector2 edge = GroundPosition + throwing.EdgeOffset(Direction);
        if (blocksMovement(edge))
        {
            Direction = Vector2I.Zero;
            SpeedRaw = 0;
            return true;
        }
        OracleObjectMovement.Shared.ApplySpeed(
            ref GroundPosition, SpeedRaw, DirectionAngle(Direction));
        return false;
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
            SpeedRaw = 0;
            Direction = Vector2I.Zero;
            return false;
        }

        SpeedZ = rebound;
        SpeedRaw = throwing.ReducedBounceSpeed(SpeedRaw);
        if (SpeedRaw == 0)
            Direction = Vector2I.Zero;
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
            LinkItemDatabase.Shared.BraceletLiftOffset(
                frame, DirectionIndex(player.FacingVector));
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
