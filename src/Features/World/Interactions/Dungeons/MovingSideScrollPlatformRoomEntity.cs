using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MOVING_SIDESCROLL_PLATFORM $a1:$00-$0e.</summary>
internal sealed partial class MovingSideScrollPlatformRoomEntity :
    DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity
{
    private readonly MovingSideScrollPlatformRecord _record;
    private Vector2 _precisePosition;
    private int _commandIndex;
    private int _angle;
    private int _waitCounter;
    private bool _initialized;
    private bool _linkRiding;

    public Node2D Node => this;
    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;
    internal bool LinkRiding => _linkRiding;
    internal int CommandIndex => _commandIndex;
    internal int WaitCounter => _waitCounter;
    internal int CurrentAnimationIndex => AnimationIndex;
    internal Vector2 PrecisePosition => _precisePosition;

    internal MovingSideScrollPlatformRoomEntity(
        MovingSideScrollPlatformPlacement placement,
        MovingSideScrollPlatformRecord record,
        DungeonInteractionVisual visual)
    {
        _record = record;
        Name =
            $"MovingSideScrollPlatform_{placement.SubId:x2}_{placement.Order}";
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        // Directions $01-$03 use 48-pixel OAM layouts. The fixed 32x32
        // compositor clips their long axis and makes the source collision
        // radii appear oversized, so preserve the complete positioned frame.
        InitializeVisual(
            visual,
            placement.Position,
            record.Direction,
            positionedOam: true);
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
        if (!_initialized)
        {
            // objectLoadMovementScript falls through to the first command on
            // state zero, but interactionCodea1 does not apply speed until
            // the following update.
            _initialized = true;
            LoadCurrentCommand();
            ResolveContact(frame);
            return;
        }

        MovingSideScrollPlatformCommand command = _record.Commands[_commandIndex];
        if (command.Direction == MovingSideScrollPlatformDirection.Wait)
        {
            if (--_waitCounter == 0)
                AdvanceCommand();
            ResolveContact(frame);
            return;
        }

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
                    _angle);
            }
            Position = OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition,
                _record.Speed,
                _angle);
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
            AdvanceCommand();
            if (_linkRiding)
            {
                // sidescrollPlatformFunc_5bfc copies both low bytes after
                // objectRunMovementScript selects the next command.
                frame.Player.SynchronizeMovingPlatformSubpixels(
                    _precisePosition);
            }
        }

        ResolveContact(frame);
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

    private void AdvanceCommand()
    {
        _commandIndex = (_commandIndex + 1) % _record.Commands.Length;
        LoadCurrentCommand();
    }

    private void LoadCurrentCommand()
    {
        MovingSideScrollPlatformCommand command = _record.Commands[_commandIndex];
        if (command.Direction == MovingSideScrollPlatformDirection.Wait)
        {
            _waitCounter = command.Endpoint;
            return;
        }
        _angle = command.Direction switch
        {
            MovingSideScrollPlatformDirection.Up => 0x00,
            MovingSideScrollPlatformDirection.Right => 0x08,
            MovingSideScrollPlatformDirection.Down => 0x10,
            MovingSideScrollPlatformDirection.Left => 0x18,
            _ => throw new InvalidOperationException(
                $"Unsupported side-platform direction {command.Direction}.")
        };
    }

    private void ResolveContact(RoomEntityFrame frame)
    {
        frame.Player.ResolveSideScrollPlatformContact(
            Position,
            _record.RadiusY,
            _record.RadiusX,
            _angle,
            _linkRiding);
        QueueRedraw();
    }

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
