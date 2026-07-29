using Godot;
using System;

namespace oracleofages;

internal partial class MoblinBoomerangProjectile : TransitionOffsetNode2D
{
    private readonly MoblinBoomerangBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.MoblinBoomerang;
    private readonly BoomerangMoblinCharacter _owner;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private int _angle;
    private int _counter;
    private int _speedCounter;
    private int _speed;
    private bool _initialized;
    private bool _returning;

    internal MoblinBoomerangProjectile(
        BoomerangMoblinCharacter owner,
        OracleRoomData room,
        Vector2 position,
        int angle,
        EnemyProjectileVisualRecord visual)
    {
        _owner = owner;
        _room = room;
        Position = position;
        _angle = angle;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(0);
    }

    internal bool Finished { get; private set; }
    internal int Counter => _counter;
    internal int SpeedRaw => _speed;
    internal bool Returning => _returning;
    internal Rect2 CollisionBounds => new(
        Position - Vector2.One * _behavior.CollisionRadius,
        Vector2.One * (_behavior.CollisionRadius * 2));

    internal void UpdateFrame(Player player, int frameCounter)
    {
        if (Finished)
            return;
        if (_owner.IsDead)
        {
            Finish(returned: false);
            return;
        }
        if (!_initialized)
        {
            // partCode21 state 0 initializes but does not move.
            _initialized = true;
            _counter = _behavior.OutboundFrames;
            _speedCounter = _behavior.DecelerationInterval;
            _speed = _behavior.InitialSpeedRaw;
            _animation.Advance();
            QueueRedraw();
            return;
        }
        if (!_returning)
        {
            // State 1 checks the current quarter-tile collision and decrements
            // counter1 before changing speed or applying movement.
            if (_room.IsSolid(Position) || --_counter == 0)
            {
                BeginReturn();
            }
            else
            {
                if (--_speedCounter == 0)
                {
                    _speedCounter = _behavior.DecelerationInterval;
                    _speed = Math.Max(
                        0, _speed - _behavior.SpeedStepRaw);
                }
                Position += OracleObjectMovement.Shared.Delta(_speed, _angle);
            }
        }
        else
        {
            if ((frameCounter & _behavior.ReturnAccelerationMask) == 0)
            {
                _speed = Math.Min(
                    _behavior.ReturnMaximumSpeedRaw,
                    _speed + _behavior.SpeedStepRaw);
            }
            Vector2 delta = _owner.Position - Position;
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, _owner.Position);
            if (Mathf.Abs(delta.X) <= _behavior.CatchRadius &&
                Mathf.Abs(delta.Y) <= _behavior.CatchRadius)
            {
                Finish(returned: true);
                return;
            }
            Position += OracleObjectMovement.Shared.Delta(_speed, _angle);
        }
        if (Mathf.Abs(player.Position.X - Position.X) < 8 &&
            Mathf.Abs(player.Position.Y - Position.Y) < 8)
        {
            player.ApplyEnemyContactDamage(
                Position, _behavior.DamageQuarters);
            BeginReturn();
        }
        _animation.Advance();
        QueueRedraw();
    }

    internal bool Deflect()
    {
        if (Finished)
            return false;
        BeginReturn();
        return true;
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        DrawTexture(
            _animation.CurrentTexture,
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    private void BeginReturn() => _returning = true;

    private void Finish(bool returned)
    {
        if (returned)
            _owner.ReturnBoomerang();
        Finished = true;
        Visible = false;
    }
}
