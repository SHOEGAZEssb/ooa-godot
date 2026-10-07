using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_DOOR_CONTROLLER subids $00 and $0c-$0f for layout minecart
/// shutters $7c-$7f. The one-shot subid $00 opens the door ahead of a moving
/// cart; the layout directional controller runs its yielding native script,
/// closes once after the cart clears the doorway, and deletes itself.
/// </summary>
internal sealed partial class MinecartShutterRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly int _closedTile;
    private readonly int _openTile;
    private readonly bool _oneShotOpener;
    private readonly Func<byte,byte,bool> _setTile;
    private readonly Func<bool> _paletteFadeActive;
    private readonly Func<bool> _textActive;
    private readonly Action<bool>? _shutterSignal;
    private MinecartShutterState _state;
    private int _counter;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int PackedPosition { get; }
    internal MinecartShutterState State => _state;
    public bool UpdatesDuringDialogue => _state == MinecartShutterState.Initialize;
    public bool UpdatesDuringRoomEntityFreeze => _state == MinecartShutterState.Initialize;

    internal MinecartShutterRoomEntity(
        int packedPosition,
        int closedTile,
        bool oneShotOpener,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        Action<int> playSound,
        Func<byte,byte,bool> setTile,
        Action<bool>? shutterSignal = null,
        Func<bool>? paletteFadeActive = null,
        Func<bool>? textActive = null)
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
        _setTile = setTile;
        _paletteFadeActive = paletteFadeActive ?? (() => false);
        _textActive = textActive ?? (() => false);
        _shutterSignal = shutterSignal;
        Position = PointFor(packedPosition);
        Name = oneShotOpener
            ? $"MinecartShutterOpener_{packedPosition:x2}"
            : $"MinecartShutter_{closedTile:x2}_{packedPosition:x2}";
        _state = MinecartShutterState.Initialize;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        // These gates precede the shared INTERAC$1e state dispatcher.
        // Palette mode holds state2 only; closing remains eligible.
        if (frame.SwitchHookState == 2 ||
            _state is MinecartShutterState.ReadyToOpen or MinecartShutterState.OpeningInterleaved && _paletteFadeActive())
            return;
        if (_state == MinecartShutterState.Initialize)
        {
            if (!_oneShotOpener && _room.GetTerrainInfo(Position).Collision == 0)
                _shutterSignal?.Invoke(true);
            _state = _oneShotOpener ? MinecartShutterState.RunOpenerScript : MinecartShutterState.SetCollisionRadii;
        }
        // interactionRunScript pauses commands under death/text after
        // state0 initialization. Native animation states2/3 remain separate.
        if ((_textActive() || frame.Player.IsDying) && _state is not (
            MinecartShutterState.ReadyToOpen or MinecartShutterState.OpeningInterleaved or
            MinecartShutterState.ReadyToClose or MinecartShutterState.ClosingInterleaved)) return;
        switch (_state)
        {
            case MinecartShutterState.RunOpenerScript:
                _state = MinecartShutterState.ReadyToOpen;
                return;

            case MinecartShutterState.SetCollisionRadii:
                _state = MinecartShutterState.InitializeAngle;
                return;

            case MinecartShutterState.ScriptEnd:
                Finished = true;
                return;

            case MinecartShutterState.InitializeAngle:
                // setcollisionradii and setangle each yield one update.
                _state = MinecartShutterState.SelectTrack;
                return;

            case MinecartShutterState.SelectTrack:
                // The script's jumptable_memoryaddress also yields. It selects
                // this branch once, not again when another opener changes it.
                _state = IsOpenTrack() ? MinecartShutterState.CallClearanceCheck : MinecartShutterState.WaitingForCartCollision;
                return;

            case MinecartShutterState.WaitingForCartCollision:
                if (OverlapsRidingCart(frame.Player))
                    _state = MinecartShutterState.PendingOpen;
                return;

            case MinecartShutterState.PendingOpen:
                _state = MinecartShutterState.ReadyToOpen;
                return;

            case MinecartShutterState.CallClearanceCheck:
                _state = MinecartShutterState.WaitingForCartClear;
                return;

            case MinecartShutterState.WaitingForCartClear:
                if (OverlapsRidingCart(frame.Player))
                    return;
                _state = MinecartShutterState.UpdateRespawn;
                return;

            case MinecartShutterState.UpdateRespawn:
                frame.Player.MoveLocalRespawnOffShutter(
                    _room, PackedPosition, _closedTile - 0x70);
                _state = MinecartShutterState.SetCloseState;
                return;

            case MinecartShutterState.SetCloseState:
                _state = MinecartShutterState.ReadyToClose;
                return;

            case MinecartShutterState.ReadyToOpen:
                if (!_room.IsSolid(Position))
                {
                    CompleteOpeningWithoutAnimation(frame.Player);
                    return;
                }
                BeginInterleave(opening: true);
                _state = MinecartShutterState.OpeningInterleaved;
                return;

            case MinecartShutterState.OpeningInterleaved:
                if (--_counter != 0)
                    return;
                if (!_oneShotOpener) _shutterSignal?.Invoke(true);
                _setTile((byte)PackedPosition,checked((byte)_openTile));
                PlayDoorSoundIfVisible();
                CompleteOpeningWithoutAnimation(frame.Player);
                return;

            case MinecartShutterState.ReadyToClose:
                // The shared native state3 accepts ITEM$18's solid tile$da
                // before collision, allowing the shutter to displace it.
                if (_room.GetMetatile(Position) != 0xda && _room.IsSolid(Position))
                {
                    CompleteScriptEnd(frame.Player);
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
                    frame.Player.RequestForcedRespawn();
                }
                _shutterSignal?.Invoke(false);
                _setTile((byte)PackedPosition,checked((byte)_closedTile));
                PlayDoorSoundIfVisible();
                // setstate $03 leaves scriptend next. The terminal handler
                // resumes it immediately and deletes this layout controller.
                CompleteScriptEnd(frame.Player);
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

    private void CompleteOpeningWithoutAnimation(Player player)
    {
        if (_oneShotOpener)
            CompleteScriptEnd(player);
        else
            _state = MinecartShutterState.WaitingForCartClear;
    }

    private void CompleteScriptEnd(Player player)
    {
        // @gotoState1 immediately resumes the saved script pointer. Death
        // can hold scriptend after the tile/cue/shutter side effects finish.
        _state = MinecartShutterState.ScriptEnd;
        if (!_textActive() && !player.IsDying) Finished = true;
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
        return (((int)delta.Y + radiusY) & 0xff) < radiusY * 2 &&
            (((int)delta.X + radiusX) & 0xff) < radiusX * 2;
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
    InitializeAngle,
    SelectTrack,
    WaitingForCartCollision,
    PendingOpen,
    CallClearanceCheck,
    WaitingForCartClear,
    UpdateRespawn,
    SetCloseState,
    ReadyToOpen,
    OpeningInterleaved,
    ReadyToClose,
    ClosingInterleaved,
    RunOpenerScript,
    SetCollisionRadii,
    ScriptEnd
}

internal sealed record MinecartShutterOpenSpawn(
    int PackedPosition,
    int ClosedTile) : RoomEntitySpawn(UpdateThisFrame: true);
