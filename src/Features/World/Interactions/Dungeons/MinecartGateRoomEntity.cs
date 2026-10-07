using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MINECART_GATE $1b.</summary>
internal sealed partial class MinecartGateRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private const int SndOpenGate = 0x7d;

    private readonly DungeonObjectRecord _record;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly DungeonInteractionVisual _visual;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly int _direction;
    private readonly int _switchMask;
    private bool _open;
    private bool _animating;
    private bool _initialized;

    public Node2D Node => this;
    internal bool Open => _open;
    internal bool Animating => _animating;
    internal int CurrentAnimationIndex => AnimationIndex;
    internal int CurrentAnimationFrame => AnimationFrame;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal MinecartGateRoomEntity(
        DungeonObjectRecord record,
        Func<OracleRoomData> activeRoom,
        OracleRuntimeState runtime,
        DungeonInteractionVisual visual,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _activeRoom = activeRoom;
        _visual = visual;
        _runtime = runtime;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _direction = (record.SubId >> 4) & 0x0f;
        _switchMask = 1 << (record.SubId & 0x07);
        if (_direction is not (0 or 2))
        {
            throw new InvalidOperationException(
                $"{record.Source} uses unsupported minecart-gate direction " +
                $"${_direction:x2}.");
        }
        Name = $"MinecartGate_{record.Group}_{record.Room:x2}_{record.Order}";
        Position = record.Position;
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Initialize()) return;
        UpdateDrawPriority(frame.Player);
        if (_animating)
        {
            AdvanceAnimation();
            QueueRedraw();
            if (AnimationParameter == 0xff)
                _animating = false;
            return;
        }

        bool open = SwitchIsClear();
        if (open == _open)
            return;
        _open = open;
        _playSound(SndOpenGate);
        ApplyGateState();
        SetAnimation(GateAnimation());
        _animating = true;
        QueueRedraw();
    }

    private bool Initialize()
    {
        if (_initialized) return false;
        // Native state0 samples the switch after earlier physical objects,
        // including $21:$07/$08, have published during this same pass.
        _initialized = true;
        _open = SwitchIsClear();
        ApplyGateState();
        InitializeVisual(_visual, _record.Position, GateAnimation() ^ 0x01);
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        Visible = true;
        return true;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (!_initialized) Initialize();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private bool SwitchIsClear() =>
        (_runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) &
            _switchMask) == 0;

    private int GateAnimation() => _direction | (_open ? 0x01 : 0x00);

    private void UpdateDrawPriority(Player player)
    {
        // Both waiting states and the transition state call
        // objectSetPriorityRelativeToLink using the shared $0b threshold.
        ZIndex = ObjectDrawPriority.RelativeToLink(Position.Y, player.Position.Y);
    }

    private void ApplyGateState()
    {
        OracleRoomData room = _activeRoom();
        int objectPacked = room.GetPackedPosition(_record.Position);
        int firstCollisionPosition = objectPacked - 1;
        byte firstCollision;
        byte secondCollision;
        int gatePosition;
        if (_direction == 0)
        {
            firstCollision = _open ? (byte)0x0c : (byte)0x00;
            secondCollision = 0x0a;
            gatePosition = firstCollisionPosition;
        }
        else
        {
            firstCollision = 0x05;
            secondCollision = _open ? (byte)0x0c : (byte)0x00;
            gatePosition = objectPacked;
        }
        SetCollision(room, firstCollisionPosition, firstCollision);
        SetCollision(room, objectPacked, secondCollision);
        Vector2 gatePoint = PointFor(gatePosition);
        byte gateCollision = _direction == 0
            ? firstCollision
            : secondCollision;
        // The source writes both wRoomCollisions bytes before changing the
        // layout tile. Pass the applicable byte again because a null runtime
        // tile write would otherwise discard that explicit override.
        room.SetPositionTileAndCollision(
            gatePoint,
            _open ? (byte)0x00 : (byte)0x5e,
            gateCollision,
            _animationTick(),
            // The handler writes wRoomLayout directly. The imported gate
            // sprite supplies the visible transition; $00/$5e must never be
            // redrawn as a background metatile.
            preserveRenderedTile: true);
        _roomTileChanged();
    }

    private void SetCollision(OracleRoomData room, int packedPosition, byte collision)
    {
        Vector2 point = PointFor(packedPosition);
        room.SetPositionTileAndCollision(
            point,
            room.GetMetatile(point),
            collision,
            _animationTick(),
            preserveRenderedTile: true);
    }

    private static Vector2 PointFor(int packedPosition) => new(
        (packedPosition & 0x0f) * 16 + 8,
        (packedPosition >> 4) * 16 + 8);
}
