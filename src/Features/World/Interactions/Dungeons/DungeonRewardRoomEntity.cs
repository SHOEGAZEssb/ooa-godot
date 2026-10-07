using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class DungeonRewardRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly DungeonInteractionDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly Func<int> _enemyCount;
    private readonly GroundTreasureGrantRequest? _treasure;
    private readonly Action _enableLinkCollisionsAndMenu;
    private readonly Action? _spawnMinibossPortal;
    private readonly Func<GroundTreasureGrantRequest, bool>? _trySpawnTreasure;
    private int _minibossScriptStep;
    private int _bossScriptStep;
    private int _counter = -1;
    private bool _initialized;
    internal byte Counter2Alias { get; private set; }
    internal void WriteCounter2Alias(int value)
    {
        if (_record.Kind != DungeonObjectKind.BossReward)
            throw new NotSupportedException($"INTERAC${_record.Id:x2}: counter2 alias requires a traced reward script.");
        Counter2Alias = unchecked((byte)value);
    }

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;

    internal DungeonRewardRoomEntity(
        DungeonObjectRecord record,
        DungeonInteractionDatabase data,
        OracleSaveData? save,
        Func<int> enemyCount,
        GroundTreasureGrantRequest? treasure,
        Action enableLinkCollisionsAndMenu, Action? spawnMinibossPortal = null,
        Func<GroundTreasureGrantRequest, bool>? trySpawnTreasure = null)
    {
        _record = record;
        _data = data;
        _save = save;
        _enemyCount = enemyCount;
        _treasure = treasure;
        _enableLinkCollisionsAndMenu = enableLinkCollisionsAndMenu;
        _spawnMinibossPortal = spawnMinibossPortal;
        _trySpawnTreasure = trySpawnTreasure;
        if (record.Kind == DungeonObjectKind.MinibossReward && spawnMinibossPortal is null)
            throw new InvalidOperationException("dungeonScript_minibossDeath requires the native interaction allocation owner.");
        if (record.Kind == DungeonObjectKind.BossReward && (trySpawnTreasure is null || treasure is null))
            throw new InvalidOperationException("dungeonScript_bossDeath requires the native treasure allocation owner.");
        Name = $"DungeonReward_{record.Group}_{record.Room:x2}_{record.Kind}";
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance(spawns);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Advance(spawns);
        return ScreenTransitionPresentation.Hidden;
    }

    private void Advance(ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            // interactionSetScript clears counter1/counter2, including bytes
            // inherited from a write while this native page was disabled.
            Counter2Alias = 0;
            _initialized = true;
        }
        if (Counter2Alias != 0)
        {
            // interactionRunScript returns even on the 1->0 update. The
            // stationary boss-reward script has speed=0, so no motion occurs.
            Counter2Alias--;
            return;
        }
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
        if (_record.Kind == DungeonObjectKind.BossReward)
        {
            // The flag branch runs once. Octogon's JUST_HIT handler sets $80
            // while its counted death animation is still running; this must
            // not redirect an already waiting checknoenemies command.
            if (_bossScriptStep == 0)
            {
                _bossScriptStep = _save?.HasRoomFlag(
                    _record.Group,_record.Room,OracleSaveData.RoomFlag80) == true ? 3 : 1;
            }
            switch (_bossScriptStep)
            {
                case 1: // checknoenemies yields even when the count reaches 0.
                    if (_enemyCount() == 0) _bossScriptStep++;
                    return;
                case 2: // orroomflag $80
                    _save?.SetRoomFlag(_record.Group,_record.Room,OracleSaveData.RoomFlag80);
                    _bossScriptStep++; return;
                case 3: // stopifitemflagset continues; setcoords then yields.
                    if (_save?.HasRoomFlag(_record.Group,_record.Room,OracleSaveData.RoomFlagItem) == true)
                    { Finished = true; return; }
                    _bossScriptStep++; return;
                case 4: // spawnitem yields; allocation failure also advances.
                    _trySpawnTreasure!(_treasure!.Value);
                    _bossScriptStep++; return;
                case 5: // ROM scriptjump and writememory continue into scriptend.
                    _enableLinkCollisionsAndMenu(); Finished = true; return;
                default: throw new InvalidOperationException("Invalid boss reward script position.");
            }
        }
        if (_record.Kind == DungeonObjectKind.MinibossReward)
        {
            // Each of these commands returns with carry clear and yields.
            // interactionRunScript continues on the counter1 1->0 update.
            switch (_minibossScriptStep)
            {
                case 0: // checknoenemies
                    if (_enemyCount() == 0) _minibossScriptStep++;
                    return;
                case 1: // orroomflag $80
                    _save?.SetRoomFlag(_record.Group,_record.Room,OracleSaveData.RoomFlag80);
                    _minibossScriptStep++; return;
                case 2: // wait 20
                    _counter = _data.Constant("miniboss-reward-wait");
                    _minibossScriptStep++; return;
                case 3: // spawninteraction $7e, then yield even on failure
                    if (--_counter != 0) return;
                    _spawnMinibossPortal!(); _minibossScriptStep++; return;
                case 4: // writememory wDisableLinkCollisionsAndMenu,$00; scriptend
                    _enableLinkCollisionsAndMenu(); Finished = true; return;
                default: throw new InvalidOperationException("Invalid miniboss reward script position.");
            }
        }
        if (_enemyCount() != 0)
            return;

        if (_record.Kind == DungeonObjectKind.EnemySmallKey)
        {
            SpawnTreasure(spawns);
            return;
        }

        throw new NotSupportedException($"INTERAC${_record.Id:x2}:${_record.SubId:x2}: reward kind {_record.Kind} has no source script owner.");
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void SpawnTreasure(ICollection<RoomEntitySpawn> spawns)
    {
        if (_treasure.HasValue)
            spawns.Add(new GroundTreasureGrantSpawn(_treasure.Value));
        Finished = true;
    }
}
