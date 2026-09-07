using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_RAFT $e6 and SPECIALOBJECT_RAFT $13, including their shared
/// w1Companion ownership and scrolling handoff.
/// </summary>
internal sealed partial class RaftRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerForcedMovement,
    IPlayerRideableRoomEntity, IPlayerScreenTransitionRoomEntity,
    IRoomEntityLifetime, IPlayerRestriction
{
    private static readonly Vector2[] RaftCollisionSamples =
    [
        new(-5, -6), new(4, -6), new(-5, 5), new(4, 5),
        new(-6, -5), new(-6, 4), new(5, -5), new(5, 4)
    ];
    private static readonly Vector2[] LinkCollisionSamples =
    [
        // link.s:calculateAdjacentWallsBitset's cumulative Y/X offsets.
        new(-3, -3), new(2, -3), new(-3, 7), new(2, 7),
        new(-5, 0), new(-5, 5), new(4, 0), new(4, 5)
    ];
    private static readonly Vector2[] DismountOffsets =
    [new(0, -9), new(8, -3), new(0, 8), new(-9, -3)];

    private OracleRoomData _room;
    private readonly OracleRuntimeState _runtime;
    private readonly RaftBehavior _behavior;
    private readonly EnemyAnimationPlayer _waitingAnimation;
    private readonly EnemyAnimationPlayer _mountedAnimation;
    private OracleRoomData? _transitionDestination;
    private Vector2 _precisePosition;
    private int _group;
    private int _roomId;
    private int _direction;
    private int _angle = 0xff;
    private int _dismountAngle = 0xff;
    private int _dismountCounter;
    private int _stateCounter;
    private int _forcedWalkCounter;
    private RaftPhase _phase;
    private bool _cutsceneControlled;
    private bool _supportsLink;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool DisablesSword => false;
    public bool DisablesItems => false;
    public bool DisablesMovement => false;
    public bool DisablesMenus => _phase == RaftPhase.Dismounting;
    public bool DisablesRingTransformations => _phase is RaftPhase.Mounting or RaftPhase.Riding;
    internal bool UsesSpecialObjectSlot => _phase is RaftPhase.Mounting or RaftPhase.Riding or RaftPhase.Dismounting;
    public bool LinkRiding => _phase == RaftPhase.Riding;
    bool IPlayerRideableRoomEntity.LinkRiding => _supportsLink ||
        _phase is RaftPhase.Mounting or RaftPhase.Riding or RaftPhase.Dismounting;
    public bool ControlsPlayerScreenTransition => LinkRiding;
    public bool BypassesScreenTransitionInputGate => false;
    public Vector2 ScreenTransitionPosition => _precisePosition;
    internal int Direction => _direction;
    internal int Angle => _angle;
    internal Vector2 PrecisePosition => _precisePosition;
    private bool UsesMountedAnimation => _phase is RaftPhase.Riding or RaftPhase.Dismounting;
    internal int AnimationIndex => UsesMountedAnimation
        ? _mountedAnimation.AnimationIndex
        : _waitingAnimation.AnimationIndex;

    internal RaftRoomEntity(
        RaftSpawn spawn,
        OracleRoomData room,
        RaftBehavior behavior,
        OracleRuntimeState runtime)
    {
        _room = room;
        _behavior = behavior;
        _runtime = runtime;
        _group = spawn.Group;
        _roomId = spawn.Room;
        _precisePosition = spawn.Position;
        _direction = spawn.Direction;
        _phase = spawn.Riding ? RaftPhase.Riding : RaftPhase.Waiting;
        _dismountCounter = behavior.DismountDelay;
        _stateCounter = behavior.DismountWaitFrames;

        Image image = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{behavior.Sprite}.png");
        _waitingAnimation = new EnemyAnimationPlayer(this, 2);
        _waitingAnimation.Load(
            image, behavior.WaitingAnimations, behavior.WaitingTileBase,
            behavior.WaitingPalette, positionedOam: true);
        _mountedAnimation = new EnemyAnimationPlayer(this, 2);
        _mountedAnimation.Load(
            image, behavior.MountedAnimations, 0, behavior.MountedPalette,
            positionedOam: true,
            animationSourceOffsets: behavior.MountedSourceOffsets);
        SetDirectionAnimation();
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        // interactionCodee6 uses visible $83; func_410d @ridingRaft uses
        // $c3. Both select source priority $03, below Link's $c1.
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        Visible = true;
        Name = $"Raft_{_group:x1}_{_roomId:x2}";
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (LinkRiding && !player.RaftRespawning)
        {
            Visible = true;
            player.SetRaftRidePosition(
                _precisePosition, _direction,
                _mountedAnimation.CurrentParameter, Vector2.Zero);
        }
        else if (_forcedWalkCounter > 0)
        {
            Vector2I direction = DirectionVector(_direction);
            player.AdvanceForcedRoomEntryMovement(direction);
            _forcedWalkCounter--;
            if (_forcedWalkCounter == 0)
                player.EndForcedRoomEntryMovement();
        }
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        switch (_phase)
        {
            case RaftPhase.Waiting:
                UpdateWaiting(frame.Player);
                break;
            case RaftPhase.Mounting:
                // The interaction allocates w1Companion after the special-object
                // pass. State $00 initializes it on the following update.
                _phase = RaftPhase.Riding;
                _stateCounter = _behavior.DismountWaitFrames;
                _dismountCounter = 0;
                _dismountAngle = 0;
                _mountedAnimation.SetAnimation(0);
                ZIndex = NpcCharacter.FixedLowPriorityZIndex;
                frame.Player.BeginRaftRide(_precisePosition, _direction);
                SavePosition(frame.Player);
                break;
            case RaftPhase.Riding:
                UpdateRiding(frame.Player);
                break;
            case RaftPhase.Dismounting:
                if (--_stateCounter == 0)
                {
                    frame.Player.SetLocalRespawnPosition(
                        OracleObjectMath.ToPixelPosition(
                            frame.Player.PrecisePosition));
                    // State $02 falls through to $03 on its zero update;
                    // the replacement interaction initializes later that pass.
                    CompanionRuntimeState.Clear(_runtime, CompanionRuntimeState.RaftId);
                    _phase = RaftPhase.Waiting;
                    _supportsLink = false;
                    ZIndex = NpcCharacter.FixedLowPriorityZIndex;
                    SetDirectionAnimation();
                }
                break;
            case RaftPhase.Wrecked:
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported raft phase {_phase}.");
        }
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    private void UpdateWaiting(Player player)
    {
        _supportsLink = false;
        _waitingAnimation.Advance();
        int dx = Math.Abs((Mathf.FloorToInt(player.PrecisePosition.X) & 0xff) -
            (Mathf.FloorToInt(_precisePosition.X) & 0xff));
        int dy = Math.Abs(((Mathf.FloorToInt(player.PrecisePosition.Y) + 5) & 0xff) -
            (Mathf.FloorToInt(_precisePosition.Y) & 0xff));
        if (dx >= _behavior.MountRadius || dy >= _behavior.MountRadius)
            return;
        if (player.TopDownAirborne &&
            ((player.TopDownAirZ & 0xff) < 0xfd || player.TopDownAirSpeedZ < 0))
        {
            return;
        }

        _supportsLink = true;
        player.DisableInstruments(_behavior.InstrumentLockFrames);
        if (dx >= _behavior.InnerMountRadius || dy >= _behavior.InnerMountRadius)
            return;

        _phase = RaftPhase.Mounting;
        CompanionRuntimeState.Begin(
            _runtime, CompanionRuntimeState.RaftId, _roomId,
            _precisePosition, _direction);
    }

    private void UpdateRiding(Player player)
    {
        if (_cutsceneControlled)
        {
            SynchronizeRide(player);
            return;
        }

        if (player.RaftRespawning)
        {
            // @respawning writes only yh/xh and hides the raft while Link's
            // LINK_STATE_RESPAWNING handler owns his position and animation.
            Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
            _precisePosition = OracleObjectMath.ToPixelPosition(player.LocalRespawnPosition) + fraction;
            CompanionRuntimeState.Update(_runtime, CompanionRuntimeState.RaftId,
                _roomId, _precisePosition, _direction);
            Visible = false;
            return;
        }

        // updateGameKeysPressed rejects any opposite directional pair, even
        // when a third key is held; vector cancellation would move on that axis.
        bool oppositeKeys = Input.IsActionPressed("move_left") && Input.IsActionPressed("move_right") ||
            Input.IsActionPressed("move_up") && Input.IsActionPressed("move_down");
        int angle = player.IsDying || oppositeKeys ? 0xff : AngleForInput(Input.GetVector(
            "move_left", "move_right", "move_up", "move_down"));
        int newDirection = CompanionMovement.DirectionForAngle(angle, _direction);
        if (newDirection != _direction)
        {
            _direction = newDirection;
            SetDirectionAnimation();
        }
        else
        {
            _mountedAnimation.Advance();
        }

        int knockbackAngle = player.AdvanceRaftKnockback();
        if (knockbackAngle != 0xff)
        {
            ApplyMovement(
                _behavior.KnockbackSpeed, knockbackAngle,
                CalculateRaftWalls());
            ResetDismount();
            SynchronizeRide(player);
            return;
        }

        _angle = angle;
        if (angle == 0xff || player.RaftMovementImmobilized)
        {
            ResetDismount();
            SynchronizeRide(player);
            return;
        }

        int walls = CalculateRaftWalls();
        Vector2 before = _precisePosition;
        ApplyMovement(_behavior.Speed, angle, walls);
        if (_precisePosition != before)
        {
            ResetDismount();
            SynchronizeRide(player);
            return;
        }

        if (_dismountAngle != angle)
        {
            _dismountAngle = angle;
            SynchronizeRide(player);
            return;
        }
        _dismountCounter = (_dismountCounter - 1) & 0xff;
        if (_dismountCounter != 0)
        {
            SynchronizeRide(player);
            return;
        }
        if (!TryDismount(player))
            ResetDismount();
        else
            return;
        SynchronizeRide(player);
    }

    private bool TryDismount(Player player)
    {
        // @positionUnchanged decrements e from angle to direction. Diagonal
        // input uses the retained cardinal facing, without a cardinal-only gate.
        int direction = _direction;
        Vector2 destination = OracleObjectMath.ToPixelPosition(_precisePosition) + DismountOffsets[direction];
        if (!CanDismountAt(destination))
            return false;

        _direction = direction;
        _angle = direction * 8;
        _phase = RaftPhase.Dismounting;
        _forcedWalkCounter = _behavior.DismountWalkFrames;
        Vector2I movement = DirectionVector(direction);
        player.EndRaftRide(player.PrecisePosition, direction);
        player.BeginForcedRoomEntryMovement(movement);
        SavePosition(player);
        return true;
    }

    internal bool CanDismountAt(Vector2 destination)
    {
        // specialObjectCode_raft checks the destination's wRoomCollisions
        // byte before calculateAdjacentWallsBitset. Collision $10 (holes,
        // water, and lava) is intentionally non-solid to ordinary pixel
        // probes, but it is not a valid raft landing.
        int collision = _room.GetTerrainInfo(destination).Collision;
        return (collision == 0 || collision == _behavior.DismountCollision) &&
            CalculateLinkWalls(destination) == 0;
    }

    private void ResetDismount()
    {
        _dismountAngle = 0xff;
        _dismountCounter = _behavior.DismountDelay;
    }

    private void SynchronizeRide(Player player)
    {
        CompanionRuntimeState.Update(
            _runtime, CompanionRuntimeState.RaftId, _roomId,
            _precisePosition, _direction);
        player.SetRaftRidePosition(
            _precisePosition, _direction,
            _mountedAnimation.CurrentParameter, Vector2.Zero);
    }

    internal void BeginRaftwreckControl(Player player, Vector2 position)
    {
        if (!LinkRiding)
            throw new InvalidOperationException("Raftwreck requires Link to be riding the raft.");
        _cutsceneControlled = true;
        CopyRaftwreckPosition(position);
        SynchronizeRide(player);
    }

    internal void SetRaftwreckPosition(Player player, Vector2 position, int direction)
    {
        if (!_cutsceneControlled)
            throw new InvalidOperationException("Raftwreck does not own this raft.");
        CopyRaftwreckPosition(position);
        _direction = direction;
        // The cutscene's setLinkDirection writes both wLinkObjectIndex and
        // w1Link; ordinary func_410d position synchronization does not.
        player.Face(DirectionVector(direction));
        SynchronizeRide(player);
    }

    private void CopyRaftwreckPosition(Vector2 position)
    {
        // interactionCode9b copies only yh/xh from its moving controller.
        Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
        _precisePosition = OracleObjectMath.ToPixelPosition(position) + fraction;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    internal void FinishRaftwreck(Player player)
    {
        _cutsceneControlled = false;
        CompanionRuntimeState.Clear(_runtime, CompanionRuntimeState.RaftId);
        player.EndRaftRide(_precisePosition, _direction);
        _phase = RaftPhase.Wrecked;
        _supportsLink = false;
    }

    internal void CancelRaftwreckControl(Player player)
    {
        if (!_cutsceneControlled)
            return;
        _cutsceneControlled = false;
        SynchronizeRide(player);
    }

    private void SavePosition(Player player)
    {
        Vector2 pixels = OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.SetLastAnimalMountPosition(_runtime, pixels);
        CompanionRuntimeState.Remember(
            _runtime, CompanionRuntimeState.RaftId, _group, _roomId, pixels);
        player.SetLocalRespawnPosition(pixels, DirectionVector(_direction));
    }

    private int CalculateRaftWalls()
    {
        int walls = 0;
        for (int index = 0; index < RaftCollisionSamples.Length; index++)
        {
            Vector2 point = OracleObjectMath.ToPixelPosition(_precisePosition) + RaftCollisionSamples[index];
            if (point.X >= 0 && point.X < _room.Width &&
                point.Y >= 0 && point.Y < _room.Height &&
                _room.GetMetatile(point) != 0 &&
                Array.IndexOf(_behavior.ValidTiles, _room.GetMetatile(point)) < 0)
            {
                walls |= 1 << (7 - index);
            }
        }
        return walls;
    }

    private int CalculateLinkWalls(Vector2 position)
    {
        int walls = 0;
        for (int index = 0; index < LinkCollisionSamples.Length; index++)
        {
            if (IsSolidForLink(position + LinkCollisionSamples[index]))
                walls |= 1 << (7 - index);
        }
        return walls;
    }

    private bool IsSolidForLink(Vector2 point)
    {
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height)
            return true;
        return _room.IsSolid(point);
    }

    private void ApplyMovement(int speed, int angle, int walls)
    {
        int? adjustedAngle = AdjustAngleForTileEdge(angle, walls);
        int movementAngle = adjustedAngle ?? angle;
        int[] masks =
        [
            0xcf,0xc3,0xc3,0xc3,0xc3,0xc3,0xc3,0xc3,
            0xf3,0x33,0x33,0x33,0x33,0x33,0x33,0x33,
            0x3f,0x3c,0x3c,0x3c,0x3c,0x3c,0x3c,0x3c,
            0xfc,0xcc,0xcc,0xcc,0xcc,0xcc,0xcc,0xcc
        ];
        // specialObjectUpdatePositionGivenVelocity clears e after a successful
        // @tileEdgeAdjust; the original wall mask no longer suppresses either axis.
        int blocked = adjustedAngle.HasValue ? 0 : walls & masks[movementAngle];
        Vector2 candidate = _precisePosition;
        OracleObjectMovement.Shared.ApplySpeed(ref candidate, speed, movementAngle);
        Vector2 delta = candidate - _precisePosition;
        if ((blocked & 0xf0) != 0) delta.Y = 0;
        if ((blocked & 0x0f) != 0) delta.X = 0;
        _precisePosition += delta;
    }

    private static int? AdjustAngleForTileEdge(int angle, int walls)
    {
        int[] table =
        [
            0x80,0x80,0x01,0x02,0x02,0x02,0x03,0x24,
            0x24,0x24,0x05,0x06,0x06,0x06,0x07,0x48,
            0x48,0x48,0x09,0x0a,0x0a,0x0a,0x0b,0x1c,
            0x1c,0x1c,0x0d,0x0e,0x0e,0x0e,0x0f,0x80
        ];
        int entry = table[angle];
        if ((entry & 3) != 0) return null;
        if ((entry & 0x80) != 0)
        {
            if ((walls & 0xc3) == 0x80) return 8;
            if ((walls & 0xcc) == 0x40) return 0x18;
        }
        else if ((entry & 0x40) != 0)
        {
            if ((walls & 0x33) == 0x20) return 8;
            if ((walls & 0x3c) == 0x10) return 0x18;
        }
        else if ((entry & 0x20) != 0)
        {
            if ((walls & 0xc3) == 1) return 0;
            if ((walls & 0x33) == 2) return 0x10;
        }
        else
        {
            if ((walls & 0xcc) == 4) return 0;
            if ((walls & 0x3c) == 8) return 0x10;
        }
        return null;
    }

    private void SetDirectionAnimation()
    {
        int index = _direction & 1;
        if (UsesMountedAnimation) _mountedAnimation.SetAnimation(index);
        else _waitingAnimation.SetAnimation(index);
    }

    public override void _Draw()
    {
        EnemyAnimationPlayer animation = UsesMountedAnimation
            ? _mountedAnimation
            : _waitingAnimation;
        DrawTexture(animation.CurrentTexture,
            animation.CurrentOffset + SourceOamDrawOffset);
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public void SetScreenTransitionBoundaryCoordinate(
        bool horizontal, int coordinate, Player player)
    {
        if (horizontal) _precisePosition.X += coordinate - Mathf.Floor(_precisePosition.X);
        else _precisePosition.Y += coordinate - Mathf.Floor(_precisePosition.Y);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        SynchronizeRide(player);
    }

    public void BeginScreenTransition(OracleRoomData destination) =>
        _transitionDestination = destination;

    public void SetScreenTransitionPosition(
        Vector2 position, Vector2 screenOffset, Player player)
    {
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        player.SetRaftRidePosition(
            position, _direction,
            _mountedAnimation.CurrentParameter, screenOffset);
    }

    public void FinishScreenTransition(Vector2 position, Player player)
    {
        _room = _transitionDestination ?? throw new InvalidOperationException(
            "Raft finished scrolling without a destination.");
        _transitionDestination = null;
        _group = _room.Group;
        _roomId = _room.Id;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        // finishScrollingTransition writes local respawn and last mount point,
        // but does not overwrite wRememberedCompanion until a later dismount.
        Vector2 pixels = OracleObjectMath.ToPixelPosition(position);
        CompanionRuntimeState.SetLastAnimalMountPosition(_runtime, pixels);
        player.SetLocalRespawnPosition(pixels, DirectionVector(_direction));
        SynchronizeRide(player);
    }

    private static int AngleForInput(Vector2 input)
    {
        int x = Math.Sign(input.X);
        int y = Math.Sign(input.Y);
        return (x, y) switch
        {
            (0,-1) => 0, (1,-1) => 4, (1,0) => 8, (1,1) => 0x0c,
            (0,1) => 0x10, (-1,1) => 0x14, (-1,0) => 0x18,
            (-1,-1) => 0x1c, _ => 0xff
        };
    }

    private static Vector2I DirectionVector(int direction) => direction switch
    {
        0 => Vector2I.Up, 1 => Vector2I.Right,
        2 => Vector2I.Down, 3 => Vector2I.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}

internal enum RaftPhase { Waiting, Mounting, Riding, Dismounting, Wrecked }

internal sealed record RaftSpawn(
    Vector2 Position, int Direction, int Group, int Room, bool Riding = false)
    : RoomEntitySpawn;
