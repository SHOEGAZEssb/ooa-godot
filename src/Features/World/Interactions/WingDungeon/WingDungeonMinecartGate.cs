using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MINECART_GATE $1b.</summary>
internal sealed partial class WingDungeonMinecartGate : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private const int SndOpenGate = 0x7d;

    private readonly ObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly int _direction;
    private readonly int _switchMask;
    private bool _open;

    public Node2D Node => this;
    internal bool Open => _open;

    internal WingDungeonMinecartGate(
        ObjectRecord record,
        OracleRoomData room,
        OracleRuntimeState runtime,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _room = room;
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
        Name = $"WingDungeonMinecartGate_{record.Room:x2}_{record.Order}";
        _open = SwitchIsClear();
        ApplyGateState();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool open = SwitchIsClear();
        if (open == _open)
            return;
        _open = open;
        _playSound(SndOpenGate);
        ApplyGateState();
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private bool SwitchIsClear() =>
        (_runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) &
            _switchMask) == 0;

    private void ApplyGateState()
    {
        int objectPacked = _room.GetPackedPosition(_record.Position);
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
        SetCollision(firstCollisionPosition, firstCollision);
        SetCollision(objectPacked, secondCollision);
        Vector2 gatePoint = PointFor(gatePosition);
        _room.SetPositionTileAndCollision(
            gatePoint,
            _open ? (byte)0x00 : (byte)0x5e,
            null,
            _animationTick());
        _roomTileChanged();
    }

    private void SetCollision(int packedPosition, byte collision)
    {
        Vector2 point = PointFor(packedPosition);
        _room.SetPositionTileAndCollision(
            point,
            _room.GetMetatile(point),
            collision,
            _animationTick(),
            preserveRenderedTile: true);
    }

    private static Vector2 PointFor(int packedPosition) => new(
        (packedPosition & 0x0f) * 16 + 8,
        (packedPosition >> 4) * 16 + 8);
}
