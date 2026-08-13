using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class DungeonRewardRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonObjectRecord _record;
    private readonly DungeonInteractionDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly Func<int> _enemyCount;
    private readonly GroundTreasureGrantRequest? _treasure;
    private readonly Action _enableLinkCollisionsAndMenu;
    private int _counter = -1;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int Counter => _counter;

    internal DungeonRewardRoomEntity(
        DungeonObjectRecord record,
        DungeonInteractionDatabase data,
        OracleSaveData? save,
        Func<int> enemyCount,
        GroundTreasureGrantRequest? treasure,
        Action enableLinkCollisionsAndMenu)
    {
        _record = record;
        _data = data;
        _save = save;
        _enemyCount = enemyCount;
        _treasure = treasure;
        _enableLinkCollisionsAndMenu = enableLinkCollisionsAndMenu;
        Name = $"DungeonReward_{record.Group}_{record.Room:x2}_{record.Kind}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        // Static $12:$01 placements are omitted by the room factory when the
        // item flag is already set. Dynamically parsed placements still
        // create the interaction, whose first script command is
        // stopifitemflagset, so retain that guard in the shared runtime owner.
        if (_record is
                {
                    Kind: DungeonObjectKind.EnemySmallKey,
                    Predicate: DungeonObjectCondition.ItemClear
                } &&
            _save?.HasRoomFlag(
                _record.Group,
                _record.Room,
                OracleSaveData.RoomFlagItem) == true)
        {
            Finished = true;
            return;
        }
        if (_record.Kind == DungeonObjectKind.BraceletReward)
        {
            SpawnTreasure(spawns);
            return;
        }
        if (_enemyCount() != 0)
            return;

        if (_record.Kind == DungeonObjectKind.EnemySmallKey)
        {
            SpawnTreasure(spawns);
            return;
        }

        if (_counter < 0)
        {
            _save?.SetRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80);
            if (_record.Kind == DungeonObjectKind.BossReward)
            {
                SpawnTreasure(spawns);
                return;
            }
            _counter = _data.Constant("miniboss-reward-wait");
            return;
        }
        if (--_counter != 0)
            return;
        spawns.Add(new MinibossPortalSpawn());
        _enableLinkCollisionsAndMenu();
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void SpawnTreasure(ICollection<RoomEntitySpawn> spawns)
    {
        if (_treasure.HasValue)
            spawns.Add(new GroundTreasureGrantSpawn(_treasure.Value));
        if (_record.Kind == DungeonObjectKind.BossReward)
            _enableLinkCollisionsAndMenu();
        Finished = true;
    }
}
