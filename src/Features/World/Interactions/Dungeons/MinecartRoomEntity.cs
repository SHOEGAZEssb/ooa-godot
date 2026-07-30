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
    IPlayerRestriction, IPlayerForcedMovement
{
    private const int SndMinecart = 0x80;

    private readonly OracleRoomData _room;
    private readonly DungeonInteractionDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly RoomSession? _rooms;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private Vector2 _precisePosition;
    private int _slot;
    private int _roomId;
    private int _direction;
    private int _pushCounter;
    private int _soundCounter;
    private bool _riding;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool DisablesSword => _riding;
    public bool DisablesItems => _riding;
    public bool DisablesMovement => _riding;
    public bool DisablesMenus => _riding;
    internal bool Riding => _riding;
    internal int Direction => _direction;
    internal int PushCounter => _pushCounter;

    internal MinecartRoomEntity(
        ActiveMinecart cart,
        OracleRoomData room,
        DungeonInteractionDatabase data,
        OracleRuntimeState runtime,
        RoomSession? rooms,
        DungeonInteractionVisual visual,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _room = room;
        _data = data;
        _runtime = runtime;
        _rooms = rooms;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _slot = cart.Slot;
        _roomId = cart.Room;
        _precisePosition = cart.Position;
        Position = cart.Position;
        _riding = cart.Riding;
        _direction = cart.Riding
            ? cart.Direction
            : DirectionAwayFromPlatform();
        _pushCounter = _data.Constant("minecart-mount-push");
        Name = cart.Riding
            ? $"MinecartRide_{cart.Room:x2}"
            : $"Minecart_{cart.Room:x2}_{cart.Slot}";
        ZIndex = 10;
        InitializeVisual(
            visual,
            Position,
            _direction & 1);
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        if (_riding)
            return false;
        Vector2 delta = linkCenter - Position;
        return Math.Abs(delta.X) < 12 && Math.Abs(delta.Y) < 12;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (_riding)
            player.SetScriptedPosition(Position);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_riding)
            UpdateRide(frame.Player);
        else
            UpdateStationary(frame.Player);
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void UpdateStationary(Player player)
    {
        Vector2I pushDirection = player.FacingVector;
        Vector2 delta = Position - player.Position;
        bool pushing =
            !player.TopDownAirborne &&
            pushDirection != Vector2I.Zero &&
            player.IsAttemptingObjectPush(pushDirection) &&
            delta.Dot((Vector2)pushDirection) is >= 8 and < 20 &&
            Math.Abs(delta.Dot(new Vector2(-pushDirection.Y, pushDirection.X))) < 7;
        if (!pushing)
        {
            _pushCounter = _data.Constant("minecart-mount-push");
            return;
        }
        if (--_pushCounter != 0)
            return;

        _direction = DirectionAwayFromPlatform();
        _riding = true;
        _soundCounter = 0;
        SetAnimation(_direction & 1);
        MinecartRuntimeState.BeginRide(
            _runtime, _slot, _roomId, Position, _direction);
        player.SetScriptedPosition(Position);
    }

    private void UpdateRide(Player player)
    {
        if (AtTileCenter() && UpdateTrackAtCenter(player))
            return;

        Vector2 previous = _precisePosition;
        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition,
            _data.Constant("minecart-speed"),
            _direction * 8);
        if (--_soundCounter < 0)
        {
            _soundCounter = 0x1a;
            _playSound(SndMinecart);
        }

        if (!TryBeginNeighborRoom(previous))
        {
            MinecartRuntimeState.UpdateRide(
                _runtime, _roomId, Position, _direction);
        }
        player.SetScriptedPosition(Position);
    }

    private bool UpdateTrackAtCenter(Player player)
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
            SetAnimation(_direction & 1);
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
                SetAnimation(_direction & 1);
            }
            return false;
        }

        int firstDoor = _data.Constant("minecart-door-up");
        if (nextTile >= firstDoor && nextTile < firstDoor + 4)
        {
            _room.SetPositionTileAndCollision(
                nextPoint, 0xa0, null, _animationTick());
            _roomTileChanged();
            return false;
        }

        _direction ^= 2;
        SetAnimation(_direction & 1);
        return false;
    }

    private void Dismount(Player player)
    {
        _slot = MinecartRuntimeState.FinishRide(
            _runtime, _roomId, Position);
        _riding = false;
        _pushCounter = _data.Constant("minecart-mount-push");
        player.SetScriptedPosition(Position + Vector2.Down * 6);
        _direction = DirectionAwayFromPlatform();
        SetAnimation(_direction & 1);
    }

    private bool TryBeginNeighborRoom(Vector2 previous)
    {
        Vector2I direction;
        Vector2 stored = Position;
        if (Position.X < 0 && previous.X >= 0)
        {
            direction = Vector2I.Left;
            stored.X = _room.Width - 1;
        }
        else if (Position.X >= _room.Width && previous.X < _room.Width)
        {
            direction = Vector2I.Right;
            stored.X = 0;
        }
        else if (Position.Y < 0 && previous.Y >= 0)
        {
            direction = Vector2I.Up;
            stored.Y = _room.Height - 1;
        }
        else if (Position.Y >= _room.Height && previous.Y < _room.Height)
        {
            direction = Vector2I.Down;
            stored.Y = 0;
        }
        else
        {
            return false;
        }

        if (_rooms is null ||
            !_rooms.TryGetNeighbor(4, _roomId, direction, out int neighbor))
        {
            throw new InvalidOperationException(
                $"Minecart in dungeon room 4:{_roomId:x2} crossed " +
                $"{direction} without an imported dungeon neighbor.");
        }
        _roomId = neighbor;
        MinecartRuntimeState.UpdateRide(
            _runtime, _roomId, stored, _direction);
        return true;
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
