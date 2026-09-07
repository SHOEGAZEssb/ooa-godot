using Godot;

namespace oracleofages;

internal sealed partial class BuzzBlobCharacter : EnemyCharacter
{
    private readonly BuzzBlobBehaviorProfile _behavior = EnemyBehaviorTables.Shared.BuzzBlob;
    private readonly ScentSeedAttraction _scent = new();
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private bool _shockPending;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal bool IsCukeman { get; private set; }
    internal override bool CollisionEnabled => !_shockPending && State != 0x0a && base.CollisionEnabled;

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, checksHazards: true);
    }

    internal bool BeginShock()
    {
        if (!CollisionEnabled || InvincibilityCounter != 0) return false;
        _shockPending = true;
        return true;
    }

    internal void BecomeCukeman()
    {
        if (IsCukeman) return;
        IsCukeman = true;
        RestartAnimation(2);
    }

    internal int ChooseText() => _behavior.TextBase + (_random.Next().Value & _behavior.TextMask);

    internal void PrepareForScreenTransition()
    {
        if (State != 0) return;
        State = 8;
        Visible = true;
    }

    internal void UpdateFrame(Vector2? scentTarget)
    {
        if (IsDead || BeginFrame() || CheckHazards()) return;
        if (_shockPending)
        {
            _shockPending = false;
            State = 0x0a;
            Counter = _behavior.ShockFrames;
            RestartAnimation(1);
            return;
        }
        if (State is not (0 or 0x0a) && scentTarget is { } target)
        {
            State = 4;
            Angle = _scent.UpdateAngle(Position, target, Angle, cardinal: true);
            _movement.MoveAtAngle(Angle, _behavior.Speed, allowHoles: false);
            AdvanceAnimation();
            return;
        }
        switch (State)
        {
            case 0:
                PrepareForScreenTransition();
                return;
            case 4:
                State = 8;
                break;
            case 8:
                OracleRandomResult result = _random.Next();
                Angle = result.High & _behavior.AngleMask;
                Counter = _behavior.DurationBase + (result.Low & _behavior.DurationMask);
                State = 9;
                break;
            case 9:
                if (--Counter == 0) { State = 8; break; }
                Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
                    point => point.X < 0 || point.Y < 0 || point.X >= _room.Width ||
                        point.Y >= _room.Height || _room.IsSolid(point) ||
                        _room.GetTerrainInfo(point).Hazard == HazardType.Hole);
                Position += OracleObjectMovement.Shared.Delta(_behavior.Speed, Angle);
                break;
            case 0x0a:
                if (--Counter == 0)
                {
                    State = 8;
                    RestartAnimation(IsCukeman ? 2 : 0);
                }
                break;
        }
        AdvanceAnimation();
    }
}
