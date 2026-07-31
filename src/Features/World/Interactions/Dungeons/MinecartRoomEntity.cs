using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_MINECART $16 and SPECIALOBJECT_MINECART. Stationary carts occupy
/// wStaticObjects; the riding form occupies w1Companion and persists across
/// ordinary dungeon scrolling.
/// </summary>
internal sealed partial class MinecartRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomBlocker, IRoomEntityLifetime,
    IPlayerRestriction, IPlayerForcedMovement, IPlayerRideableRoomEntity,
    IPlayerScreenTransitionRoomEntity
{
    private const int SndMinecart = 0x80;

    private OracleRoomData _room;
    private readonly DungeonInteractionDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private Vector2 _precisePosition;
    private OracleRoomData? _transitionDestination;
    private int _slot;
    private int _roomId;
    private int _direction;
    private int _angle;
    private int _pushCounter;
    private int _soundCounter;
    private MinecartPhase _phase;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool DisablesSword => false;
    public bool DisablesItems => false;
    public bool DisablesMovement => _phase != MinecartPhase.Stationary;
    public bool DisablesMenus => false;
    public bool LinkRiding => _phase == MinecartPhase.Riding;
    public bool ControlsPlayerScreenTransition => LinkRiding;
    public Vector2 ScreenTransitionPosition => _precisePosition;
    internal bool Riding => LinkRiding;
    internal bool Mounting => _phase == MinecartPhase.Mounting;
    internal bool Dismounting => _phase == MinecartPhase.Dismounting;
    internal int Direction => _direction;
    internal int Angle => _angle;
    internal int PushCounter => _pushCounter;
    internal int CurrentAnimationIndex => AnimationIndex;
    internal int CurrentAnimationFrame => AnimationFrame;

    internal MinecartRoomEntity(
        ActiveMinecart cart,
        OracleRoomData room,
        DungeonInteractionDatabase data,
        OracleRuntimeState runtime,
        DungeonInteractionVisual visual,
        Action<int> playSound)
    {
        _room = room;
        _data = data;
        _runtime = runtime;
        _playSound = playSound;
        _slot = cart.Slot;
        _roomId = cart.Room;
        _precisePosition = cart.Position;
        Position = cart.Position;
        _phase = cart.Riding
            ? MinecartPhase.Riding
            : MinecartPhase.Stationary;
        _direction = cart.Riding
            ? cart.Direction
            : DirectionAwayFromPlatform();
        _angle = _direction * 8;
        _pushCounter = _data.Constant("minecart-mount-push");
        Name = cart.Riding
            ? $"MinecartRide_{cart.Room:x2}"
            : $"Minecart_{cart.Room:x2}_{cart.Slot}";
        // SPECIALOBJECT_MINECART calls objectSetVisiblec2 while Link remains
        // in priority group 1. Lower source priority-group values draw over
        // higher ones, so the ridden cart is one complete sprite behind Link;
        // the imported frame offset creates the seated "inside" appearance.
        ZIndex = cart.Riding
            ? NpcCharacter.BehindLinkZIndex
            : Player.NormalZIndex;
        InitializeVisual(
            visual,
            Position,
            AnimationForCurrentState());
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        if (_phase != MinecartPhase.Stationary)
            return false;
        Vector2 delta = linkCenter - Position;
        return Math.Abs(delta.X) < 12 && Math.Abs(delta.Y) < 12;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (LinkRiding)
        {
            player.SetMinecartRidePosition(
                _precisePosition,
                _direction,
                AnimationParameter,
                Vector2.Zero);
        }
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (_phase)
        {
            case MinecartPhase.Stationary:
                UpdateStationary(frame.Player);
                break;
            case MinecartPhase.Mounting:
                UpdateMount(frame.Player);
                break;
            case MinecartPhase.Riding:
                UpdateRide(frame.Player, spawns);
                break;
            case MinecartPhase.Dismounting:
                UpdateDismount(frame.Player);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported minecart phase {_phase}.");
        }
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void UpdateStationary(Player player)
    {
        UpdateStationaryDrawPriority(player);
        Vector2I pushDirection = player.FacingVector;
        Vector2 delta = Position - player.Position;
        bool pushing =
            !player.TopDownAirborne &&
            pushDirection != Vector2I.Zero &&
            player.IsAttemptingObjectPush(pushDirection) &&
            delta.Dot((Vector2)pushDirection) is >= 8 and < 20 &&
            (Math.Abs(delta.X) <= 4 || Math.Abs(delta.Y) <= 4);
        if (!pushing)
        {
            _pushCounter = _data.Constant("minecart-mount-push");
            return;
        }
        if (--_pushCounter != 0)
            return;

        int boardingAngle = OracleObjectMovement.Shared.RelativeAngle(
            Position, player.PrecisePosition) ^ 0x10;
        _phase = MinecartPhase.Mounting;
        player.BeginMinecartJump(
            player.PrecisePosition,
            boardingAngle,
            initialZ: 0);
    }

    private void UpdateMount(Player player)
    {
        UpdateStationaryDrawPriority(player);
        if (!player.MinecartJumpReadyToRide)
            return;

        _phase = MinecartPhase.Riding;
        ZIndex = NpcCharacter.BehindLinkZIndex;
        _soundCounter = 0;
        SetAnimation(AnimationForCurrentState());
        MinecartRuntimeState.BeginRide(
            _runtime, _slot, _roomId, Position, _direction);
        player.FinishMinecartMount(
            _precisePosition,
            _direction,
            AnimationParameter);
    }

    private void UpdateRide(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (AtTileCenter() && UpdateTrackAtCenter(player, spawns))
            return;

        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition,
            _data.Constant("minecart-speed"),
            _angle);
        if (--_soundCounter < 0)
        {
            _soundCounter = 0x1a;
            _playSound(SndMinecart);
        }

        MinecartRuntimeState.UpdateRide(
            _runtime, _roomId, Position, _direction);
        AdvanceAnimation();
        player.SetMinecartRidePosition(
            _precisePosition,
            _direction,
            AnimationParameter,
            Vector2.Zero);
    }

    private bool UpdateTrackAtCenter(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        int currentPacked = _room.GetPackedPosition(Position);
        int currentTile = _room.GetMetatile(Position);
        if (!TryTrackExit(
                _direction,
                currentTile,
                currentPacked,
                out int nextPacked,
                out int[] allowed))
        {
            _direction ^= 2;
            _angle = _direction * 8;
            SetAnimation(AnimationForCurrentState());
            return false;
        }

        Vector2 nextPoint = PointFor(nextPacked);
        if (nextPacked < 0 ||
            nextPacked >= 0xb0 ||
            (nextPacked & 0x0f) >= _room.WidthInTiles)
        {
            return false;
        }
        int nextTile = _room.GetMetatile(nextPoint);
        if (nextTile == _data.Constant("minecart-platform"))
        {
            Dismount(player);
            return true;
        }

        bool accepted = false;
        foreach (int tile in allowed)
        {
            if (nextTile == tile)
            {
                accepted = true;
                break;
            }
        }
        if (accepted)
        {
            int replacement = currentTile switch
            {
                int tile when tile == _data.Constant("track-tl") ||
                    tile == _data.Constant("track-br") => _direction ^ 1,
                int tile when tile == _data.Constant("track-bl") ||
                    tile == _data.Constant("track-tr") => _direction ^ 3,
                int tile when tile == _data.Constant("track-horizontal") =>
                    (_direction & 2) | 1,
                int tile when tile == _data.Constant("track-vertical") =>
                    _direction & 2,
                _ => _direction
            };
            if (replacement != _direction)
            {
                _direction = replacement;
                _angle = _direction * 8;
                SetAnimation(AnimationForCurrentState());
            }
            return false;
        }

        int firstDoor = _data.Constant("minecart-door-up");
        if (nextTile >= firstDoor && nextTile < firstDoor + 4)
        {
            // minecartCheckCollisions allocates INTERAC_DOOR_CONTROLLER
            // subid $00. Its state-2 handler performs the audible six-update
            // interleaved opening; the persistent $0c-$0f controller created
            // from the layout later closes the track behind the cart.
            spawns.Add(new MinecartShutterOpenSpawn(nextPacked, nextTile));
            return false;
        }

        _direction ^= 2;
        _angle = _direction * 8;
        SetAnimation(AnimationForCurrentState());
        return false;
    }

    private void Dismount(Player player)
    {
        int rideAngle = _angle;
        Vector2 linkJumpPosition = player.PrecisePosition + Vector2.Down * 6;
        _slot = MinecartRuntimeState.FinishRide(
            _runtime, _roomId, Position);
        _phase = MinecartPhase.Dismounting;
        _pushCounter = _data.Constant("minecart-mount-push");
        player.BeginMinecartJump(
            linkJumpPosition,
            rideAngle,
            initialZ: -6);
        _direction = DirectionAwayFromPlatform();
        _angle = _direction * 8;
        SetAnimation(AnimationForCurrentState());
    }

    private void UpdateDismount(Player player)
    {
        UpdateStationaryDrawPriority(player);
        if (player.MinecartJumpActive)
            return;
        _phase = MinecartPhase.Stationary;
        _pushCounter = _data.Constant("minecart-mount-push");
    }

    private void UpdateStationaryDrawPriority(Player player)
    {
        // INTERAC_MINECART uses objectSetPriorityRelativeToLink with the same
        // $0b Y threshold as ordinary interactions.
        ZIndex = Position.Y > player.Position.Y + NpcCharacter.LinkPriorityYOffset
            ? NpcCharacter.InFrontOfLinkZIndex
            : NpcCharacter.BehindLinkZIndex;
    }

    public void SetScreenTransitionBoundaryCoordinate(
        bool horizontal,
        int coordinate,
        Player player)
    {
        if (!ControlsPlayerScreenTransition)
            throw new InvalidOperationException(
                "A stationary minecart cannot own a screen boundary.");
        if (horizontal)
        {
            float fraction =
                _precisePosition.X - Mathf.Floor(_precisePosition.X);
            _precisePosition.X = coordinate + fraction;
        }
        else
        {
            float fraction =
                _precisePosition.Y - Mathf.Floor(_precisePosition.Y);
            _precisePosition.Y = coordinate + fraction;
        }
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        MinecartRuntimeState.UpdateRide(
            _runtime, _roomId, _precisePosition, _direction);
        player.SetMinecartRidePosition(
            _precisePosition,
            _direction,
            AnimationParameter,
            Vector2.Zero);
    }

    public void BeginScreenTransition(OracleRoomData destination)
    {
        if (!ControlsPlayerScreenTransition ||
            _transitionDestination is not null)
        {
            throw new InvalidOperationException(
                "SPECIALOBJECT_MINECART received an invalid scrolling handoff.");
        }
        _transitionDestination = destination;
    }

    public void SetScreenTransitionPosition(
        Vector2 position,
        Vector2 screenOffset,
        Player player)
    {
        if (_transitionDestination is null)
            throw new InvalidOperationException(
                "SPECIALOBJECT_MINECART moved without a transition destination.");
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        player.SetMinecartRidePosition(
            position,
            _direction,
            AnimationParameter,
            screenOffset);
    }

    public void FinishScreenTransition(Vector2 position, Player player)
    {
        OracleRoomData destination = _transitionDestination ??
            throw new InvalidOperationException(
                "SPECIALOBJECT_MINECART finished without a destination.");
        _transitionDestination = null;
        _room = destination;
        _roomId = destination.Id;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        MinecartRuntimeState.UpdateRide(
            _runtime, _roomId, position, _direction);
        player.SetMinecartRidePosition(
            position,
            _direction,
            AnimationParameter,
            Vector2.Zero);
    }

    private int DirectionAwayFromPlatform()
    {
        int platform = _data.Constant("minecart-platform");
        Vector2[] offsets =
        {
            Vector2.Up * 16,
            Vector2.Right * 16,
            Vector2.Down * 16,
            Vector2.Left * 16
        };
        for (int direction = 0; direction < offsets.Length; direction++)
        {
            if (_room.GetMetatile(Position + offsets[direction]) == platform)
                return direction ^ 2;
        }
        throw new InvalidOperationException(
            $"Minecart at 4:{_roomId:x2} " +
            $"({Position.Y:x0},{Position.X:x0}) has no adjacent platform tile.");
    }

    private bool AtTileCenter() =>
        (Mathf.FloorToInt(Position.Y) & 0x0f) == 8 &&
        (Mathf.FloorToInt(Position.X) & 0x0f) == 8;

    private int AnimationForCurrentState() =>
        (_phase == MinecartPhase.Riding ? 2 : 0) + (_direction & 1);

    private bool TryTrackExit(
        int direction,
        int tile,
        int current,
        out int next,
        out int[] allowed)
    {
        int vertical = _data.Constant("track-vertical");
        int horizontal = _data.Constant("track-horizontal");
        int tl = _data.Constant("track-tl");
        int tr = _data.Constant("track-tr");
        int bl = _data.Constant("track-bl");
        int br = _data.Constant("track-br");
        (next, allowed) = (direction, tile) switch
        {
            (0, var t) when t == vertical =>
                (current - 0x10, [vertical, tl, tr]),
            (0, var t) when t == tl =>
                (current + 1, [horizontal, br, tr]),
            (0, var t) when t == tr =>
                (current - 1, [horizontal, bl, tl]),
            (1, var t) when t == horizontal =>
                (current + 1, [horizontal, br, tr]),
            (1, var t) when t == br =>
                (current - 0x10, [vertical, tl, tr]),
            (1, var t) when t == tr =>
                (current + 0x10, [vertical, bl, br]),
            (2, var t) when t == vertical =>
                (current + 0x10, [vertical, br, bl]),
            (2, var t) when t == br =>
                (current - 1, [horizontal, bl, tl]),
            (2, var t) when t == bl =>
                (current + 1, [horizontal, br, tr]),
            (3, var t) when t == horizontal =>
                (current - 1, [horizontal, bl, tl]),
            (3, var t) when t == bl =>
                (current - 0x10, [vertical, tr, tl]),
            (3, var t) when t == tl =>
                (current + 0x10, [vertical, br, bl]),
            _ => (current, Array.Empty<int>())
        };
        return allowed.Length != 0;
    }

    private static Vector2 PointFor(int packedPosition) => new(
        (packedPosition & 0x0f) * 16 + 8,
        (packedPosition >> 4) * 16 + 8);
}

internal enum MinecartPhase
{
    Stationary,
    Mounting,
    Riding,
    Dismounting
}
