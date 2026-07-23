using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class SpiritsGraveMovingPlatformSpawner : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly Func<int, bool> _triggerActive;
    private readonly Action<int> _playSound;
    private int _state;
    private int _counter;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State => _state;
    internal int Counter => _counter;

    internal SpiritsGraveMovingPlatformSpawner(
        Func<int, bool> triggerActive,
        Action<int> playSound)
    {
        _triggerActive = triggerActive;
        _playSound = playSound;
        Name = "SpiritsGraveMovingPlatformSpawner";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_state == 0)
        {
            if (!_triggerActive(1))
                return;
            spawns.Add(new PuzzlePuffSpawn(new Vector2(0x78, 0x48), 0));
            spawns.Add(new PuzzlePuffSpawn(new Vector2(0x78, 0x58), 0));
            _counter = 30;
            _state = 1;
            return;
        }
        if (--_counter != 0)
            return;
        spawns.Add(new SpiritsGraveMovingPlatformSpawn(
            new Vector2(0x78, 0x50), 0x09));
        _playSound(OracleSoundEngine.SndSolvePuzzle);
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
