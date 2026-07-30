using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class SpiritsGraveMovingPlatformSpawner : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly Func<int, bool> _triggerActive;
    private readonly Action<int> _playSound;
    private readonly int _spawnWait;
    private int _state;
    private int _counter;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int State => _state;
    internal int Counter => _counter;

    internal SpiritsGraveMovingPlatformSpawner(
        Func<int, bool> triggerActive,
        Action<int> playSound,
        int spawnWait)
    {
        _triggerActive = triggerActive;
        _playSound = playSound;
        _spawnWait = spawnWait;
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
            _counter = _spawnWait;
            _state = 1;
            return;
        }
        if (--_counter != 0)
            return;
        spawns.Add(new MovingPlatformSpawn(
            new Vector2(0x78, 0x50), 0x09));
        _playSound(OracleSoundEngine.SndSolvePuzzle);
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
