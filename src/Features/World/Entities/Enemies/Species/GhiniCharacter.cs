using Godot;
using System;

namespace oracleofages;

internal partial class GhiniCharacter : EnemyCharacter
{
    internal override bool InitializationPending => _state == GhiniState.Uninitialized;

    private readonly GhiniBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Ghini;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private int _counter;
    private int _angle;
    private GhiniState _state;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Angle => _angle;
    internal GhiniState State => _state;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _random = random;
        _room = room;
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.ScreenBoundary);
        Visible=false;
    }

    internal void UpdateFrame()
    {
        if (IsDead)
            return;
        if (BeginFrame())
            return;
        if (_state == GhiniState.Uninitialized)
        {
            _random.Next(); // enemyStandardUpdate initializes var3d before species dispatch.
            _state = GhiniState.Choosing;
            Visible=true;
            return;
        }
        if (_state == GhiniState.Choosing)
        {
            ChooseDirection();
            _state = GhiniState.Moving;
            return;
        }
        Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),
            _behavior.SpeedRaw, _angle).PrecisePosition;
        int previousAngle=_angle;
        // ghini_updateMovement changes only angle through the shared
        // side-view probes. Collision radii do not clamp its coordinates.
        _angle=EnemyAdjacentWallResolver.Shared.BounceAngle(Position,_angle,
            point=>_room.GetTerrainInfo(point).Collision==0xff);
        if (_angle!=previousAngle)
            SetAnimation(_angle < 0x10 ? 1 : 0);
        _counter--;
        if (_counter == 0)
            _state = GhiniState.Choosing;
        AdvanceAnimation();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (IsDead || !Visible)
            return;
        DrawSetTransform(GaleCollisionDisabled ? Vector2.Zero : Vector2.Up * 2.0f);
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    private void ChooseDirection()
    {
        OracleRandomResult result = _random.Next();
        _counter = _behavior.MoveCounterBase +
            (result.Low & _behavior.MoveCounterMask);
        _angle = result.High & 0x18;
        SetAnimation(_angle < 0x10 ? 1 : 0);
    }
}

internal enum GhiniState
{
    Uninitialized,
    Choosing,
    Moving
}
