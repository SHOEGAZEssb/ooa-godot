using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_CIRCULAR_SIDESCROLL_PLATFORM $a4:$00-$02.</summary>
internal sealed partial class CircularSideScrollPlatformRoomEntity :
    DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IPlayerRideableRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly CircularSideScrollPlatformRecord _record;

    private Vector2 _precisePosition;
    private int _angle;
    private int _counter;
    private bool _initialized;
    private bool _linkRiding;
    private MovingPlatformRidingState? _riding;
    internal void BindRidingState(MovingPlatformRidingState riding) => _riding = riding;

    public Node2D Node => this;
    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;
    internal bool LinkRiding => _linkRiding;
    internal Vector2 PrecisePosition => _precisePosition;
    internal int Angle => _angle;
    internal int Counter => _counter;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal CircularSideScrollPlatformRoomEntity(
        DungeonObjectRecord record,
        DungeonInteractionVisual visual,
        CircularSideScrollPlatformRecord profile)
    {
        _record = profile;
        _precisePosition = record.Position;
        Name = $"CircularSideScrollPlatform_{record.SubId}";
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        InitializeVisual(visual, record.Position);
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance(frame.Player);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("INTERAC$a4 preload requires live Link for state0 contact checks.");

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException("INTERAC$a4 preload requires live Link for state0 contact checks.");
        if (!_initialized) Advance(player);
        return ScreenTransitionPresentation.Visible;
    }

    private void Advance(Player player)
    {
        bool wasLinkRiding = _linkRiding;
        UpdateRiding(player);
        _riding?.PublishSideScroll(this, _linkRiding);
        if (_linkRiding && !wasLinkRiding)
        {
            player.SynchronizeMovingPlatformSubpixels(
                _precisePosition);
        }
        if (!_initialized)
        {
            // State0 checks contact with the pending object's zero radii,
            // installs its circular position and yields without moving.
            _initialized = true;
            _precisePosition = _record.Center +
                (Vector2)OracleObjectMovement.Shared.CircleArcOffset(_record.Radius, _record.InitialAngle);
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            _angle = (_record.InitialAngle + _record.TangentOffset) & ObjectAngle.Mask;
            _counter = _record.InitialCounter;
            Visible = true;
            ResolveContact(player);
            return;
        }
        if (--_counter == 0)
        {
            _counter = _record.TurnFrames;
            _angle = (_angle + 1) & ObjectAngle.Mask;
        }
        Vector2I previousHigh = new(
            Mathf.FloorToInt(_precisePosition.X),
            Mathf.FloorToInt(_precisePosition.Y));
        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, _record.Speed, _angle);
        if (_linkRiding)
        {
            Vector2I currentHigh = new(
                Mathf.FloorToInt(_precisePosition.X),
                Mathf.FloorToInt(_precisePosition.Y));
            player.ApplyMovingPlatformHighByteDisplacement(
                currentHigh - previousHigh);
        }
        ResolveContact(player);
        QueueRedraw();
    }

    private void ResolveContact(Player player) =>
        player.ResolveSideScrollPlatformContact(
            Position,
            radiusY: _record.CollisionRadius,
            radiusX: _record.CollisionRadius,
            platformAngle: _angle,
            riding: _linkRiding);

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void UpdateRiding(Player player)
    {
        _linkRiding = player.CheckSideScrollPlatformRide(
            Position,
            radiusY: _initialized ? _record.CollisionRadius : 0,
            radiusX: _initialized ? _record.CollisionRadius : 0);
    }
}
