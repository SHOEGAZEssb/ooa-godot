using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_DOOR_CONTROLLER subids $00 and $0c-$0f for layout minecart
/// shutters $7c-$7f. The one-shot subid $00 opens the door ahead of a moving
/// cart; the persistent directional controller observes the layout track and
/// closes it once the cart has cleared the doorway.
/// </summary>
internal sealed partial class MinecartShutterRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly int _closedTile;
    private readonly int _openTile;
    private readonly bool _oneShotOpener;
    private MinecartShutterState _state;
    private int _counter;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int PackedPosition { get; }
    internal int ClosedTile => _closedTile;
    internal MinecartShutterState State => _state;

    internal MinecartShutterRoomEntity(
        int packedPosition,
        int closedTile,
        bool oneShotOpener,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        Action<int> playSound)
    {
        if (closedTile is < DungeonShutterEntry.FirstMinecartShutterTile or
            > DungeonShutterEntry.LastMinecartShutterTile)
        {
            throw new ArgumentOutOfRangeException(nameof(closedTile));
        }
        PackedPosition = packedPosition;
        _closedTile = closedTile;
        _openTile = DungeonShutterEntry.MinecartOpenTile(closedTile);
        _oneShotOpener = oneShotOpener;
        _room = room;
        _data = data;
        _worldToScreen = worldToScreen;
        _animationTick = animationTick;
        _playSound = playSound;
        Position = PointFor(packedPosition);
        Name = oneShotOpener
            ? $"MinecartShutterOpener_{packedPosition:x2}"
            : $"MinecartShutter_{closedTile:x2}_{packedPosition:x2}";
        _state = oneShotOpener
            ? MinecartShutterState.ReadyToOpen
            : MinecartShutterState.Initialize;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        switch (_state)
        {
            case MinecartShutterState.Initialize:
                _state = IsOpenTrack()
                    ? MinecartShutterState.WaitingForCartClear
                    : MinecartShutterState.WaitingForCartCollision;
                return;

            case MinecartShutterState.WaitingForCartCollision:
                if (IsOpenTrack())
                {
                    _state = MinecartShutterState.WaitingForCartClear;
                    return;
                }
                if (OverlapsRidingCart(frame.Player))
                    _state = MinecartShutterState.ReadyToOpen;
                return;

            case MinecartShutterState.WaitingForCartClear:
                if (!IsOpenTrack())
                {
                    _state = MinecartShutterState.WaitingForCartCollision;
                    return;
                }
                if (OverlapsRidingCart(frame.Player))
                    return;
                frame.Player.MoveLocalRespawnOffShutter(
                    _room, PackedPosition, _closedTile - 0x70);
                _state = MinecartShutterState.ReadyToClose;
                return;

            case MinecartShutterState.ReadyToOpen:
                if (!_room.IsSolid(Position))
                {
                    CompleteOpeningWithoutAnimation();
                    return;
                }
                BeginInterleave(opening: true);
                _state = MinecartShutterState.OpeningInterleaved;
                return;

            case MinecartShutterState.OpeningInterleaved:
                if (--_counter != 0)
                    return;
                _room.SetPositionTileAndCollision(
                    Position, checked((byte)_openTile), null,
                    _animationTick());
                PlayDoorSoundIfVisible();
                CompleteOpeningWithoutAnimation();
                return;

            case MinecartShutterState.ReadyToClose:
                if (_room.IsSolid(Position))
                {
                    _state = MinecartShutterState.WaitingForCartCollision;
                    return;
                }
                BeginInterleave(opening: false);
                _state = MinecartShutterState.ClosingInterleaved;
                return;

            case MinecartShutterState.ClosingInterleaved:
                if (--_counter != 0)
                    return;
                if (_room.GetPackedPosition(frame.Player.Position) ==
                    PackedPosition)
                {
                    frame.Player.BeginFloorDoorRespawn();
                }
                _room.SetPositionTileAndCollision(
                    Position, checked((byte)_closedTile), null,
                    _animationTick());
                PlayDoorSoundIfVisible();
                _state = MinecartShutterState.WaitingForCartCollision;
                return;

            default:
                throw new InvalidOperationException(
                    $"Minecart shutter ${_closedTile:x2} at " +
                    $"${PackedPosition:x2} entered state {_state}.");
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void BeginInterleave(bool opening)
    {
        PlayDoorSoundIfVisible();
        _room.SetInterleavedMetatile(
            Position,
            checked((byte)_openTile),
            checked((byte)_closedTile),
            _closedTile & 0x03,
            _animationTick());
        _counter = _data.DoorFrameWait;
    }

    private void CompleteOpeningWithoutAnimation()
    {
        if (_oneShotOpener)
            Finished = true;
        else
            _state = MinecartShutterState.WaitingForCartClear;
    }

    private bool IsOpenTrack() =>
        _room.GetMetatile(Position) == _openTile;

    private bool OverlapsRidingCart(Player player)
    {
        if (!player.MinecartRideActive)
            return false;
        Vector2 delta = OracleObjectMath.ToPixelPosition(
            player.MinecartMainObjectPosition) - Position;
        (int radiusY, int radiusX) = (_closedTile -
            DungeonShutterEntry.FirstMinecartShutterTile) switch
        {
            0 => (0x10, 0x08),
            1 => (0x08, 0x0e),
            2 => (0x0f, 0x08),
            3 => (0x08, 0x0f),
            _ => throw new InvalidOperationException()
        };
        // w1Companion retains zero collision radii when INTERAC_MINECART
        // writes the special-object slot, so only the controller radii enter
        // objectCheckCollidedWithLink_ignoreZ here.
        return Mathf.Abs(delta.Y) < radiusY &&
            Mathf.Abs(delta.X) < radiusX;
    }

    private void PlayDoorSoundIfVisible()
    {
        if (OracleObjectMath.IsInsideOriginalScreenBoundary(
            _worldToScreen(Position)))
        {
            _playSound(_data.DoorSound);
        }
    }

    private static Vector2 PointFor(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}

internal enum MinecartShutterState
{
    Initialize,
    WaitingForCartCollision,
    WaitingForCartClear,
    ReadyToOpen,
    OpeningInterleaved,
    ReadyToClose,
    ClosingInterleaved
}

internal sealed record MinecartShutterOpenSpawn(
    int PackedPosition,
    int ClosedTile) : RoomEntitySpawn(UpdateThisFrame: true);
