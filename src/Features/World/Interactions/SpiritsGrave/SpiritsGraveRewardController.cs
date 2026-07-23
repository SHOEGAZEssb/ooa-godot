using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class SpiritsGraveRewardController : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly ObjectRecord _record;
    private readonly OracleSaveData? _save;
    private readonly Func<int> _enemyCount;
    private readonly GroundTreasureDatabaseRecord? _treasure;
    private readonly Action _enableLinkCollisionsAndMenu;
    private int _counter = -1;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int Counter => _counter;

    internal SpiritsGraveRewardController(
        ObjectRecord record,
        OracleSaveData? save,
        Func<int> enemyCount,
        GroundTreasureDatabaseRecord? treasure,
        Action enableLinkCollisionsAndMenu)
    {
        _record = record;
        _save = save;
        _enemyCount = enemyCount;
        _treasure = treasure;
        _enableLinkCollisionsAndMenu = enableLinkCollisionsAndMenu;
        Name = $"SpiritsGraveReward_{record.Kind}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_record.Kind == ObjectKind.BraceletReward)
        {
            SpawnTreasure(spawns);
            return;
        }
        if (_enemyCount() != 0)
            return;

        if (_record.Kind == ObjectKind.EnemySmallKey)
        {
            SpawnTreasure(spawns);
            return;
        }

        if (_counter < 0)
        {
            _save?.SetRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80);
            if (_record.Kind == ObjectKind.BossReward)
            {
                SpawnTreasure(spawns);
                return;
            }
            _counter = 20;
            return;
        }
        if (--_counter != 0)
            return;
        spawns.Add(new SpiritsGraveMinibossPortalSpawn());
        _enableLinkCollisionsAndMenu();
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void SpawnTreasure(ICollection<RoomEntitySpawn> spawns)
    {
        if (_treasure.HasValue)
            spawns.Add(new GroundTreasureSpawn(_treasure.Value));
        if (_record.Kind == ObjectKind.BossReward)
            _enableLinkCollisionsAndMenu();
        Finished = true;
    }
}
