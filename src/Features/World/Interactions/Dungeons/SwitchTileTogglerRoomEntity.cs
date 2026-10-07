using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_SWITCH_TILE_TOGGLER $78.</summary>
internal sealed partial class SwitchTileTogglerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly DungeonObjectRecord _record;
    private readonly DungeonInteractionDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Action _roomTileChanged;
    private readonly Func<byte,byte,bool> _setTile;
    private int _lastSwitchState;
    private bool _initialized;

    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal SwitchTileTogglerRoomEntity(
        DungeonObjectRecord record,
        DungeonInteractionDatabase data,
        OracleRuntimeState runtime,
        Action roomTileChanged,
        Func<byte,byte,bool> setTile)
    {
        _record = record;
        _data = data;
        _runtime = runtime;
        _roomTileChanged = roomTileChanged;
        _setTile = setTile;
        _lastSwitchState = runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        Name = $"SwitchTileToggler_{record.Group}_{record.Room:x2}_{record.Order}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            PrepareForScreenTransition(spawns);
            return;
        }
        int switchState = _runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        if (switchState == _lastSwitchState)
            return;
        _lastSwitchState = switchState;
        SetTile((switchState & _record.SubId) != 0);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            _lastSwitchState = _runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress);
        }
        return ScreenTransitionPresentation.Visible;
    }

    private void SetTile(bool enabled)
    {
        (int off, int on) = _data.SwitchTiles(_record.X);
        byte tile = (byte)(enabled ? on : off);
        // switchTileToggler.s records the complete byte before setTile.
        // Queue rejection consumes this change; a later identical byte
        // does not retry, even after graphics have drained the queue.
        if (_setTile((byte)_record.Y,tile)) _roomTileChanged();
    }

}
