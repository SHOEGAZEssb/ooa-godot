using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// common/specialObjects/minecart.s: the one physical D1 body.
internal sealed partial class MinecartRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IPlayerRestriction, IPlayerForcedMovement, IPlayerRideableRoomEntity,
    IPlayerScreenTransitionRoomEntity, INativeSpecialObjectRoomEntity, IScrollPersistentRoomEntity
{
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectId => SpecialObjectId.Minecart;
    bool INativeSpecialObjectRoomEntity.SupportsDebugReconstruction => LinkRiding && CanReconstruct(_runtime, _data);
    bool INativeSpecialObjectRoomEntity.UsesSpecialObjectSlot => !Finished;
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectDirection => _direction;
    Vector2 INativeSpecialObjectRoomEntity.NativeSpecialObjectPosition
    {
        get => _precisePosition;
        set { if (_nativeBound) _precisePosition = value; Position = value.Floor(); }
    }
    private readonly ActiveMinecart _spawn;
    private OracleRoomData _room;
    private readonly MinecartData _data;
    private readonly OracleRuntimeState _runtime;
    private readonly RoomEntityManager _owner;
    private readonly Action<int> _playSound;
    private Vector2 _precisePosition
    {
        get => _nativeBound ? new((Read(0x0c) | Read(0x0d) << 8) / 256f,
            (Read(0x0a) | Read(0x0b) << 8) / 256f) : _spawn.Position;
        set
        {
            var position = OracleObjectPosition.FromPixels(value);
            Write(0x0a, position.YFixed & 0xff); Write(0x0b, position.YFixed >> 8);
            Write(0x0c, position.XFixed & 0xff); Write(0x0d, position.XFixed >> 8);
        }
    }
    private OracleRoomData? _transitionDestination;
    private int _direction { get => _nativeBound ? Read(8) : _spawn.Direction; set => Write(8, value); }
    private int _angle { get => Read(9); set => Write(9, value); }
    private int NativeState { get => Read(4); set => Write(4, value); }
    private bool _nativeBound, _finished;
    private Func<bool> _textActive = static () => false;
    private Func<bool> _paletteActive = static () => false;
    private Func<int,int,bool>? _createDoorOpener;

    internal void BindDoorOpener(Func<int,int,bool> create) => _createDoorOpener = create;
    internal void BindUpdateAdmission(Func<bool> textActive, Func<bool> paletteActive)
    { _textActive = textActive; _paletteActive = paletteActive; }

    public Node2D Node => this;
    public bool Finished
    {
        get
        {
            if (!_finished && _nativeBound && Read(0) == 0) { _finished = true; Visible = false; }
            return _finished;
        }
    }
    internal void DeleteForScript()
    {
        if (!Finished) for (int offset = 0; offset < 0x40; offset++) Write(offset, 0);
        _finished = true; Visible = false;
    }
    public bool DisablesSword => false;
    public bool DisablesItems => false;
    public bool DisablesMovement => !Finished;
    public bool DisablesMenus => false;
    public bool LinkRiding => !Finished && (_nativeBound ? NativeState == 1 : _spawn.Riding);
    public bool ControlsPlayerScreenTransition => LinkRiding;
    public bool BypassesScreenTransitionInputGate => true;
    public Vector2 ScreenTransitionPosition => _precisePosition;
    internal bool Riding => LinkRiding;
    internal bool AllocationPending => !Finished && NativeState == 0;
    public bool PersistsAcrossScroll => !Finished && (Read(0) & 3) == 3;
    public void EnterRoom(OracleRoomData room) => _room = room;
    internal int Direction => _direction;
    internal int Angle => _angle;
    internal int CurrentAnimationIndex => AnimationIndex;
    internal int CurrentAnimationFrame => AnimationFrame;

    internal MinecartRoomEntity(
        ActiveMinecart cart,
        OracleRoomData room,
        OracleRuntimeState runtime,
        DungeonInteractionVisual visual,
        Action<int> playSound,
        RoomEntityManager owner)
    {
        if (!cart.Riding) throw new ArgumentException("INTERAC $16 requires its independent interaction binding.", nameof(cart));
        AssetRoot = room.AssetRoot;
        _spawn = cart; _owner = owner; _data = MinecartData.For(room.AssetRoot);
        _room = room;
        _runtime = runtime;
        _playSound = playSound;
        Position = cart.Position;
        Name = $"MinecartRide_{cart.Room:x2}";
        // SPECIALOBJECT_MINECART calls objectSetVisiblec2 while Link remains
        // in priority group 1. Lower source priority-group values draw over
        // higher ones, so the ridden cart is one complete sprite behind Link;
        // the imported frame offset creates the seated "inside" appearance.
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        InitializeVisual(
            visual,
            Position,
            AnimationForCurrentState());
        Visible = false;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (LinkRiding)
        {
            PublishNativeAnimation();
            Write(0x1a, 0xc2); Visible = true;
            player.SetMinecartRidePosition(
                _precisePosition,
                _direction,
                Read(0x21),
                Vector2.Zero);
        }
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        // This checked child allocation precedes every state and freeze gate.
        if (Read(0x36) == 0 && _owner.TryCreateNativeItem(() => new MinecartCollisionRoomEntity(_runtime, _data)) is not null)
            Write(0x36, 1);
        if (NativeState == 1 && (_paletteActive() || _runtime.ReadField("wPaletteThread_mode") != 0 ||
            _textActive() || _runtime.ReadField("wTextIsActive") != 0 ||
            (_runtime.ReadField("wScrollMode") & 0x0e) != 0 || (_runtime.ReadField("wDisabledObjects") & 0x81) != 0)) return;
        switch (NativeState)
        {
            case 0:
                NativeState = 1;
                // common/specialObjects/commonCode.s OAM row $0a.
                Write(0x32, 0xff); Write(0x1d, _data.Constant("mounted-tile-base"));
                Write(0x1b, _data.Constant("mounted-oam-flags")); Write(0x1c, _data.Constant("mounted-oam-flags"));
                Write(0x10, _data.Constant("minecart-speed"));
                ZIndex = ObjectDrawPriority.BehindLinkZIndex;
                SetNativeAnimation(_direction);
                _runtime.SetField("wLinkObjectIndex", 0xd1);
                _runtime.SetField("wCameraFocusedObject", 0xd1);
                _runtime.SetField("wCameraFocusedObjectType", 0);
                frame.Player.FinishMinecartMount(_precisePosition, _direction, Read(0x21));
                Write(0x1a, 0xc2); Visible = true;
                break;
            case 1:
                RestoreNativeAnimation();
                UpdateRide(frame.Player, spawns);
                break;
            default:
                throw new NotSupportedException($"SPECIALOBJECT $0a state${NativeState:x2}: unsupported minecart state.");
        }
        Position = _precisePosition.Floor();
        QueueRedraw();
    }

    void INativeSpecialObjectRoomEntity.OnNativeSpecialObjectBound()
    {
        _nativeBound = true;
        if (NativeState > 1 || _direction > 3)
            throw new NotSupportedException($"SPECIALOBJECT $0a state${NativeState:x2}/direction${_direction:x2}: unsupported native binding.");
        if (NativeState == 1) RestoreNativeAnimation();
        Position = _precisePosition.Floor();
        Visible = (Read(0x1a) & 0x80) != 0;
    }

    private void SetNativeAnimation(int mode)
    {
        Write(0x30, mode);
        SetAnimation(2 + (mode & 1));
        PublishNativeAnimation();
    }

    private void RestoreNativeAnimation()
    {
        int mode = Read(0x30), graphic = Read(0x31);
        if (mode > 3 || graphic > 3 || graphic / 2 != (mode & 1))
            throw new NotSupportedException($"SPECIALOBJECT $0a: unsupported animation mode${mode:x2}/graphic${graphic:x2}.");
        SetAnimation(2 + (mode & 1));
        RestoreAnimationFrame(graphic & 1, Read(0x20));
    }

    private void PublishNativeAnimation()
    {
        var native = _data.Mounted[Read(0x30) & 1][AnimationFrame];
        Write(0x20, AnimationCounter); Write(0x21, native.Parameter);
        Write(0x31, native.Graphic); Write(0x32, native.Graphic);
        Write(0x22, native.NextPointer & 0xff); Write(0x23, native.NextPointer >> 8);
        Write(0x1e, native.OamPointer & 0xff); Write(0x1f, (native.OamPointer >> 8) & 0x3f);
    }

    internal static bool CanReconstruct(OracleRuntimeState memory, MinecartData data)
    {
        int mode = memory.ReadWramByte(0xd130), graphic = memory.ReadWramByte(0xd131);
        if (memory.ReadWramByte(0xd104) != 1 || memory.ReadField("wLinkObjectIndex") != 0xd1 ||
            memory.ReadWramByte(0xd108) > 3 || memory.ReadWramByte(0xd10f) != 0 ||
            mode > 3 || graphic > 3 || graphic / 2 != (mode & 1) ||
            memory.ReadWramByte(0xd11a) != 0xc2 || memory.ReadWramByte(0xd132) != graphic) return false;
        var native = data.Mounted[mode & 1][graphic & 1];
        return (memory.ReadWramByte(0xd122) | memory.ReadWramByte(0xd123) << 8) == native.NextPointer &&
            (memory.ReadWramByte(0xd11e) | memory.ReadWramByte(0xd11f) << 8) == (native.OamPointer & 0x3fff);
    }

    private int Read(int offset) => _runtime.ReadWramByte(0xd100 + offset);
    private void Write(int offset, int value) => _runtime.SetWramByte(0xd100 + offset, checked((byte)value));

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void UpdateRide(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (AtTileCenter() && UpdateTrackAtCenter(player, spawns))
            return;

        // specialObjects/minecart.s decrements var35 and requests its cue
        // before objectApplySpeed publishes velocity and changes position.
        int soundCounter = (Read(0x35) - 1) & 0xff;
        if ((soundCounter & 0x80) != 0)
        {
            soundCounter = 0x1a;
            _playSound(SoundId.SndMinecart);
        }
        Write(0x35, soundCounter);
        Vector2 position = _precisePosition;
        NativeObjectMovement.ApplySpeed(_runtime, ref position, Read(0x10), _angle);
        _precisePosition = position;
        Position = position.Floor();
        AdvanceAnimation();
        PublishNativeAnimation();
        // Interactions read the current cart through wLinkObjectIndex, but
        // w1Link keeps its previous sprite coordinates until func_410d.
        player.SetMinecartMainObjectPosition(_precisePosition);
    }

    private bool UpdateTrackAtCenter(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        int currentPacked = _room.GetPackedPosition(Position);
        int currentTile = _room.GetMetatile(Position);
        if (!_data.TryTrackExit(
                _direction,
                currentTile,
                currentPacked,
                out int nextPacked,
                out int[] allowed))
        {
            _direction ^= 2;
            _angle = _direction * 8;
            SetNativeAnimation(_direction);
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
                SetNativeAnimation(_direction);
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
            // The special-object pass reserves this interaction before
            // Link/items run. Failure falls through to @notTrack's reverse.
            if ((_createDoorOpener ?? throw new InvalidOperationException(
                "SPECIALOBJECT_MINECART has no INTERAC$1e allocation owner."))(nextPacked,nextTile))
                return false;
        }

        _direction ^= 2;
        _angle = _direction * 8;
        SetNativeAnimation(_direction);
        return false;
    }

    private void Dismount(Player player)
    {
        player.ClearNativeParentAnimationClaims();
        int rideAngle = _angle;
        Vector2 linkJumpPosition = player.PrecisePosition + Vector2.Down * 6;
        NativeState = 2;
        player.BeginMinecartJump(
            linkJumpPosition,
            rideAngle,
            initialZ: -6);
        _runtime.SetField("wLinkObjectIndex", 0xd0);
        _runtime.SetField("wCameraFocusedObject", 0xd0);
        _runtime.SetField("wCameraFocusedObjectType", 0);
        _owner.TryCreateInteractionEntity(new MinecartInteractionSpawn(_precisePosition, CopyPositionFromSpecialObject: true));
        DeleteForScript();
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
            _precisePosition = new(coordinate + fraction, _precisePosition.Y);
        }
        else
        {
            float fraction =
                _precisePosition.Y - Mathf.Floor(_precisePosition.Y);
            _precisePosition = new(_precisePosition.X, coordinate + fraction);
        }
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
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
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        player.SetMinecartRidePosition(
            position,
            _direction,
            AnimationParameter,
            Vector2.Zero);
    }

    private bool AtTileCenter() =>
        (Mathf.FloorToInt(Position.Y) & 0x0f) == 8 &&
        (Mathf.FloorToInt(Position.X) & 0x0f) == 8;

    private int AnimationForCurrentState() =>
        2 + (_direction & 1);

    private static Vector2 PointFor(int packedPosition) => new(
        (packedPosition & 0x0f) * 16 + 8,
        (packedPosition >> 4) * 16 + 8);
}
