using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MOVING_PLATFORM $79, including Link riding displacement.</summary>
internal sealed partial class MovingPlatformRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity
{
    private readonly int _script;
    private readonly Vector2 _collisionRadii;
    private readonly DungeonInteractionDatabase _data;
    private int _state;
    private int _counter;
    private int _moveRemaining;
    private Vector2 _moveDirection;
    private Vector2 _precisePosition;
    private bool _linkRiding;

    public Node2D Node => this;
    internal int Script => _script;
    internal int Counter => _counter;
    internal bool LinkRiding => _linkRiding;
    internal Vector2 CollisionRadii => _collisionRadii;
    internal Vector2 PrecisePosition => _precisePosition;
    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;

    internal MovingPlatformRoomEntity(
        DungeonInteractionVisual visual,
        Vector2 position,
        int rawSubId,
        Vector2 collisionRadii,
        DungeonInteractionDatabase data)
    {
        _script = rawSubId >> 3;
        _collisionRadii = collisionRadii;
        _data = data;
        if (_script is not (0 or 1) ||
            _collisionRadii.X <= 0 ||
            _collisionRadii.Y <= 0)
        {
            throw new InvalidOperationException(
                $"Unsupported moving-platform subid ${rawSubId:x2}.");
        }
        Name = $"MovingPlatform_{_script}";
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        InitializeVisual(visual, position);
        _precisePosition = position;
        BeginWait();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // interactionCode79 updates wLinkRidingObject before it executes the
        // current wait/move script state. The containment check uses only the
        // objects' high coordinate bytes.
        Vector2 linkPoint = frame.Player.Position + Vector2.Down * 5.0f;
        bool touching =
            Mathf.Abs(linkPoint.X - Position.X) < _collisionRadii.X &&
            Mathf.Abs(linkPoint.Y - Position.Y) < _collisionRadii.Y;
        if (_linkRiding && !touching)
            _linkRiding = false;
        else if (!_linkRiding && touching)
            _linkRiding = true;

        Vector2 displacement = Vector2.Zero;
        if (_counter > 0)
        {
            _counter--;
        }
        else if (_moveRemaining > 0)
        {
            Vector2 previous = _precisePosition;
            Position = OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition,
                _data.Constant("platform-speed"),
                DirectionAngle(_moveDirection));
            displacement = _precisePosition - previous;
            _moveRemaining--;
            if (_moveRemaining == 0)
                BeginWait();
        }
        else
        {
            BeginMove();
        }

        if (_linkRiding && displacement != Vector2.Zero &&
            frame.Player.IsGroundedForFloorButton)
        {
            frame.Player.ApplyMovingPlatformDisplacement(displacement);
        }
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void BeginWait()
    {
        _counter = _data.Constant("platform-wait");
        _moveRemaining = 0;
    }

    private void BeginMove()
    {
        if (_script == 0)
        {
            _moveDirection = (_state++ & 1) == 0 ? Vector2.Up : Vector2.Down;
            _moveRemaining = 0x80;
            return;
        }

        if (_state == 0)
        {
            _state = 1;
            _moveDirection = Vector2.Left;
            _moveRemaining = 0x40;
        }
        else
        {
            _moveDirection = (_state++ & 1) == 1 ? Vector2.Right : Vector2.Left;
            _moveRemaining = 0x80;
        }
    }

    private static int DirectionAngle(Vector2 direction) =>
        direction == Vector2.Up ? 0x00 :
        direction == Vector2.Right ? 0x08 :
        direction == Vector2.Down ? 0x10 :
        direction == Vector2.Left ? 0x18 :
        throw new InvalidOperationException(
            $"Unsupported moving-platform direction {direction}.");
}
