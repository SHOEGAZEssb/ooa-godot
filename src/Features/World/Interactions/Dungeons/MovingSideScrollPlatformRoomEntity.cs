using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MOVING_SIDESCROLL_PLATFORM $a1:$06-$09.</summary>
internal sealed partial class MovingSideScrollPlatformRoomEntity :
    DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity
{
    private readonly MovingSideScrollPlatformRecord _record;
    private Vector2 _precisePosition;
    private int _commandIndex;
    private bool _linkRiding;

    public Node2D Node => this;
    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;
    internal bool LinkRiding => _linkRiding;
    internal int CommandIndex => _commandIndex;
    internal Vector2 PrecisePosition => _precisePosition;

    internal MovingSideScrollPlatformRoomEntity(
        DungeonObjectRecord placement,
        MovingSideScrollPlatformRecord record,
        DungeonInteractionVisual visual)
    {
        _record = record;
        Name =
            $"MovingSideScrollPlatform_{placement.SubId:x2}_{placement.Order}";
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
        MovingSideScrollPlatformCommand command = _record.Commands[_commandIndex];
        int angle = command.Direction switch
        {
            MovingSideScrollPlatformDirection.Up => 0x00,
            MovingSideScrollPlatformDirection.Right => 0x08,
            MovingSideScrollPlatformDirection.Down => 0x10,
            MovingSideScrollPlatformDirection.Left => 0x18,
            _ => throw new InvalidOperationException(
                $"Unsupported side-platform direction {command.Direction}.")
        };

        bool shouldMove = ShouldMove(command);
        if (shouldMove)
        {
            // interactionCodea1 carries Link before objectApplySpeed for
            // right/down/left. Upward platforms instead push him from
            // sidescrollingPlatformCommon after the platform has moved.
            if (_linkRiding &&
                command.Direction != MovingSideScrollPlatformDirection.Up)
            {
                frame.Player.ApplySideScrollMovingPlatformVelocity(
                    _record.Speed,
                    angle);
            }
            Position = OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition,
                _record.Speed,
                angle);
        }
        else
        {
            if (command.Direction is MovingSideScrollPlatformDirection.Up or
                MovingSideScrollPlatformDirection.Down)
            {
                SetCoordinateHigh(horizontal: false, command.Endpoint);
            }
            else
            {
                SetCoordinateHigh(horizontal: true, command.Endpoint);
            }
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            _commandIndex = (_commandIndex + 1) % _record.Commands.Length;
            if (_linkRiding)
            {
                // sidescrollPlatformFunc_5bfc copies both low bytes after
                // objectRunMovementScript selects the next command.
                frame.Player.SynchronizeMovingPlatformSubpixels(
                    _precisePosition);
            }
        }

        frame.Player.ResolveSideScrollPlatformContact(
            Position,
            _record.RadiusY,
            _record.RadiusX,
            angle,
            _linkRiding);
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private bool ShouldMove(MovingSideScrollPlatformCommand command) =>
        command.Direction switch
        {
            MovingSideScrollPlatformDirection.Up =>
                command.Endpoint < Mathf.FloorToInt(_precisePosition.Y),
            MovingSideScrollPlatformDirection.Right =>
                Mathf.FloorToInt(_precisePosition.X) < command.Endpoint,
            MovingSideScrollPlatformDirection.Down =>
                Mathf.FloorToInt(_precisePosition.Y) < command.Endpoint,
            MovingSideScrollPlatformDirection.Left =>
                command.Endpoint < Mathf.FloorToInt(_precisePosition.X),
            _ => false
        };

    private void SetCoordinateHigh(bool horizontal, int coordinate)
    {
        if (horizontal)
        {
            _precisePosition.X =
                coordinate +
                (_precisePosition.X - Mathf.Floor(_precisePosition.X));
        }
        else
        {
            _precisePosition.Y =
                coordinate +
                (_precisePosition.Y - Mathf.Floor(_precisePosition.Y));
        }
    }

    private void UpdateRiding(Player player)
    {
        _linkRiding = player.CheckSideScrollPlatformRide(
            Position,
            _record.RadiusY,
            _record.RadiusX);
    }
}
