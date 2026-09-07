using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class GopongaFlowerCharacter : EnemyCharacter
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _profile = EnemyBehaviorTables.Shared.GopongaFlower;
    private OracleRandom _random = null!;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }

    internal void Initialize(ImportedEnemyDefinition record, Vector2 position, OracleRandom random)
    {
        Record = record;
        _random = random;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
    }

    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        State = 8;
        Counter = _profile[0].Value;
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead) return;
        // enemyCode25 ignores living recoil. Animations change only on state
        // entry; the native routine never calls enemyAnimate.
        AdvanceInvincibilityCounter();
        if (State == 0) { PrepareForScreenTransition(); return; }
        Counter = (Counter - 1) & 0xff;
        if (State == 8)
        {
            if (Counter != 0) return;
            State = 9;
            Counter = _profile[1].Value;
            RestartAnimation(1);
        }
        else if (Counter == 0)
        {
            State = 8;
            Counter = _profile[3].Value;
            RestartAnimation(0);
        }
        else if (Counter == _profile[2].Value && (_random.Next().Value & _profile[4].Value) == 0)
            spawns.Add(new ZoraFireSpawn(Position)); // PART_$31 shares partCode19.
    }
}
