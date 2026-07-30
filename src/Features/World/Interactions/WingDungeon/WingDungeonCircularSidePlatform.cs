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
        Vector2I previousHigh = new(
            Mathf.FloorToInt(_precisePosition.X),
            Mathf.FloorToInt(_precisePosition.Y));
        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, Speed, _angle);
        if (_linkRiding)
        {
            Vector2I currentHigh = new(
                Mathf.FloorToInt(_precisePosition.X),
                Mathf.FloorToInt(_precisePosition.Y));
            frame.Player.ApplyMovingPlatformHighByteDisplacement(
                currentHigh - previousHigh);
        }
        frame.Player.ResolveSideScrollPlatformContact(
            Position,
            radiusY: 8,
            radiusX: 8,
            platformAngle: _angle,
            riding: _linkRiding);
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void UpdateRiding(Player player)
    {
        _linkRiding = player.CheckSideScrollPlatformRide(
            Position,
            radiusY: 8,
            radiusX: 8);
    }
}
