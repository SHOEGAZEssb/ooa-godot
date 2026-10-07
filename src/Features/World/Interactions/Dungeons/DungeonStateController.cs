using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Shared INTERAC_DUNGEON_EVENTS state consumers layered around the
/// shared rotating-cube state.
/// </summary>
internal sealed partial class DungeonStateController : Node2D,
    IRoomEntity, IFixedRoomEntity, IColoredCubePuzzleStateSource,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly DungeonInteractionDatabase _data;
    private readonly ColoredCubePuzzleState _puzzle;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int, bool> _setTrigger;
    private int _lastTile;
    private bool _initialized;

    public Node2D Node => this;
    public ColoredCubePuzzleState ColoredCubePuzzleState => _puzzle;
    public bool UpdatesDuringDialogue => _record.Kind != DungeonObjectKind.CubeColorSource || !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;

    internal DungeonStateController(
        DungeonObjectRecord record,
        Func<OracleRoomData> activeRoom,
        DungeonInteractionDatabase data,
        ColoredCubePuzzleState puzzle,
        OracleRuntimeState runtime,
        Action<int, bool> setTrigger)
    {
        _record = record;
        _activeRoom = activeRoom;
        _data = data;
        _puzzle = puzzle;
        _runtime = runtime;
        _setTrigger = setTrigger;
        Position = record.Position;
        Name = $"DungeonState_{record.Kind}_{record.Room:x2}";
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance();

    private void Advance()
    {
        switch (_record.Kind)
        {
            case DungeonObjectKind.RedFloorTrigger:
                PublishTrigger(TileAt(0x5a) == _data.Constant("red-toggle-floor"));
                break;
            case DungeonObjectKind.RedFlameTrigger:
                PublishTrigger(_puzzle.CubeColor == 0x80);
                break;
            case DungeonObjectKind.FloorSwitchBit:
                UpdateFloorSwitchBit();
                break;
            case DungeonObjectKind.CubeSwitchSensor:
                UpdateCubeSwitchBit();
                break;
            case DungeonObjectKind.CubeColorSource:
                if (!_initialized)
                {
                    _initialized = true;
                    InitializeCubeColor();
                }
                UpdateCubeColor();
                break;
            default:
                throw new InvalidOperationException(
                    $"{_record.Source} is not a shared dungeon state controller.");
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void PublishTrigger(bool active)
    {
        // Both handlers replace all of wActiveTriggers, not just bit zero.
        for (int bit = 0; bit < 8; bit++)
            _setTrigger(bit, bit == 0 && active);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (UpdatesDuringDialogue)
            Advance();
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (UpdatesDuringDialogue)
            Advance();
    }

    private void InitializeCubeColor()
    {
        int tile = TileAt((byte)((_record.Y & 0xf0) | ((_record.X >> 4) & 15)));
        _lastTile = tile;
        _puzzle.CubeColor =
            0x80 | (tile - _data.Constant("red-toggle-floor"));
        _puzzle.CubePosition = 0x57;
    }

    private void UpdateCubeColor()
    {
        int tile = TileAt((byte)((_record.Y & 0xf0) | ((_record.X >> 4) & 15)));
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
        return _activeRoom().GetPackedStorageMetatile((byte)packedPosition);
    }
}
