using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal partial class MoldormSpawnerCharacter : EnemyCharacter
{
    private EnemyDatabase _enemies = null!;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Func<int, bool> _slots = null!;
    private Action<MoldormCharacter> _killParts = null!;
    private Action<int> _sound = null!;
    private EnemyCombatSourceDescriptor _source;
    private MoldormCharacter _head = null!;
    internal int State { get; private set; }
    internal bool CountsAsEnemy => _source.CountsAsEnemy && !IsDead;
    internal override bool CollisionEnabled => false;

    internal void Initialize(ImportedEnemyDefinition record, EnemyDatabase enemies, OracleRoomData room,
        Vector2 position, OracleRandom random, EnemyCombatSourceDescriptor source,
        Func<int, bool> slots, Action<MoldormCharacter> killParts, Action<int> sound)
    {
        _enemies = enemies; _room = room; _random = random; _source = source;
        _slots = slots; _killParts = killParts; _sound = sound;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        RestartAnimation(0);
        Visible = false; ZIndex = 10;
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead) return;
        if (State == 0) { _random.Next(); State = 1; }
        if (!_slots(3)) { Visible = true; return; }
        for (int subid = 1; subid <= 3; subid++) spawns.Add(new MoldormChildSpawn(this, subid));
        Finish(); // Count transfers to head; tails acquire two additional counts.
    }

    internal IRoomEntity CreateChild(int subid)
    {
        var definition = _enemies.ImportedEnemy(EnemyId.Moldorm, subid);
        if (subid == 1)
        {
            _head = new MoldormCharacter { Name = "MoldormHead", ZIndex = 10, KillRelatedParts = _killParts };
            _head.Initialize(definition, _room, Position.Floor(), _random);
            return new MoldormRoomEntity(_head, _source with { SubId = 1 }, _sound);
        }
        var tail = new MoldormTailCharacter { Name = $"MoldormTail{subid - 1}", Head = _head };
        tail.Initialize(definition, _room, Position.Floor(), _random, subid == 2 ? _head : _head.Tail1!);
        if (subid == 2) _head.Tail1 = tail;
        else _head.Tail2 = tail;
        return new MoldormTailRoomEntity(tail, _source with { SubId = subid, ObjectFlags = 0, KillableEnemyIndex = 0 }, _sound);
    }
}

internal sealed record MoldormChildSpawn(MoldormSpawnerCharacter Spawner, int SubId) : RoomEntitySpawn;
