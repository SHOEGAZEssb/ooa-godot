using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_SWITCH_TILE_TOGGLER $78.</summary>
internal sealed partial class SwitchTileTogglerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonInteractionDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private int _lastSwitchState;

    public Node2D Node => this;

    internal SwitchTileTogglerRoomEntity(
        DungeonObjectRecord record,
        OracleRoomData room,
        DungeonInteractionDatabase data,
        OracleRuntimeState runtime,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _room = room;
        _data = data;
        _runtime = runtime;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _lastSwitchState = runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        Name = $"SwitchTileToggler_{record.Group}_{record.Room:x2}_{record.Order}";
        // replaceSwitchTiles restores only active rows before object parsing.
        // The source layout already contains the inactive tile.
        if ((_lastSwitchState & _record.SubId) != 0)
            SetTile(enabled: true);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int switchState = _runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        if (switchState == _lastSwitchState)
            return;
        _lastSwitchState = switchState;
        SetTile((switchState & _record.SubId) != 0);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void SetTile(bool enabled)
    {
        (int off, int on) = _data.SwitchTiles(_record.X);
        byte tile = (byte)(enabled ? on : off);
        Vector2 point = PointFor(_record.Y);
        _room.SetPositionTileAndCollision(
            point, tile, null, _animationTick());
        _roomTileChanged();
    }

    private static Vector2 PointFor(int packedPosition) => new(
        (packedPosition & 0x0f) * 16 + 8,
        (packedPosition >> 4) * 16 + 8);
}
