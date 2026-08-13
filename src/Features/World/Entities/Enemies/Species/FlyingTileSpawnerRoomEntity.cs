using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// The placed subid-$00-$02 form of ENEMY_FLYING_TILE. It owns the original
/// ordered packed-position stream and replaces itself with one active tile at
/// a time without consuming the room-placement RNG again.
/// </summary>
internal sealed partial class FlyingTileSpawnerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    private readonly ImportedEnemyDefinition _definition;
    private readonly FlyingTileBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.FlyingTile;
    private readonly IReadOnlyList<EnemyBehaviorValue> _layout;
    private readonly bool _countsAsEnemy;
    private FlyingTileSpawnerState _state;
    private int _counter;
    private int _layoutIndex;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool CountsAsEnemy => _countsAsEnemy && !Finished;

    internal FlyingTileSpawnerState State => _state;
    internal int Counter => _counter;
    internal int LayoutIndex => _layoutIndex;

    internal FlyingTileSpawnerRoomEntity(
        int subId,
        ImportedEnemyDefinition definition,
        bool countsAsEnemy)
    {
        if (subId < 0 || subId >= _behavior.Layouts.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(subId), subId,
                "Ages ENEMY_FLYING_TILE has layouts $00-$02.");
        }
        _definition = definition;
        _layout = _behavior.Layouts[subId];
        _countsAsEnemy = countsAsEnemy;
        _state = FlyingTileSpawnerState.Uninitialized;
        Name = $"FlyingTileSpawner_{subId:x2}";
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;

        switch (_state)
        {
            case FlyingTileSpawnerState.Uninitialized:
                _state = FlyingTileSpawnerState.Initializing;
                return;

            case FlyingTileSpawnerState.Initializing:
                _counter = _behavior.InitialSpawnWaitFrames;
                _state = FlyingTileSpawnerState.Waiting;
                return;

            case FlyingTileSpawnerState.Waiting:
                _counter--;
                if (_counter != 0)
                    return;
                _counter = _behavior.SpawnWaitFrames;
                EnemyBehaviorValue position = _layout[_layoutIndex++];
                spawns.Add(new FlyingTileSpawn(
                    _definition,
                    PackedCenter(position.Value),
                    _countsAsEnemy));
                if (_layoutIndex == _layout.Count)
                    Finished = true;
                return;
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private static Vector2 PackedCenter(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        ((packedPosition >> 4) & 0x0f) * OracleRoomData.MetatileSize + 8);
}

internal enum FlyingTileSpawnerState
{
    Uninitialized,
    Initializing,
    Waiting
}
