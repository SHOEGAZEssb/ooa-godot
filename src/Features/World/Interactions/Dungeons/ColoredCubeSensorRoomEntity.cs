using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class ColoredCubeSensorRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly ColoredCubePuzzleState _puzzle;
    private readonly bool _light;
    private readonly int _packedPosition;
    private readonly Action<int, bool> _setTrigger;
    private readonly Action<int> _playSound;
    private int _lastPosition = -1;

    public Node2D Node => this;

    internal ColoredCubeSensorRoomEntity(
        DungeonObjectRecord record,
        OracleRoomData room,
        ColoredCubePuzzleState puzzle,
        Action<int, bool> setTrigger,
        Action<int> playSound)
    {
        _puzzle = puzzle;
        _light = record.Kind == DungeonObjectKind.CubeLightSensor;
        _packedPosition = _light ? room.GetPackedPosition(record.Position) : 0;
        _setTrigger = setTrigger;
        _playSound = playSound;
        Name = _light ? "ColoredCubeLightSensor" : "ColoredCubeTriggerSensor";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_light)
        {
            if (_lastPosition != _puzzle.CubePosition &&
                _puzzle.CubePosition == _packedPosition)
            {
                _puzzle.CubeColor |= 0x80;
                _playSound(OracleSoundEngine.SndLightTorch);
            }
            _lastPosition = _puzzle.CubePosition;
        }
        else
        {
            _setTrigger(0, _puzzle.CubeColor == 0x82);
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
