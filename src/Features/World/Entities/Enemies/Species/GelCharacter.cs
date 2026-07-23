using Godot;
using System;

namespace oracleofages;

public partial class GelCharacter : EnemyCharacter
{

    private const int InitialSpeedZ = -0x200;
    private const int Gravity = 0x28;
    private const int AttachedFrames = 120;

    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private EnemyVerticalMotion _verticalMotion = null!;
    private GelState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private bool _collisionEnabled;

    public bool DiedInHazard { get; private set; }
    public HazardType DeathHazard { get; private set; }
    public bool IsAttached => !IsDead && _state == GelState.Attached;
    internal GelDefinition Definition { get; private set; }
    internal GelState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int ZFixed => _verticalMotion.ZFixed;
    internal int CurrentAnimationFrame => AnimationFrame;
    internal override bool CollisionEnabled => _collisionEnabled;
    protected override bool DrawsAnimation => !IsDead && Visible;
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + (_verticalMotion.ZFixed >> 8));

    internal void Initialize(
        GelDefinition definition,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Definition = definition;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _verticalMotion = new EnemyVerticalMotion(this, Gravity);
        _state = GelState.Waiting;
        _counter1 = 0x10;
        _collisionEnabled = true;

        string[] encodedAnimations =
        {
            definition.NormalAnimation,
            definition.AttachedAnimation,
            definition.ShakeAnimation
        };
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(
                definition.Health,
                definition.CollisionRadiusX,
                definition.CollisionRadiusY,
                definition.SpriteName,
                encodedAnimations,
                definition.TileBase,
                definition.Palette));
        RestartAnimation(0);
    }

    internal void UpdateFrame(
        Vector2 linkPosition,
        Vector2I linkFacing,
        bool anyButtonJustPressed)
    {
        if (IsDead)
            return;

        if (_state == GelState.Attached)
        {
            Position = linkPosition;
            _counter2--;
            if (_counter2 <= 0)
            {
                BeginHop(AngleAwayFromFacing(linkFacing));
                return;
            }
            if (anyButtonJustPressed)
                _counter2 = Math.Max(1, _counter2 - 3);
            if ((_counter2 & 0x03) == 0)
                ZIndex = ZIndex <= 10 ? 11 : 9;
            AdvanceAnimation();
            QueueRedraw();
            return;
        }

        if (_verticalMotion.ZFixed == 0 && _collisionEnabled && _movement.IsOnHazard)
        {
            DeathHazard = _movement.Hazard;
            DiedInHazard = true;
            Finish();
            return;
        }

        switch (_state)
        {
            case GelState.Waiting:
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return;
                if ((_random.Next().Value & 0x07) == 0)
                {
                    _state = GelState.Shaking;
                    _counter1 = 0x30;
                    RestartAnimation(2);
                }
                else
                {
                    _state = GelState.Inching;
                    _counter1 = 0x08;
                    _angle = OracleObjectMath.AngleToward(Position, linkPosition);
                }
                return;

            case GelState.Inching:
                _movement.MoveAtAngle(_angle, 0.25f, allowHoles: false);
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return;
                _state = GelState.Waiting;
                _counter1 = 0x10;
                return;

            case GelState.Shaking:
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return;
                BeginHop(OracleObjectMath.AngleToward(Position, linkPosition));
                return;

            case GelState.Hopping:
                _movement.MoveAtAngle(_angle, 1.0f, allowHoles: true);
                if (!_verticalMotion.Update())
                    return;
                _state = GelState.Waiting;
                _counter1 = 0x10;
                _collisionEnabled = true;
                RestartAnimation(0);
                return;
        }
    }

    internal void AttachToLink(Vector2 linkPosition)
    {
        if (IsDead || IsAttached)
            return;
        Position = linkPosition;
        _state = GelState.Attached;
        _counter2 = AttachedFrames;
        _verticalMotion.Reset();
        _collisionEnabled = false;
        RestartAnimation(1);
        ZIndex = 11;
    }

    public bool TakeSwordHit()
        => TakeSwordHit(2);

    internal bool TakeSwordHit(int damage)
    {
        if (IsDead || IsAttached)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    internal void SetStateForValidation(
        GelState state,
        int counter1 = 0,
        int counter2 = 0,
        int animation = -1)
    {
        _state = state;
        _counter1 = counter1;
        _counter2 = counter2;
        if (animation >= 0)
            RestartAnimation(animation);
    }

    private void BeginHop(int angle)
    {
        _state = GelState.Hopping;
        _verticalMotion.SpeedZ = InitialSpeedZ;
        _angle = angle & 0x1f;
        // gel_beginHop does not alter collisionType. A Gel hopping normally
        // therefore stays enabled, while a Gel whose Link collision disabled
        // it stays disabled until state $0b restores bit 7 on landing.
        ZIndex = 10;
        RestartAnimation(0);
    }

    private int AngleAwayFromFacing(Vector2I facing)
    {
        if (facing == Vector2I.Zero)
            return _random.Next().Value & 0x1f;
        int linkAngle = facing == Vector2I.Up ? 0x00
            : facing == Vector2I.Right ? 0x08
            : facing == Vector2I.Down ? 0x10
            : 0x18;
        return (linkAngle + 0x10) & 0x1f;
    }

}

internal enum GelState
{
    Waiting = 8,
    Inching = 9,
    Shaking = 10,
    Hopping = 11,
    Attached = 13
}
