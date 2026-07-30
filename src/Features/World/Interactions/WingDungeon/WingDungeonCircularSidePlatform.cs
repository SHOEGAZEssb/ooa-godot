using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_CIRCULAR_SIDESCROLL_PLATFORM $a4:$00-$02.</summary>
internal sealed partial class WingDungeonCircularSidePlatform :
    SpiritsGraveVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity
{
    private const int Speed = 0x1e;
    private static readonly Vector2 Center = new(0x78, 0x56);

    private Vector2 _precisePosition;
    private int _angle;
    private int _counter = 7;
    private bool _linkRiding;

    public Node2D Node => this;
    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;
    internal int Angle => _angle;
    internal int Counter => _counter;
    internal Vector2 PrecisePosition => _precisePosition;

    internal WingDungeonCircularSidePlatform(
        ObjectRecord record,
        VisualRecord visual)
    {
        int radialAngle = record.SubId switch
        {
            0 => 0x00,
            1 => 0x08,
            2 => 0x10,
            _ => throw new InvalidOperationException(
                $"{record.Source} uses unsupported circular-platform subid " +
                $"${record.SubId:x2}.")
        };
        _precisePosition =
            Center + OracleObjectMovement.Shared.Direction(radialAngle) * 0x35;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _angle = (radialAngle + 8) & 0x1f;
        Name = $"WingDungeonCircularPlatform_{record.SubId}";
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        InitializeVisual(visual, Position);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool wasLinkRiding = _linkRiding;
        UpdateRiding(frame.Player);
        if (_linkRiding && !wasLinkRiding)
        {
            frame.Player.SynchronizeMovingPlatformSubpixels(
                _precisePosition);
        }
        if (--_counter == 0)
        {
            _counter = 14;
            _angle = (_angle + 1) & 0x1f;
        }
        Vector2 previous = _precisePosition;
        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, Speed, _angle);
        Vector2 displacement = _precisePosition - previous;
        if (_linkRiding && !frame.Player.SideScrollAirborne)
            frame.Player.ApplyMovingPlatformDisplacement(displacement);
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void UpdateRiding(Player player)
    {
        float xTolerance = 8 + (player.SideScrollAirborne ? 4 : 5);
        _linkRiding =
            Math.Abs(player.Position.X - Position.X) <= xTolerance &&
            player.Position.Y < Position.Y - 10 &&
            Math.Abs(player.Position.Y - Position.Y) < 16;
    }
}
