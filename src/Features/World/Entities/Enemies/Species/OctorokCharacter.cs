using Godot;

namespace oracleofages;

public partial class OctorokCharacter : EnemyCharacter
{

    private readonly EnemyBehaviorTables _behavior = EnemyBehaviorTables.Shared;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private readonly ScentSeedAttraction _scentAttraction = new();
    private OctorokState _state;
    private int _counter1;
    private int _walkCounter;
    private int _angle;

    public OctorokRecord Record { get; private set; }
    internal OctorokState State => _state;
    internal int Counter1 => _counter1;
    internal int Angle => _angle;
    internal int CurrentAnimationFrame => AnimationFrame;
    internal int ScentAttractionCounter => _scentAttraction.Counter;

    internal void Initialize(
        OctorokRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);

        string[] encodedAnimations =
        {
            record.UpAnimation,
            record.RightAnimation,
            record.DownAnimation,
            record.LeftAnimation
        };
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                encodedAnimations,
                record.TileBase,
                record.Palette));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);

        OracleRandomResult initial = _random.Next();
        _counter1 = _behavior.OctorokCounterValues[
            initial.Value & record.CounterMask].Value;
        _angle = initial.High & 0x18;
        _walkCounter =
            _behavior.OctorokWalkCounterValues[initial.Low & 0x03].Value;
        _state = OctorokState.Walking;
        SetAnimationFromAngle();
        QueueRedraw();
    }

    internal bool UpdateFrame(Vector2 linkPosition, Vector2? scentSeedTarget = null)
    {
        if (IsDead)
            return false;

        if (BeginFrame())
            return false;
        if (CheckHazards())
            return false;

        if (scentSeedTarget is { } scentPosition)
        {
            _state = OctorokState.FollowingScentSeed;
            _angle = _scentAttraction.UpdateAngle(
                Position, scentPosition, _angle, cardinal: true);
            SetAnimationFromAngle();
            _movement.MoveAtAngle(
                _angle, Record.SpeedRaw, allowHoles: false);
            AdvanceAnimation();
            return false;
        }
        if (_state == OctorokState.FollowingScentSeed)
        {
            // octorok_state_followingScentSeed writes state $08 and returns;
            // ordinary decision logic resumes on the following update.
            _state = OctorokState.Deciding;
            return false;
        }

        switch (_state)
        {
            case OctorokState.Deciding:
                DecideNextAction(linkPosition);
                break;
            case OctorokState.Standing:
                UpdateStanding(linkPosition);
                break;
            case OctorokState.Walking:
                UpdateWalking();
                break;
            case OctorokState.Shooting:
                return UpdateShooting();
        }
        return false;
    }

    public bool TakeSwordHit(Vector2 sourcePosition)
        => TakeSwordHit(sourcePosition, 2);

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (IsDead || !CollisionEnabled || InvincibilityCounter > 0)
            return false;

        if (!TakeRawDamage(damage))
            return false;
        return true;
    }

    internal override bool TakeBurnHit(int damage) => TakeRawDamage(damage);

    private bool TakeRawDamage(int damage)
    {
        if (IsDead || !CollisionEnabled)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    internal void SetStateForValidation(
        OctorokState state,
        int counter1 = 0,
        int walkCounter = 0,
        int angle = 0)
    {
        _state = state;
        _counter1 = counter1;
        _walkCounter = walkCounter;
        _angle = angle & 0x18;
        SetAnimationFromAngle();
    }

    private void DecideNextAction(Vector2 linkPosition)
    {
        int decision = _random.Next().Value & Record.CounterMask;
        if (decision == 0)
        {
            _state = OctorokState.Shooting;
            _counter1 = _behavior.Octorok.ShootDelayFrames;
            if (Record.SubId >= 2)
            {
                _angle = GetCardinalAngleToward(linkPosition);
                SetAnimationFromAngle();
            }
            return;
        }

        _state = OctorokState.Standing;
        _counter1 = _behavior.OctorokCounterValues[decision].Value;
    }

    private void UpdateStanding(Vector2 linkPosition)
    {
        _counter1--;
        if (_counter1 > 0)
            return;

        _state = OctorokState.Walking;
        OracleRandomResult result = _random.Next();
        _walkCounter =
            _behavior.OctorokWalkCounterValues[result.Value & 0x03].Value;
        _angle = result.Low & 0x18;
        if ((result.High & 0x03) == 0)
            _angle = GetCardinalAngleToward(linkPosition);
        SetAnimationFromAngle();
    }

    private void UpdateWalking()
    {
        _walkCounter--;
        if (_walkCounter == 0)
        {
            _state = OctorokState.Deciding;
            return;
        }

        if (!_movement.MoveUsingAdjacentWalls(
            _angle,
            Record.SpeedRaw,
            allowHoles: false,
            topDown: true))
        {
            _angle = _random.Next().Value & 0x18;
            SetAnimationFromAngle();
        }
        AdvanceAnimation();
    }

    private bool UpdateShooting()
    {
        _counter1--;
        if (_counter1 > 0)
            return false;

        _counter1 = _behavior.Octorok.PostShotWaitFrames;
        _state = OctorokState.Standing;
        return true;
    }

    private int GetCardinalAngleToward(Vector2 target)
    {
        Vector2 difference = target - Position;
        if (Mathf.Abs(difference.X) >= Mathf.Abs(difference.Y))
            return difference.X >= 0 ? 0x08 : 0x18;
        return difference.Y >= 0 ? 0x10 : 0x00;
    }

    private void SetAnimationFromAngle()
    {
        RestartAnimation((_angle & 0x18) >> 3);
    }
}

internal enum OctorokState
{
    FollowingScentSeed = 4,
    Deciding = 8,
    Standing = 9,
    Walking = 10,
    Shooting = 11
}
