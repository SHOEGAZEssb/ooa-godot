using Godot;
using System;

namespace oracleofages;

public partial class ZolCharacter : EnemyCharacter
{
    internal int NativeSpeed { get; private set; }
    private readonly ZolBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Zol;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private EnemyVerticalMotion _verticalMotion = null!;
    private ZolState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private bool _collisionEnabled;
    private bool _damageHitPending;
    private int _stunCounter;
    internal int StunCounter => _stunCounter;
    internal override void ApplyBoomerangStun(int updates) => _stunCounter = updates;
    private bool _emergeSoundPlayed;
    private Action<int> _sound = static _ => { };

    public ZolRecord Record { get; private set; }
    internal ZolState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal bool DamageHitPending => _damageHitPending;
    internal int ZFixed => _verticalMotion.ZFixed;
    internal override bool CollisionEnabled =>
        _collisionEnabled && base.CollisionEnabled;
    protected override bool DrawsAnimation => !IsDead && Visible;
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + (_verticalMotion.ZFixed >> 8));

    internal void Initialize(
        ZolRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int>? sound = null)
    {
        Record = record;
        _room = room;
        _random = random;
        _sound = sound ?? (static _ => { });
        _movement = new EnemyTerrainMovement(this, room);
        _verticalMotion = new EnemyVerticalMotion(this, _behavior.Gravity);

        string[] encodedAnimations =
        {
            record.EmergeAnimation,
            record.WaitAnimation,
            record.HopAnimation,
            record.DisappearAnimation,
            record.RedIdleAnimation,
            record.RedShakeAnimation
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
            checksHazards: true,
            nativeSpeed: () => NativeSpeed);
        ConfigureHazards(
            room,
            animateWhileFallingInHole: false,
            zPosition: () => _verticalMotion.ZFixed);

        _state = ZolState.Uninitialized;
        _collisionEnabled = false;
        Visible = false;
    }

    internal void InitializeState()
    {
        if (_state != ZolState.Uninitialized) return;
        _random.Next(); // enemyStandardUpdate writes var3d before enemyCode34.
        NativeSpeed = Record.SubId == 0 ? _behavior.GreenHopSpeedRaw : _behavior.RedInitialSpeedRaw;
        if (Record.SubId == 0)
        {
            _state = ZolState.GreenHidden;
            _collisionEnabled = false;
            Visible = false;
            RestartAnimation(0);
        }
        else
        {
            _state = ZolState.RedWaiting;
            _counter1 = _behavior.RedInitialWaitFrames;
            _collisionEnabled = true;
            Visible = true;
            RestartAnimation(4);
        }
        QueueRedraw();
    }

    internal UpdateEvent UpdateFrame(Vector2 linkPosition, int frameCounter = 0)
    {
        if (_state == ZolState.Uninitialized)
        {
            InitializeState();
            return UpdateEvent.None;
        }
        if (_damageHitPending)
        {
            _damageHitPending = false;
            AdvanceInvincibilityCounter();
            // zol.s: a weapon's JUST_HIT selects the red split state and returns.
            // A lethal hit dispatches enemyDie before that state can run.
            if (Record.SubId == 1) _state = ZolState.RedSplitting;
            return UpdateEvent.None;
        }
        if (IsDead)
            return UpdateEvent.None;
        bool stunned = !NativeHitPending && !HasActiveKnockback && Health > 0 && _stunCounter != 0;
        if (stunned)
        {
            int z = _verticalMotion.ZFixed, speedZ = _verticalMotion.SpeedZ;
            Position = EnemyStunMotion.Update(Position, (int)_state, frameCounter,
                ref _stunCounter, ref z, ref speedZ);
            _verticalMotion.ZFixed = z;
            _verticalMotion.SpeedZ = speedZ;
            QueueRedraw();
        }
        if (BeginFrame() || CheckHazards() || stunned) return UpdateEvent.None;

        switch (_state)
        {
            case ZolState.GreenHidden:
                if (ManhattanDistance(Position, linkPosition) >=
                    _behavior.WakeDistance)
                    return UpdateEvent.None;
                _verticalMotion.SpeedZ = _behavior.InitialSpeedZ;
                _counter2 = _behavior.GreenHopCount;
                _state = ZolState.GreenEmerging;
                Visible = true;
                return UpdateEvent.None;

            case ZolState.GreenEmerging:
                if (AnimationParameter == 0)
                {
                    AdvanceAnimation();
                    return UpdateEvent.None;
                }
                if (!_emergeSoundPlayed)
                {
                    _emergeSoundPlayed = true;
                    _sound(OracleSoundEngine.SndEnemyJump);
                }
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _state = ZolState.GreenWaiting;
                _counter1 = _behavior.GreenWaitFrames;
                _collisionEnabled = true;
                RestartAnimation(1);
                return UpdateEvent.None;

            case ZolState.GreenWaiting:
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.GreenHopping;
                _verticalMotion.SpeedZ = _behavior.InitialSpeedZ;
                _angle = OracleObjectMovement.Shared.RelativeAngle(
                    Position, linkPosition);
                RestartAnimation(2);
                _sound(OracleSoundEngine.SndEnemyJump);
                AdvanceAnimation(); // stateA falls through to zol_animate.
                return UpdateEvent.None;

            case ZolState.GreenHopping:
                _movement.MoveAtAngle(
                    _angle, _behavior.GreenHopSpeedRaw, allowHoles: true);
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _counter1 = _behavior.GreenWaitFrames;
                _counter2--;
                if (_counter2 > 0)
                {
                    _state = ZolState.GreenWaiting;
                    RestartAnimation(1);
                }
                else
                {
                    _state = ZolState.GreenDisappearing;
                    _collisionEnabled = false;
                    RestartAnimation(3);
                }
                return UpdateEvent.None;

            case ZolState.GreenDisappearing:
                if (AnimationParameter == 0)
                {
                    AdvanceAnimation();
                    return UpdateEvent.None;
                }
                _state = ZolState.GreenGone;
                _counter1 = _behavior.HiddenWaitFrames;
                _emergeSoundPlayed = false;
                Visible = false;
                RestartAnimation(0);
                return UpdateEvent.None;

            case ZolState.GreenGone:
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.GreenHidden;
                RestartAnimation(0);
                return UpdateEvent.None;

            case ZolState.RedWaiting:
                if (--_counter1 > 0)
                {
                    AdvanceAnimation();
                    return UpdateEvent.None;
                }
                if ((_random.Next().Value & 0x07) == 0)
                {
                    _state = ZolState.RedShaking;
                    _counter1 = _behavior.RedShakeFrames;
                    RestartAnimation(5);
                }
                else
                {
                    _state = ZolState.RedSliding;
                    NativeSpeed = _behavior.RedSlideSpeedRaw;
                    _counter1 = _behavior.RedSlideFrames;
                    _angle = OracleObjectMovement.Shared.RelativeAngle(
                        Position, linkPosition);
                    AdvanceAnimation();
                }
                return UpdateEvent.None;

            case ZolState.RedSliding:
                _movement.MoveAtAngle(
                    _angle, _behavior.RedSlideSpeedRaw, allowHoles: false);
                BounceOffScreenBoundary();
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.RedWaiting;
                _counter1 = _behavior.RedWaitFrames;
                return UpdateEvent.None;

            case ZolState.RedShaking:
                if (--_counter1 > 0)
                {
                    AdvanceAnimation();
                    return UpdateEvent.None;
                }
                _state = ZolState.RedHopping;
                NativeSpeed = _behavior.RedHopSpeedRaw;
                _verticalMotion.SpeedZ = _behavior.InitialSpeedZ;
                _angle = OracleObjectMovement.Shared.RelativeAngle(
                    Position, linkPosition);
                RestartAnimation(2);
                _sound(OracleSoundEngine.SndEnemyJump);
                return UpdateEvent.None;

            case ZolState.RedHopping:
                _movement.MoveAtAngle(
                    _angle, _behavior.RedHopSpeedRaw, allowHoles: true);
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _state = ZolState.RedWaiting;
                _counter1 = _behavior.RedWaitFrames;
                RestartAnimation(4);
                return UpdateEvent.None;

            case ZolState.RedSplitting:
                _state = ZolState.RedSplitDelay;
                _counter2 = _behavior.SplitDelayFrames;
                _collisionEnabled = false;
                Visible = false;
                return UpdateEvent.BeginSplit;

            case ZolState.RedSplitDelay:
                if (--_counter2 > 0)
                    return UpdateEvent.None;
                Finish();
                return UpdateEvent.SpawnGels;
        }

        return UpdateEvent.None;
    }

    public bool TakeSwordHit()
        => TakeSwordHit(Position, 2);

    internal bool TakeSwitchHookHit(Vector2 linkPosition, int damage)
        => TakeSwordHit(linkPosition, damage);

    internal bool TakeSomariaHit(Vector2 sourcePosition, int damage)
    {
        if (!TakeSwordHit(sourcePosition, damage)) return false;
        // Block column$15 uses ENEMYDMG_04 rather than the sword's
        // ENEMYDMG_0c. JUST_HIT still selects the red split state, but its
        // eleven recoil updates precede that state's dispatch (or death).
        ApplySwordKnockback(sourcePosition, EnemyKnockbackStrength.Normal);
        return true;
    }

    internal bool TakeSwordHit(int damage)
        => TakeSwordHit(Position, damage);

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!TakeDeferredNoKnockbackHit(sourcePosition, damage)) return false;
        _damageHitPending = true;
        return true;
    }

    internal override bool TakeBurnHit(int damage)
    {
        if (IsDead || !CollisionEnabled || _state is
            ZolState.RedSplitting or ZolState.RedSplitDelay)
        {
            return false;
        }
        Health = Math.Max(0, Health - Math.Max(1, damage));
        if (Health == 0)
            Finish();
        return true;
    }

    internal void SetStateForValidation(
        ZolState state,
        int counter1 = 0,
        int counter2 = 0,
        int animation = -1,
        bool collisionEnabled = true)
    {
        _state = state;
        _counter1 = counter1;
        _counter2 = counter2;
        _collisionEnabled = collisionEnabled;
        Visible = true;
        if (animation >= 0)
            RestartAnimation(animation);
    }

    private void BounceOffScreenBoundary()
    {
        bool hitHorizontal = Position.X <= 5 || Position.X >= _room.Width - 6;
        bool hitVertical = Position.Y <= 4 || Position.Y >= _room.Height - 7;
        if (hitHorizontal)
            _angle = (0x20 - _angle) & 0x1f;
        if (hitVertical)
            _angle = (0x10 - _angle) & 0x1f;
    }

    private static int ManhattanDistance(Vector2 first, Vector2 second) =>
        Mathf.Abs(Mathf.FloorToInt(first.X) - Mathf.FloorToInt(second.X)) +
        Mathf.Abs(Mathf.FloorToInt(first.Y) - Mathf.FloorToInt(second.Y));
}

internal enum ZolState
{
    Uninitialized = 0,
    GreenHidden = 8,
    GreenEmerging = 9,
    GreenWaiting = 10,
    GreenHopping = 11,
    GreenDisappearing = 12,
    GreenGone = 13,
    RedWaiting = 16,
    RedSliding = 17,
    RedShaking = 18,
    RedHopping = 19,
    RedSplitting = 20,
    RedSplitDelay = 21
}

internal enum UpdateEvent
{
    None,
    BeginSplit,
    SpawnGels
}
