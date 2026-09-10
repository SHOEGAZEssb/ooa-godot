using Godot;

namespace oracleofages;

internal partial class SwordEnemyCharacter : EnemyCharacter
{
    private readonly SwordEnemyBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.SwordEnemy;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private readonly ScentSeedAttraction _scentAttraction = new();
    private SwordEnemyState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _speedRaw;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SwordEnemyState State => _state;
    internal int Counter2 => _counter2;
    internal int Counter1 => _counter1;
    internal int Angle => _angle;
    internal int ScentAttractionCounter => _scentAttraction.Counter;
    internal bool SwordBlocking { get; private set; }
    internal Vector2 EnemySwordPosition
    {
        get
        {
            int direction = ((_angle + 4) & 0x18) >> 3;
            int frame = AnimationParameter & 1;
            var offsets = EnemyBehaviorTables.Shared.EnemySwordOffsets;
            int index = direction * 4 + frame * 2;
            Vector2 position = OracleObjectMath.ToPixelPosition(Position);
            return new Vector2(((int)position.X + offsets[index + 1].Value) & 0xff,
                ((int)position.Y + offsets[index].Value) & 0xff);
        }
    }
    internal Rect2 EnemySwordCollisionBounds
    {
        get
        {
            int direction = ((_angle + 4) & 0x18) >> 3;
            var radii = EnemyBehaviorTables.Shared.EnemySwordRadii;
            int index = (direction & 1) * 2;
            Vector2 radius = new(radii[index + 1].Value, radii[index].Value);
            return new Rect2(
                EnemySwordPosition - radius,
                radius * 2.0f);
        }
    }

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _random = random;
        _room = room;
        _movement = new EnemyTerrainMovement(this, room);
        _state = SwordEnemyState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record),
            positionedOam: true);
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        Visible = false;
    }

    internal void UpdateFrame(
        Vector2 linkPosition,
        Vector2? scentSeedTarget = null,
        bool swordSlotAvailable = true)
    {
        UpdateState(linkPosition, scentSeedTarget, swordSlotAvailable);
        // swordEnemy_updateEnemyCollisionMode runs after the handler and
        // publishes the collision mode consumed by the following item pass.
        SwordBlocking = !IsDead && BlocksSwordFrom(linkPosition);
    }

    private void UpdateState(Vector2 linkPosition, Vector2? scentSeedTarget, bool swordSlotAvailable)
    {
        if (IsDead || CheckHazards() || BeginFrame())
            return;
        if (_state != SwordEnemyState.Uninitialized &&
            scentSeedTarget is { } scentPosition)
        {
            _state = SwordEnemyState.FollowingScentSeed;
            _speedRaw = _behavior.ChaseSpeedRaw;
            _angle = _scentAttraction.UpdateAngle(
                Position, scentPosition, _angle, cardinal: false);
            SetDirectionalAnimation();
            _movement.MoveAtAngle(_angle, _speedRaw, allowHoles: true);
            AdvanceAnimation();
            AdvanceAnimation();
            return;
        }
        if (_state == SwordEnemyState.FollowingScentSeed)
        {
            _state = SwordEnemyState.Wandering;
            _speedRaw = _behavior.WanderSpeedRaw;
            _angle = (_angle + 4) & 0x18;
            _counter2 = _behavior.CooldownFrames[
                Record.SubId];
            SetDirectionalAnimation();
            AdvanceAnimation();
            return;
        }
        switch (_state)
        {
            case SwordEnemyState.Uninitialized:
                InitializeState(swordSlotAvailable);
                return;

            case SwordEnemyState.Wandering:
                if (_counter2 > 0)
                    _counter2--;
                if (_counter2 == 0 &&
                    WithinChaseRange(linkPosition.X, Position.X) &&
                    WithinChaseRange(linkPosition.Y, Position.Y))
                {
                    _state = SwordEnemyState.PreparingChase;
                    _counter1 = _behavior.ChasePrepareFrames;
                    _angle = OracleObjectMovement.Shared.RelativeAngle(
                        Position, linkPosition);
                    SetDirectionalAnimation();
                    return;
                }

                _counter1 = (_counter1 - 1) & 0xff;
                if (_counter1 == 0)
                {
                    ChooseWanderRoute(linkPosition);
                    return;
                }
                if (!_movement.MoveAtAngle(
                    _angle, _speedRaw, allowHoles: false))
                {
                    EnemyAdjacentWallProbe walls = EnemyAdjacentWallResolver.Shared.Probe(
                        Position, _angle, point => point.X < 0 || point.X >= _room.Width ||
                            point.Y < 0 || point.Y >= _room.Height ||
                            _room.IsSolidForEnemyMovement(point, holesAreWalls: true));
                    if (walls.Bitset != 0)
                    {
                        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(_angle, walls);
                        SetDirectionalAnimation();
                        return;
                    }
                }
                AdvanceAnimation();
                return;

            case SwordEnemyState.PreparingChase:
                if (--_counter1 != 0)
                    return;
                _state = SwordEnemyState.Chasing;
                _counter1 = _behavior.ChaseFrames;
                _speedRaw = _behavior.ChaseSpeedRaw;
                return;

            case SwordEnemyState.Chasing:
                _counter1 = (_counter1 - 1) & 0xff;
                if (_counter1 == 0)
                {
                    _state = SwordEnemyState.Wandering;
                    _speedRaw = _behavior.WanderSpeedRaw;
                    _angle = (_angle + 4) & 0x18;
                    _counter2 = _behavior.CooldownFrames[
                        Record.SubId];
                    SetDirectionalAnimation();
                    AdvanceAnimation();
                    return;
                }
                if ((_counter1 &
                    _behavior.TurnIntervalMask) == 0)
                {
                    int target =
                        OracleObjectMovement.Shared.RelativeAngle(
                            Position, linkPosition);
                    _angle = NudgeAngle(_angle, target);
                    SetDirectionalAnimation();
                }
                _movement.MoveAtAngle(
                    _angle, _speedRaw, allowHoles: false);
                AdvanceAnimation();
                AdvanceAnimation();
                return;
        }
    }

    internal bool BlocksSwordFrom(Vector2 sourcePosition)
    {
        // swordEnemy_checkIgnoreCollision returns NZ immediately during
        // recoil; the caller selects the blocked mode on NZ, regardless of
        // Link's angle. Its comment's stated Z convention is misleading.
        if (KnockbackCounter != 0) return true;
        int sourceAngle = OracleObjectMovement.Shared.RelativeAngle(
            Position, sourcePosition);
        int bits = EnemyBehaviorTables.Shared.SwordEnemyBlockingBits[
            AnimationIndex * 4 + (sourceAngle >> 3)].Value;
        return (bits & (1 << (sourceAngle & 7))) != 0;
    }

    private void ChooseWanderRoute(Vector2 linkPosition)
    {
        OracleRandomResult result = _random.Next();
        _counter1 = _behavior.RouteCounterBase +
            (result.High & _behavior.RouteCounterMask);
        _angle = (result.Low &
            _behavior.TowardLinkMask) == 0
            ? (OracleObjectMovement.Shared.RelativeAngle(
                Position, linkPosition) + 4) & 0x18
            : _random.NextCardinalAngle();
        SetDirectionalAnimation();
    }

    private void SetDirectionalAnimation()
    {
        // ecom_updateAnimationFromAngle preserves the previous facing within
        // diagonal sectors when it is one of the two neighboring directions.
        int index = EnemyBehaviorTables.Shared.SwordEnemyAngleAnimations[_angle].Value;
        if (index >= 4)
        {
            int difference = (index - AnimationIndex) & 0xff;
            if (difference == 7 || ((difference - 3) & 0xff) < 2) return;
            index = ((_angle + 4) & 0x18) >> 3;
        }
        SetAnimation(index);
    }

    internal override bool TakeBurnHit(int damage) => base.TakeBurnHit(Health);

    internal void ApplyBladeBump(Vector2 sourcePosition, int invincibility, int knockback)
    {
        int previousInvincibility = InvincibilityCounter;
        ApplyCollisionBump(sourcePosition, -invincibility, knockback);
        if (previousInvincibility != 0) InvincibilityCounter = previousInvincibility;
    }

    internal ScreenTransitionPresentation PrepareForScreenTransition(bool swordSlotAvailable)
    {
        if (_state == SwordEnemyState.Uninitialized) InitializeState(swordSlotAvailable);
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    private void InitializeState(bool swordSlotAvailable)
    {
        // enemyStandardUpdate consumes one RNG call for Enemy.var3d before
        // the species initialization consumes another for its cardinal angle.
        _scentAttraction.Initialize(_random.Next().Value);
        if (!swordSlotAvailable) return;
        _angle = _random.NextCardinalAngle();
        _speedRaw = _behavior.WanderSpeedRaw;
        _counter1 = 1;
        _counter2 = _behavior.CooldownFrames[Record.SubId];
        _state = SwordEnemyState.Wandering;
        SetDirectionalAnimation();
        Visible = true;
    }

    private bool WithinChaseRange(float target, float position) =>
        ((Mathf.FloorToInt(target) - Mathf.FloorToInt(position) + _behavior.ChaseRadius) & 0xff)
            < _behavior.ChaseRadius * 2 + 1;

    private static int NudgeAngle(int angle, int target)
    {
        int clockwise = (target - angle) & 0x1f;
        if (clockwise == 0)
            return angle;
        return clockwise <= 0x10
            ? (angle + 1) & 0x1f
            : (angle - 1) & 0x1f;
    }
}

internal enum SwordEnemyState
{
    Uninitialized,
    FollowingScentSeed = 4,
    Wandering = 8,
    PreparingChase,
    Chasing
}
