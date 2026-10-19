using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ENEMY_CROW ($41), subid $00. The perched Crow turns toward Link, rises for
/// 25 updates when approached, then charges with the original 32-step angle,
/// SPEED_140 movement, random four-step aim offset, and eight-update steering.
/// </summary>
public partial class CrowCharacter : EnemyCharacter, ITerrainShadowSource
{
    internal override bool InitializationPending => !_initialized;

    int? ITerrainShadowSource.TerrainShadowZHigh => Z;

    private readonly CrowBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Crow;
    private OracleRandom _random = null!;
    private Vector2 _precisePosition;
    private CrowState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _z;
    private bool _collisionEnabled;
    private bool _initialized;

    public CrowRecord Record { get; private set; }
    internal bool DeletedOutOfBounds { get; private set; }
    public override Rect2 CollisionBounds => CollisionEnabled && !IsDead
        ? new Rect2(
            Position - new Vector2(Record.CollisionRadiusX, Record.CollisionRadiusY),
            new Vector2(Record.CollisionRadiusX * 2, Record.CollisionRadiusY * 2))
        : new Rect2(Position, Vector2.Zero);
    internal CrowState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int Z => _z;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && _collisionEnabled;
    internal Vector2 PrecisePosition => _precisePosition;
    protected override bool DrawsAnimation => !IsDead && Visible;
    protected override Vector2 AnimationDrawOffset => new(-16, -16 + _z);

    internal void Initialize(
        CrowRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        if (record.SubId != 0)
            throw new InvalidOperationException(
                $"CrowCharacter only supports ENEMY_CROW subid $00, got ${record.SubId:x2}.");

        Record = record;
        _random = random;
        _precisePosition = position;

        InitializeEnemy(
            OracleObjectMath.ToPixelPosition(position),
            EnemyCharacterConfiguration.FromSprite(
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                [
                    record.PerchedRightAnimation,
                    record.PerchedLeftAnimation,
                    record.FlightRightAnimation,
                    record.FlightLeftAnimation
                ],
                record.TileBase,
                record.Palette));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.ScreenBoundary,
            precisePosition: () => _precisePosition,
            setPrecisePosition: SetKnockbackPosition);
        RestartAnimation(0);
        Visible = false;
    }

    internal void PrepareForScreenTransition()
    {
        if (_initialized) return;
        _random.Next(); // enemyStandardUpdate seeds var3d before crow state0.
        _initialized = true;
        Visible = true;
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;
        if (!_initialized)
        {
            PrepareForScreenTransition();
            return;
        }
        if (BeginFrame())
            return;

        switch (_state)
        {
            case CrowState.Perched:
                FaceTarget(linkPosition, flight: false);
                if (!WithinUnsignedApproachRange(linkPosition))
                    return;
                _state = CrowState.Rising;
                _counter1 = _behavior.RisingFrames;
                return;

            case CrowState.Rising:
                FaceTarget(linkPosition, flight: true);
                _counter1--;
                if (_counter1 == 0)
                {
                    BeginCharge(linkPosition);
                }
                else if ((_counter1 & 0x03) == 0)
                {
                    _z--;
                }
                AdvanceAnimation();
                return;

            case CrowState.Charging:
                if (!WithinChargeScreenBounds())
                {
                    DeletedOutOfBounds = true;
                    Finish();
                    return;
                }

                if (_counter2 > 0)
                {
                    _counter2--;
                    if (_counter2 != 0 && (_counter2 & 0x07) == 0)
                    {
                        int target = OracleObjectMovement.Shared.RelativeAngle(
                            Position, linkPosition);
                        _angle = OracleObjectMath.NudgeAngleTowards(_angle, target);
                        SetDirectionalAnimation(flight: true);
                    }
                }
                Position = ApplyMovementSpeed(ref _precisePosition, Record.SpeedRaw, _angle);
                AdvanceAnimation();
                return;
        }
    }

    internal bool TakeSwordHit(int damage)
    {
        if (IsDead || InvincibilityCounter > 0 || !CollisionEnabled)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    private bool WithinUnsignedApproachRange(Vector2 target)
    {
        int crowY = (int)Position.Y & 0xff;
        int crowX = (int)Position.X & 0xff;
        int targetY = (int)target.Y & 0xff;
        int targetX = (int)target.X & 0xff;
        return ((targetY - crowY + _behavior.ApproachRadiusY) & 0xff) <
                _behavior.ApproachRadiusY * 2 + 1 &&
            ((targetX - crowX + _behavior.ApproachRadiusX) & 0xff) <
                _behavior.ApproachRadiusX * 2 + 1;
    }

    private void BeginCharge(Vector2 linkPosition)
    {
        _state = CrowState.Charging;
        _counter2 = _behavior.ChargeFrames;
        _collisionEnabled = true;
        _angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, linkPosition);
        int offset = (_random.Next().Value & 0x04) == 0 ? -4 : 4;
        // crow_subid0_state9 adds in the raw byte without masking to $1f.
        // It retains the pre-offset animation until the next steering visit.
        _angle = (_angle + offset) & 0xff;
    }

    private void FaceTarget(Vector2 target, bool flight)
    {
        _angle = OracleObjectMovement.Shared.RelativeAngle(Position, target);
        SetDirectionalAnimation(flight);
    }

    private void SetDirectionalAnimation(bool flight)
    {
        // crow_setAnimationFromAngle deliberately preserves the current
        // animation when the target is exactly above or below the Crow.
        if ((_angle & 0x0f) == 0)
            return;
        int animation = (flight ? 2 : 0) + ((_angle & 0x10) == 0 ? 1 : 0);
        if (AnimationIndex != animation)
            RestartAnimation(animation);
    }

    private bool WithinChargeScreenBounds()
    {
        int y = (int)Mathf.Floor(_precisePosition.Y);
        int x = (int)Mathf.Floor(_precisePosition.X);
        return y >= 0 && y < _behavior.ScreenBottom &&
            x >= 0 && x < _behavior.ScreenRight;
    }

    private void SetKnockbackPosition(Vector2 position)
    {
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
    }
}

internal enum CrowState
{
    Perched,
    Rising,
    Charging
}
