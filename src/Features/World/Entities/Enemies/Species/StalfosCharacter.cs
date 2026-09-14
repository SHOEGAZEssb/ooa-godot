using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Common ENEMY_STALFOS $31, including subid $02's dodge and bone attack.
/// </summary>
public partial class StalfosCharacter : EnemyCharacter, ISwitchHookEnemy
{
    private readonly StalfosBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Stalfos;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private StalfosState _state;
    private int _counter1;
    private int _angle;
    private int _zFixed;
    private int _speedZ;
    private bool _jumpCollision = true;
    private EnemyTerrainMovement _movement = null!;
    private Action<int> _sound = static _ => { };
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal override bool CollisionEnabled => base.CollisionEnabled && _jumpCollision && _state != StalfosState.SwitchHook;
    internal int SwitchHookSubstate { get; private set; }
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + new Vector2(0, _zFixed >> 8);

    public StalfosRecord Record { get; private set; }
    internal StalfosState State => _state;
    internal int Counter1 => _counter1;
    internal int Angle => _angle;
    internal int CurrentAnimationFrame => AnimationFrame;

    internal void Initialize(
        StalfosRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int>? sound = null, int zHigh = 0)
    {
        if (record.SubId is not (0 or 2))
            throw new ArgumentOutOfRangeException(
                nameof(record), record.SubId,
                "ENEMY_STALFOS supports imported subids $00 and $02.");

        Record = record;
        _room = room;
        _random = random;
        _sound = sound ?? (static _ => { });
        _movement = new EnemyTerrainMovement(this, room);
        _state = StalfosState.Uninitialized;
        _zFixed = zHigh << 8;

        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                new[] { record.WalkAnimation, record.JumpAnimation },
                record.TileBase,
                record.Palette));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        RestartAnimation(0);
        ConfigureHazards(room, zPosition: () => _zFixed);
    }

    internal bool UpdateFrame(Vector2 linkPosition, bool itemStarted = false,
        Func<bool>? canSpawnPart = null)
    {
        if (IsDead)
            return false;
        if (BeginFrame())
            return false;
        if (CheckHazards())
            return false;

        if (_state == StalfosState.Uninitialized)
            _random.Next(); // enemyStandardUpdate's var3d initialization precedes the dodge gate.
        if (Record.SubId != 0 && itemStarted && _state < StalfosState.Jumping &&
            Math.Abs(Mathf.FloorToInt(linkPosition.X) - Mathf.FloorToInt(Position.X)) +
            Math.Abs(Mathf.FloorToInt(linkPosition.Y) - Mathf.FloorToInt(Position.Y)) < _behavior.DodgeDistance)
        {
            _state = StalfosState.Jumping;
            _speedZ = _behavior.JumpSpeedZ;
            _jumpCollision = false;
            // Despite its name, ecom_updateCardinalAngleAwayFromTarget does
            // not round: it XORs the full objectGetAngleTowardEnemyTarget.
            _angle = OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition) ^ 0x10;
            SetAnimation(1);
            Visible = true;
            _sound(OracleSoundEngine.SndEnemyJump);
            return false;
        }

        switch (_state)
        {
            case StalfosState.SwitchHook:
                if (SwitchHookSubstate == 0) SwitchHookSubstate = 1;
                else if (SwitchHookSubstate == 3 &&
                    OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x20))
                {
                    _jumpCollision = true;
                    _state = StalfosState.Deciding;
                }
                QueueRedraw();
                return false;
            case StalfosState.Uninitialized:
                _state = StalfosState.Deciding;
                Visible = true;
                return false;

            case StalfosState.Deciding:
                // State $08 always consumes this first 1-in-8 attack roll.
                // Subid $00 cannot shoot, so every result falls through to
                // the shared random-walk selection and its second RNG call.
                int attackRoll = _random.Next().Value;
                if (Record.SubId == 2 && (attackRoll & _behavior.BoneChanceMask) == 0)
                {
                    _state = StalfosState.Firing;
                    return false;
                }
                BeginRandomWalk(linkPosition);
                return false;

            case StalfosState.Walking:
                _counter1--;
                if (_counter1 == 0)
                    _state = StalfosState.Deciding;
                BounceOffWallsAndHoles();
                Position +=
                    OracleObjectMovement.Shared.Delta(Record.SpeedRaw, _angle);
                QueueRedraw();
                AdvanceAnimation();
                return false;
            case StalfosState.Jumping:
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _behavior.JumpGravity))
                {
                    _state = StalfosState.Deciding;
                    SetAnimation(0);
                }
                else
                {
                    if ((_speedZ >> 8) == 0) _jumpCollision = true;
                    _movement.MoveAtAngle(_angle, _behavior.JumpSpeedRaw, allowHoles: true);
                }
                QueueRedraw();
                return false;
            case StalfosState.Firing:
                bool spawned = canSpawnPart?.Invoke() ?? true;
                BeginRandomWalk(linkPosition);
                return spawned;
        }
        return false;
    }

    public bool TakeSwordHit(Vector2 sourcePosition)
        => TakeSwordHit(sourcePosition, 2);

    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead && !DiedInHazard &&
        _state == StalfosState.SwitchHook && SwitchHookSubstate < 3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition)
    {
        KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), linkPosition.Floor()) ^ 0x10;
        _state = StalfosState.SwitchHook;
        SwitchHookSubstate = 0;
    }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    {
        Position = position.Floor() + Position - Position.Floor();
        _zFixed = (zHigh << 8) | (_zFixed & 0xff);
        QueueRedraw();
    }
    public void SwapSwitchHook() => SwitchHookSubstate = 2;
    public void ReleaseSwitchHook()
    {
        if (SwitchHookHeld) SwitchHookSubstate = 3;
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (IsDead || !CollisionEnabled || InvincibilityCounter > 0)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    private void BeginRandomWalk(Vector2 linkPosition)
    {
        OracleRandomResult result = _random.Next();
        _counter1 = _behavior.MoveCounterBase +
            (result.Value & _behavior.MoveCounterMask);
        _angle = (result.Low & 0x0f) == 1
            ? OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition)
            : result.High & 0x1f;
        _state = StalfosState.Walking;
        RestartAnimation(0);
    }

    private void BounceOffWallsAndHoles()
    {
        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            Position,
            _angle,
            IsWallOrHole);
    }

    private bool IsWallOrHole(Vector2I point)
    {
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height)
        {
            return true;
        }

        // ecom_bounceOffWallsAndHoles uses checkTileCollisionAt_disallowHoles:
        // SPECIALCOLLISION_HOLE $10 includes water and lava, not just pits.
        return _room.IsSolidForEnemyMovement(point, holesAreWalls: true);
    }
}

internal enum StalfosState
{
    Uninitialized = 0,
    SwitchHook = 3,
    Deciding = 8,
    Walking = 9,
    Jumping = 11,
    Firing = 12
}
