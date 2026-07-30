using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MOVING_SIDESCROLL_PLATFORM $a1:$06-$09.</summary>
internal sealed partial class WingDungeonSideScrollPlatform :
    SpiritsGraveVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity
{
    private readonly WingDungeonSidePlatformRecord _record;
    private Vector2 _precisePosition;
    private int _commandIndex;
    private bool _linkRiding;

    public Node2D Node => this;
    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;
    internal bool LinkRiding => _linkRiding;
    internal int CommandIndex => _commandIndex;
    internal Vector2 PrecisePosition => _precisePosition;

    internal WingDungeonSideScrollPlatform(
        ObjectRecord placement,
        WingDungeonSidePlatformRecord record,
        VisualRecord visual)
    {
        _record = record;
        Name = $"WingDungeonSidePlatform_{placement.SubId:x2}_{placement.Order}";
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        InitializeVisual(visual, placement.Position);
        _precisePosition = placement.Position;
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
        WingDungeonPlatformCommand command = _record.Commands[_commandIndex];
        Vector2 previous = _precisePosition;
        int angle = command.Direction switch
        {
            WingDungeonPlatformDirection.Up => 0x00,
            WingDungeonPlatformDirection.Right => 0x08,
            WingDungeonPlatformDirection.Down => 0x10,
            WingDungeonPlatformDirection.Left => 0x18,
            _ => throw new InvalidOperationException(
                $"Unsupported side-platform direction {command.Direction}.")
        };
        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, _record.Speed, angle);
        if (Reached(command))
        {
            if (command.Direction is WingDungeonPlatformDirection.Up or
                WingDungeonPlatformDirection.Down)
            {
                _precisePosition.Y = command.Endpoint;
            }
            else
            {
                _precisePosition.X = command.Endpoint;
            }
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            _commandIndex = (_commandIndex + 1) % _record.Commands.Length;
        }

        Vector2 displacement = _precisePosition - previous;
        if (_linkRiding && !frame.Player.SideScrollAirborne &&
            displacement != Vector2.Zero)
        {
            frame.Player.ApplyMovingPlatformDisplacement(displacement);
        }
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private bool Reached(WingDungeonPlatformCommand command) =>
        command.Direction switch
        {
            WingDungeonPlatformDirection.Up =>
                _precisePosition.Y <= command.Endpoint,
            WingDungeonPlatformDirection.Right =>
                _precisePosition.X >= command.Endpoint,
            WingDungeonPlatformDirection.Down =>
                _precisePosition.Y >= command.Endpoint,
            WingDungeonPlatformDirection.Left =>
                _precisePosition.X <= command.Endpoint,
            _ => false
        };

    private void UpdateRiding(Player player)
    {
        float xTolerance = _record.RadiusX +
            (player.SideScrollAirborne ? 4 : 5);
        bool closeX = Math.Abs(player.Position.X - Position.X) <= xTolerance;
        bool aboveTop =
            player.Position.Y < Position.Y - _record.RadiusY - 2;
        bool colliding =
            Math.Abs(player.Position.Y - Position.Y) <
                _record.RadiusY + 8;
        _linkRiding = closeX && aboveTop && colliding;
    }
}
