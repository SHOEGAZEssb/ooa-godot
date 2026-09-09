using Godot;

namespace oracleofages;

/// <summary>ENEMY_CHEEP_CHEEP $2c: parameterized horizontal/vertical patrol.</summary>
internal partial class CheepCheepCharacter : EnemyCharacter
{
    private readonly CheepCheepBehaviorProfile _behavior = EnemyBehaviorTables.Shared.CheepCheep;
    private Vector2 _precisePosition;
    private int _travelCounter;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, int distance)
    {
        Record = record;
        _precisePosition = position;
        _travelCounter = distance;
        State = Counter = Angle = 0;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain,
            precisePosition: () => _precisePosition,
            setPrecisePosition: value =>
            {
                _precisePosition = value;
                Position = OracleObjectMath.ToPixelPosition(value);
            });
        Visible = false;
    }

    internal void UpdateFrame()
    {
        if (IsDead || BeginFrame()) return;
        switch (State)
        {
            case 0:
                PrepareForScreenTransition();
                return;
            case 8:
                State = 9;
                Angle = Record.SubId == 0 ? 0x18 : 0x10;
                // Both subid state-8 handlers double var03 as an eight-bit add.
                _travelCounter = (_travelCounter * 2) & 0xff;
                Counter = _travelCounter;
                return;
            case 9:
                Counter = (Counter - 1) & 0xff;
                if (Counter == 0)
                {
                    Counter = _behavior.RestFrames;
                    State = 10;
                }
                // The zero-counter update still moves; ordinary swimming has
                // no terrain, boundary, gravity, scent, or RNG calls.
                Position = OracleObjectMovement.Shared.ApplySpeed(
                    ref _precisePosition, _behavior.SpeedRaw, Angle);
                AdvanceAnimation();
                return;
            case 10:
                Counter = (Counter - 1) & 0xff;
                if (Counter != 0)
                {
                    AdvanceAnimation();
                    return;
                }
                Counter = _travelCounter;
                State = 9;
                Angle ^= 0x10;
                SetAnimation(AnimationIndex ^ 1);
                return;
        }
    }

    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        State = 8;
        Visible = true;
        QueueRedraw();
    }
}
