using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// object_code/ages/specialObjects/raft.s: the one physical D1 body.
internal sealed partial class RaftRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerForcedMovement,
    IPlayerRideableRoomEntity, IPlayerScreenTransitionRoomEntity,
    IRoomEntityLifetime, IPlayerRestriction, INativeSpecialObjectRoomEntity, IScrollPersistentRoomEntity
{
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectId => SpecialObjectId.Raft;
    bool INativeSpecialObjectRoomEntity.SupportsDebugReconstruction => LinkRiding && !_cutsceneControlled && CanReconstruct(_runtime, _behavior);
    bool INativeSpecialObjectRoomEntity.UsesSpecialObjectSlot => !Finished;
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectDirection => _direction;
    Vector2 INativeSpecialObjectRoomEntity.NativeSpecialObjectPosition
    {
        get => _precisePosition;
        set { if (_nativeBound) _precisePosition = value; Position = value.Floor(); }
    }
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
    private readonly OracleSaveData _saveData;
    private readonly RaftBehavior _behavior;
    private readonly RaftSpawn _spawn;
    private readonly RoomEntityManager _owner;
    private readonly EnemyAnimationPlayer _mountedAnimation;
    private OracleRoomData? _transitionDestination;
    private bool _nativeBound, _cutsceneControlled;
    private Func<bool> _textActive = static () => false;
    private Func<bool> _paletteActive = static () => false;
    private int NativeState { get => Read(4); set => Write(4, value); }
    private int _direction { get => _nativeBound ? Read(8) : _spawn.Direction; set => Write(8, value); }
    private int _angle { get => Read(9); set => Write(9, value); }
    private int _stateCounter { get => Read(6); set => Write(6, value); }
    private int _dismountAngle { get => Read(0x3e); set => Write(0x3e, value); }
    private int _dismountCounter { get => Read(0x3f); set => Write(0x3f, value); }
    private Vector2 _precisePosition
    {
        get => _nativeBound ? new((Read(0x0c) | Read(0x0d) << 8) / 256f,
            (Read(0x0a) | Read(0x0b) << 8) / 256f) : _spawn.Position;
        set
        {
            OracleObjectPosition position = OracleObjectPosition.FromPixels(value);
            Write(0x0a, position.YFixed & 0xff); Write(0x0b, position.YFixed >> 8);
            Write(0x0c, position.XFixed & 0xff); Write(0x0d, position.XFixed >> 8);
        }
    }
    private int Read(int offset) => _runtime.ReadWramByte(0xd100 + offset);
    private void Write(int offset, int value) => _runtime.SetWramByte(0xd100 + offset, checked((byte)value));

    internal void BindUpdateAdmission(Func<bool> textActive, Func<bool> paletteActive)
    { _textActive = textActive; _paletteActive = paletteActive; }

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool DisablesSword => false;
    public bool DisablesItems => false;
    public bool DisablesMovement => false;
    public bool DisablesMenus => !Finished && NativeState == 2;
    public bool DisablesRingTransformations => !Finished && NativeState is 0 or 1;
    internal bool UsesSpecialObjectSlot => !Finished;
    public bool LinkRiding => !Finished && _nativeBound && NativeState == 1;
    public bool ControlsPlayerScreenTransition => LinkRiding;
    public bool BypassesScreenTransitionInputGate => false;
    public Vector2 ScreenTransitionPosition => _precisePosition;
    public bool PersistsAcrossScroll => !Finished && (Read(0) & 3) == 3;
    public void EnterRoom(OracleRoomData room)
    { _room = room; }
    internal int Direction => _direction;
    internal int Angle => _angle;
    internal Vector2 PrecisePosition => _precisePosition;
    internal int AnimationIndex => _mountedAnimation.AnimationIndex;

    internal static bool CanReconstruct(OracleRuntimeState memory, RaftBehavior behavior)
    {
        int mode = memory.ReadWramByte(0xd130), graphic = memory.ReadWramByte(0xd131);
        if (memory.ReadWramByte(0xd104) != 1 || memory.ReadField("wLinkObjectIndex") != 0xd1 ||
            memory.ReadWramByte(0xd108) > 3 || memory.ReadWramByte(0xd10f) != 0 || mode > 3 || graphic > 3 ||
            graphic / 2 != (mode & 1) || memory.ReadWramByte(0xd11a) != 0xc3 || memory.ReadWramByte(0xd132) != graphic) return false;
        var frame = behavior.MountedNativeFrames[mode & 1][graphic & 1];
        return (memory.ReadWramByte(0xd122) | memory.ReadWramByte(0xd123) << 8) ==
            frame.NextPointer && (memory.ReadWramByte(0xd11e) | memory.ReadWramByte(0xd11f) << 8) == (frame.OamPointer & 0x3fff);
    }

    internal RaftRoomEntity(RaftSpawn spawn, OracleRoomData room, RaftBehavior behavior,
        OracleSaveData saveData, OracleRuntimeState runtime, RoomEntityManager owner)
    {
        AssetRoot = room.AssetRoot;
        _room = room; _behavior = behavior; _runtime = runtime; _saveData = saveData;
        _spawn = spawn; _owner = owner;
        _mountedAnimation = new EnemyAnimationPlayer(this, 2);
        _mountedAnimation.Load(OracleGraphicsCache.LoadImage(room.AssetRoot + $"gfx/{behavior.Sprite}.png"),
            behavior.MountedAnimations, 0, behavior.MountedPalette, positionedOam: true,
            animationSourceOffsets: behavior.MountedSourceOffsets);
        Position = spawn.Position.Floor();
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        Visible = false;
        Name = $"MountedRaft_{spawn.Group:x1}_{spawn.Room:x2}";
    }

    void INativeSpecialObjectRoomEntity.OnNativeSpecialObjectBound()
    {
        _nativeBound = true;
        if (NativeState > 3)
            throw new NotSupportedException($"SPECIALOBJECT $13 state${NativeState:x2}: unsupported raft state.");
        if (NativeState != 0)
        {
            _mountedAnimation.SetAnimation(Read(0x30) & 1);
            _mountedAnimation.RestoreFrame(Read(0x31) & 1, Read(0x20));
        }
        Position = _precisePosition.Floor();
        Visible = (Read(0x1a) & 0x80) != 0;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (!LinkRiding || player.RaftRespawnPreventsSynchronization) return;
        Write(0x1a, 0xc3); Write(0x32, Read(0x31));
        var native = _behavior.MountedNativeFrames[Read(0x30) & 1][Read(0x31) & 1];
        Write(0x1e, native.OamPointer & 0xff); Write(0x1f, (native.OamPointer >> 8) & 0x3f);
        Visible = true;
        player.SetRaftRidePosition(_precisePosition, Read(0x21), Vector2.Zero);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        _runtime.SetField("wLinkRidingObject", 0xd1);
        // Only state1 uses this gate; raft does not call companionRetIfInactive.
        if (NativeState == 1 && (_paletteActive() || _runtime.ReadField("wPaletteThread_mode") != 0 ||
            _textActive() || _runtime.ReadField("wTextIsActive") != 0 ||
            (_runtime.ReadField("wScrollMode") & 0x0e) != 0 || (_runtime.ReadField("wDisabledObjects") & 0x81) != 0)) return;
        switch (NativeState)
        {
            case 0:
                Write(0x32, 0xff); Write(0x1d, _behavior.MountedTileBase);
                Write(0x1b, _behavior.MountedOamFlags); Write(0x1c, _behavior.MountedOamFlags);
                SetAnimation(0);
                NativeState = 1;
                Write(0x24, 0x80); Write(0x26, 6); Write(0x27, 6);
                _stateCounter = _behavior.DismountWaitFrames;
                frame.Player.BeginRaftRide(_precisePosition);
                _runtime.SetField("wCameraFocusedObject", 0xd1);
                _runtime.SetField("wCameraFocusedObjectType", 0);
                SavePosition(frame.Player);
                break;
            case 1:
                UpdateRiding(frame.Player);
                break;
            case 2:
                _runtime.SetField("wcc92", 0x80);
                _stateCounter = (_stateCounter - 1) & 0xff;
                if (_stateCounter != 0) break;
                _runtime.SetField("wMenuDisabled", 0);
                Write(0, 1);
                frame.Player.SetLocalRespawnPosition(frame.Player.PrecisePosition.Floor());
                NativeState = 3;
                goto case 3;
            case 3:
                if (_owner.TryCreateInteractionEntity(new RaftInteractionSpawn(
                    _precisePosition.Floor(), _owner.ActiveRoom.Group, _owner.ActiveRoom.Id, Direction: _direction, ZHigh: Read(0x0f))) is null) break;
                CompanionRuntimeState.Clear(_runtime, CompanionRuntimeState.RaftId);
                Finished = true; Visible = false;
                break;
            default:
                throw new NotSupportedException($"SPECIALOBJECT $13: unsupported state${NativeState:x2}.");
        }
        if (!Finished) Position = _precisePosition.Floor();
        QueueRedraw();
    }

    private void MoveAtSpeed(int speed, int angle, int walls)
    {
        Vector2 position = _precisePosition;
        SpecialObjectMovement.ApplySpeed(_runtime, ref position, speed, angle, walls);
        _precisePosition = position;
    }

    private void UpdateRiding(Player player)
    {
        if (_cutsceneControlled) return;

        if (player.RaftRespawning)
        {
            // @respawning writes only yh/xh and hides the raft while Link's
            // LINK_STATE_RESPAWNING handler owns his position and animation.
            Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
            _precisePosition = OracleObjectMath.ToPixelPosition(player.LocalRespawnPosition) + fraction;
            CompanionRuntimeState.Update(_runtime, CompanionRuntimeState.RaftId, _precisePosition, _direction);
            Write(0x1a, Read(0x1a) & 0x7f);
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
            _mountedAnimation.SetAnimation(Read(0x30) & 1);
            _mountedAnimation.RestoreFrame(Read(0x31) & 1, Read(0x20));
            _mountedAnimation.Advance();
            PublishAnimation();
        }

        player.TransferNativeCompanionContact();
        int knockbackAngle = player.AdvanceRaftKnockback();
        if (knockbackAngle != 0xff)
        {
            MoveAtSpeed(_behavior.KnockbackSpeed, knockbackAngle, CalculateRaftWalls());
            _runtime.SetField("wcc92", 0x88);
            ResetDismount();
            return;
        }

        Write(0x10, _behavior.Speed);
        _angle = angle;
        if (angle == 0xff || player.RaftMovementImmobilized)
        {
            ResetDismount();
            return;
        }

        int walls = CalculateRaftWalls();
        Vector2 before = _precisePosition;
        MoveAtSpeed(_behavior.Speed, angle, walls);
        if (_precisePosition != before)
        {
            _runtime.SetField("wcc92", 0x08);
            ResetDismount();
            return;
        }

        if (_dismountAngle != angle)
        {
            _dismountAngle = angle;
            return;
        }
        _dismountCounter = (_dismountCounter - 1) & 0xff;
        if (_dismountCounter != 0)
        {
            return;
        }
        if (!TryDismount(player))
            ResetDismount();
        else
            return;
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
        NativeState = 2;
        _runtime.SetField("wMenuDisabled", 1);
        _runtime.SetField("wCameraFocusedObject", 0xd0);
        _runtime.SetField("wCameraFocusedObjectType", 0);
        player.EndRaftRide(player.PrecisePosition, direction);
        player.RequestNativeForcedMovement(_behavior.DismountWalkFrames, _angle, direction);
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

    internal void BeginRaftwreckControl(Player player, Vector2 position)
    {
        if (!LinkRiding)
            throw new InvalidOperationException("Raftwreck requires Link to be riding the raft.");
        _cutsceneControlled = true;
        CopyRaftwreckPosition(position);
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
        Vector2 linkPosition = _precisePosition.Floor() + player.PrecisePosition - player.PrecisePosition.Floor();
        int direction = _direction;
        _cutsceneControlled = false;
        CompanionRuntimeState.Clear(_runtime, CompanionRuntimeState.RaftId);
        player.EndRaftRide(linkPosition, direction);
        Finished = true;
        Visible = false;
    }

    internal void CancelRaftwreckControl(Player player)
    {
        if (!_cutsceneControlled)
            return;
        _cutsceneControlled = false;
    }

    private void SavePosition(Player player)
    {
        Vector2 pixels = OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.SetLastAnimalMountPosition(_saveData, pixels);
        CompanionRuntimeState.Remember(
            _runtime, CompanionRuntimeState.RaftId, _owner.ActiveRoom.Group, _owner.ActiveRoom.Id, pixels);
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

    private void SetDirectionAnimation() => SetAnimation(_direction);
    private void SetAnimation(int mode)
    {
        Write(0x30, mode);
        _mountedAnimation.SetAnimation(mode & 1);
        PublishAnimation();
    }
    private void PublishAnimation()
    {
        var native = _behavior.MountedNativeFrames[_mountedAnimation.AnimationIndex][_mountedAnimation.FrameIndex];
        Write(0x20, _mountedAnimation.RemainingUpdates); Write(0x21, _mountedAnimation.CurrentParameter);
        Write(0x31, (_mountedAnimation.AnimationIndex * 2) + _mountedAnimation.FrameIndex);
        Write(0x22, native.NextPointer & 0xff); Write(0x23, native.NextPointer >> 8);
    }
    public override void _Draw() => DrawTexture(_mountedAnimation.CurrentTexture,
        _mountedAnimation.CurrentOffset + SourceOamDrawOffset);

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public void SetScreenTransitionBoundaryCoordinate(
        bool horizontal, int coordinate, Player player)
    {
        Vector2 position = _precisePosition;
        if (horizontal) position.X += coordinate - Mathf.Floor(position.X);
        else position.Y += coordinate - Mathf.Floor(position.Y);
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    public void BeginScreenTransition(OracleRoomData destination) =>
        _transitionDestination = destination;

    public void SetScreenTransitionPosition(
        Vector2 position, Vector2 screenOffset, Player player)
    {
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        player.SetRaftRidePosition(
            position,
            _mountedAnimation.CurrentParameter, screenOffset);
    }

    public void FinishScreenTransition(Vector2 position, Player player)
    {
        _room = _transitionDestination ?? throw new InvalidOperationException(
            "Raft finished scrolling without a destination.");
        _transitionDestination = null;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        // finishScrollingTransition writes local respawn and last mount point,
        // but does not overwrite wRememberedCompanion until a later dismount.
        Vector2 pixels = OracleObjectMath.ToPixelPosition(position);
        CompanionRuntimeState.SetLastAnimalMountPosition(_saveData, pixels);
        player.SetLocalRespawnPosition(pixels, DirectionVector(_direction));
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

internal sealed record RaftSpawn(Vector2 Position, int Direction, int Group, int Room) : RoomEntitySpawn;
