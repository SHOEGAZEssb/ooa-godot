using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_STUFF $12:$02.</summary>
internal sealed partial class WingDungeonEnemyChest : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly ObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly WingDungeonDatabase _data;
    private readonly Func<int> _enemyCount;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private int _counter;
    private bool _appearing;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int Counter => _counter;

    internal WingDungeonEnemyChest(
        ObjectRecord record,
        OracleRoomData room,
        WingDungeonDatabase data,
        Func<int> enemyCount,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _room = room;
        _data = data;
        _enemyCount = enemyCount;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Name = $"WingDungeonEnemyChest_{record.Room:x2}";
        Position = record.Position;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_appearing)
        {
            if (_enemyCount() != 0)
                return;
            _appearing = true;
            _counter = _data.Constant("enemy-chest-wait");
            _playSound(OracleSoundEngine.SndSolvePuzzle);
            spawns.Add(new PuzzlePuffSpawn(Position, OracleSoundEngine.SndPoof));
            return;
        }
        if (--_counter != 0)
            return;
        _room.SetPositionTileAndCollision(
            Position,
            (byte)_data.Constant("chest"),
            null,
            _animationTick());
        _roomTileChanged();
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
