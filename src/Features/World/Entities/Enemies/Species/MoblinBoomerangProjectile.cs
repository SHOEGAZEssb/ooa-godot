using Godot;
using System;

namespace oracleofages;

internal partial class MoblinBoomerangProjectile : TransitionOffsetNode2D
{
    private readonly BoomerangMoblinCharacter _owner;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private int _angle;
    private int _counter;
    private int _speedCounter;
    private float _speed;
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
    internal float Speed => _speed;
    internal bool Returning => _returning;
    internal Rect2 CollisionBounds => new(Position - new Vector2(2, 2), new Vector2(4, 4));

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
            _counter = 0x2d;
            _speedCounter = 6;
            _speed = 2.0f;
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
                    _speedCounter = 6;
                    _speed = Math.Max(0.0f, _speed - 0.125f);
                }
                Position += OracleObjectMath.CardinalVector(_angle) * _speed;
            }
        }
        else
        {
            if ((frameCounter & 3) == 0)
                _speed = Math.Min(1.875f, _speed + 0.125f);
            Vector2 delta = _owner.Position - Position;
            _angle = OracleObjectMath.AngleToward(Position, _owner.Position);
            if (Mathf.Abs(delta.X) <= 4 && Mathf.Abs(delta.Y) <= 4)
            {
                Finish(returned: true);
                return;
            }
            Position += OracleObjectMath.VectorFromAngle32(_angle) * _speed;
        }
        if (Mathf.Abs(player.Position.X - Position.X) < 8 &&
            Mathf.Abs(player.Position.Y - Position.Y) < 8)
        {
            player.ApplyEnemyContactDamage(Position, 2);
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
