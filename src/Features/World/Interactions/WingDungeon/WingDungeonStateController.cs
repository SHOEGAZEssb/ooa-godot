using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// D2-specific INTERAC_DUNGEON_EVENTS state consumers layered around the
/// shared rotating-cube state.
/// </summary>
internal sealed partial class WingDungeonStateController : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonInteractionDatabase _data;
    private readonly ColoredCubePuzzleState _puzzle;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int, bool> _setTrigger;
    private int _lastTile = -1;

    public Node2D Node => this;

    internal WingDungeonStateController(
        DungeonObjectRecord record,
        OracleRoomData room,
        DungeonInteractionDatabase data,
        ColoredCubePuzzleState puzzle,
        OracleRuntimeState runtime,
        Action<int, bool> setTrigger)
    {
        _record = record;
        _room = room;
        _data = data;
        _puzzle = puzzle;
        _runtime = runtime;
        _setTrigger = setTrigger;
        Name = $"WingDungeonState_{record.Kind}_{record.Room:x2}";
        if (record.Kind == DungeonObjectKind.CubeColorSource)
            InitializeCubeColor();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (_record.Kind)
        {
            case DungeonObjectKind.RedFloorTrigger:
                _setTrigger(0, TileAt(0x5a) == _data.Constant("red-toggle-floor"));
                break;
            case DungeonObjectKind.RedFlameTrigger:
                _setTrigger(0, _puzzle.CubeColor == 0x80);
                break;
            case DungeonObjectKind.FloorSwitchBit:
                UpdateFloorSwitchBit();
                break;
            case DungeonObjectKind.CubeSwitchSensor:
                UpdateCubeSwitchBit();
                break;
            case DungeonObjectKind.CubeColorSource:
                UpdateCubeColor();
                break;
            default:
                throw new InvalidOperationException(
                    $"{_record.Source} is not a Wing Dungeon state controller.");
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void InitializeCubeColor()
    {
        int tile = TileAt(_room.GetPackedPosition(_record.Position));
        _lastTile = tile;
        _puzzle.CubeColor =
            0x80 | (tile - _data.Constant("red-toggle-floor"));
        _puzzle.CubePosition = 0x57;
    }

    private void UpdateCubeColor()
    {
        int tile = TileAt(_room.GetPackedPosition(_record.Position));
        int first = _data.Constant("red-toggle-floor");
        if (tile == _lastTile || tile < first || tile >= first + 3)
            return;
        _lastTile = tile;
        _puzzle.CubeColor = 0x80 | (tile - first);
    }

    private void UpdateFloorSwitchBit()
    {
        int tile = TileAt(_record.Y);
        int first = _data.Constant("red-toggle-floor");
        if (tile < first || tile >= first + 3)
            return;
        SetSwitchBit(_record.X, tile == _data.Constant("blue-toggle-floor"));
    }

    private void UpdateCubeSwitchBit()
    {
        if ((_puzzle.CubeColor & 0x80) == 0)
            return;
        _puzzle.CubeColor &= 0x7f;
        SetSwitchBit(_record.X, _puzzle.CubeColor == 2);
    }

    private void SetSwitchBit(int mask, bool active)
    {
        byte current = _runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        byte replacement = active
            ? (byte)(current | mask)
            : (byte)(current & ~mask);
        _runtime.SetWramByte(
            OracleRuntimeState.SwitchStateAddress, replacement);
    }

    private int TileAt(int packedPosition)
    {
        Vector2 point = new(
            (packedPosition & 0x0f) * 16 + 8,
            (packedPosition >> 4) * 16 + 8);
        return _room.GetMetatile(point);
    }
}
