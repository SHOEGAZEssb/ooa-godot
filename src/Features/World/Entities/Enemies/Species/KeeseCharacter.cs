using Godot;
using System;

namespace Oracles;

public partial class KeeseCharacter : EnemyCharacter
{
    internal override bool InitializationPending => !_initialized;
    private bool _initialized;
    private KeeseDatabase? _behaviorSourceCache;
    private KeeseDatabase _behavior => _behaviorSourceCache ??= KeeseDatabase.For(AssetRoot);
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private KeeseState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _turnAmount;
    private int _speed;
    private bool _flying;
    private bool _hookHitPending;

    public EnemyDatabaseEnemyRecord Record { get; private set; }
    internal KeeseState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 { get => _counter2; set => _counter2 = checked((byte)value); }
    internal int Angle => _angle;
    internal int Speed => _speed;
    internal int TurnAmount => _turnAmount;
    internal bool Flying => _flying;
    internal int CurrentAnimationFrame => AnimationFrame;
    internal int SpriteHeight => _initialized && Record.SubId == 1 ? -1 : 0;
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + SpriteHeight);

    internal void Initialize(
        EnemyDatabaseEnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        AssetRoot = record.AssetRoot;
        Record = record;
        _room = room;
        _random = random;
        _initialized = false; _state = KeeseState.Uninitialized;
        _counter1 = _counter2 = _angle = _turnAmount = _speed = 0;
        _flying = _hookHitPending = false;

        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(record.AssetRoot,
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                new[] { record.IdleAnimation, record.FlyAnimation },
                record.TileBase,
                record.Palette));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.ScreenBoundary,
            nativeSpeed: () => _speed);
        RestartAnimation(0);
        Visible = false;
    }

    internal void InitializeStateZero()
    {
        if (_initialized) return;
        _random.Next(); // enemyStandardUpdate initializes var3d before enemyCode32.
        _speed = _behavior.InitialSpeedRaw;
        _counter1 = Record.SubId == 0 ? _behavior.KeeseState.InitialRestFrames : 0;
        if (Record.SubId == 1)
        {
            _turnAmount = _behavior.KeeseState.InitialTurnAmount;
            if ((_random.Next().Value & _behavior.KeeseState.ReverseTurningMask) == 0) _turnAmount = -_turnAmount;
        }
        _initialized = true; _state = KeeseState.Resting;
        Visible = true;
    }

    internal void UpdateFrame(Vector2 linkPosition, int frameCounter, Vector2? angleTarget = null)
    {
        if (!_initialized) { InitializeStateZero(); return; }
        if (_hookHitPending)
        {
            // keese.s returns on ENEMYSTATUS_JUST_HIT before recoil or
            // movement. updateEnemies still advances invincibility on that
            // update after the native handler returns.
            _hookHitPending = false;
            AdvanceInvincibilityCounter();
            return;
        }
        if (IsDead)
            return;
        if (BeginFrame())
            return;

        if (Record.SubId == 1)
            UpdateApproachKeese(linkPosition,angleTarget ?? linkPosition);
        else
            UpdateNormalKeese(frameCounter);
    }

    internal bool TakeSwordHit(int damage)
    {
        if (IsDead || InvincibilityCounter > 0)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    internal bool TakeSwitchHookHit(Vector2 linkPosition, int damage)
    {
        if (!CollisionEnabled || !TakeSwordHit(damage)) return false;
        ApplySwordKnockback(linkPosition, EnemyKnockbackStrength.Low);
        _hookHitPending = true;
        return true;
    }

    private void UpdateNormalKeese(int frameCounter)
    {
        switch (_state)
        {
            case KeeseState.Resting:
                _counter1 = (_counter1 - 1) & 0xff;
                if (_counter1 != 0)
                    return;
                OracleRandomResult startRandom = _random.Next();
                _angle = startRandom.High & 0x1f;
                _counter1 = _behavior.KeeseState.MovementCounterBase +
                    (startRandom.Low &
                        _behavior.KeeseState.MovementCounterMask);
                _speed = _behavior.KeeseState.NormalSpeedRaw;
                _state = KeeseState.Moving;
                SetFlying(true);
                AdvanceAnimation();
                return;

            case KeeseState.Moving:
                ApplySpeed(_speed);
                BounceOffScreenBoundary();
                if ((frameCounter & 1) == 0)
                {
                    _counter1--;
                    if (_counter1 == 0)
                    {
                        _state = KeeseState.Decelerating;
                    }
                    else
                    {
                        OracleRandomResult directionRandom = _random.Next();
                        if ((directionRandom.High & 0x0f) == 0 &&
                            (directionRandom.Low & 0x1f) == 0)
                            _angle = directionRandom.Low & 0x1f;
                    }
                }
                AdvanceAnimation();
                return;

            case KeeseState.Decelerating:
                if (_counter1 < _behavior.KeeseState.DecelerationMoveLimit)
                {
                    ApplySpeed(_speed);
                    BounceOffScreenBoundary();
                }
                if ((_counter1 & 0x0f) == 0)
                {
                    _speed = _behavior.KeeseDecelerationSpeeds[
                        Math.Min(_counter1 >> 4, 7)].Value;
                }
                int mask = _behavior.KeeseDecelerationAnimationMasks[
                    Math.Min(_counter1 >> 4, 7)].Value;
                if ((frameCounter & mask) == 0)
                    AdvanceAnimation();
                _counter1++;
                if (_counter1 != _behavior.KeeseState.DecelerationEnd)
                    return;

                _state = KeeseState.Resting;
                _counter1 = _behavior.KeeseState.RestCounterBase +
                    (_random.Next().Value &
                        _behavior.KeeseState.RestCounterMask);
                SetFlying(false);
                return;
        }
    }

    private void UpdateApproachKeese(Vector2 linkPosition, Vector2 angleTarget)
    {
        if (_state == KeeseState.Resting)
        {
            int distance = Math.Abs(OracleObjectPosition.HighByte(linkPosition.X) - OracleObjectPosition.HighByte(Position.X)) +
                Math.Abs(OracleObjectPosition.HighByte(linkPosition.Y) - OracleObjectPosition.HighByte(Position.Y));
            if (distance >=
                _behavior.KeeseState.ApproachDistance)
                return;

            _angle = (OracleObjectMovement.Shared.RelativeAngle(
                Position, angleTarget) +
                _turnAmount) & ObjectAngle.Mask;
            _counter1 = _behavior.KeeseState.TurningInterval;
            _counter2 = _behavior.KeeseState.TurningIntervals;
            _speed = _behavior.KeeseState.ApproachSpeedRaw;
            _state = KeeseState.Moving;
            SetFlying(true);
            return;
        }

        ApplySpeed(_speed);
        BounceOffScreenBoundary();
        _counter1--;
        if (_counter1 == 0)
        {
            _counter1 = _behavior.KeeseState.TurningInterval;
            _angle = (_angle + _turnAmount) & ObjectAngle.Mask;
            _counter2 = (_counter2 - 1) & 255;
            if (_counter2 == 0)
            {
                _state = KeeseState.Resting;
                if ((_random.Next().Value & _behavior.KeeseState.ReverseTurningMask) == 0)
                    _turnAmount = -_turnAmount;
                SetFlying(false);
                return;
            }
        }
        AdvanceAnimation();
    }

    private void ApplySpeed(int speed)
    {
        Position += MovementDelta(speed, _angle);
    }

    private void BounceOffScreenBoundary()
    {
        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            Position,
            _angle,
            point =>
                point.X < 0 || point.X >= _room.Width ||
                point.Y < 0 || point.Y >= _room.Height);
    }

    private void SetFlying(bool flying)
    {
        if (_flying == flying)
            return;
        _flying = flying;
        RestartAnimation(flying ? 1 : 0);
    }

}

internal enum KeeseState
{
    Uninitialized = 0,
    Resting = 8,
    Moving = 9,
    Decelerating = 0x0a
}
