using Godot;

namespace Oracles;

internal partial class MoblinBoomerangProjectile : TransitionOffsetNode2D
{
    private readonly MoblinBoomerangBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.MoblinBoomerang;
    private readonly BoomerangMoblinCharacter _owner;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private readonly OracleRuntimeState? _movementMemory;
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
        EnemyProjectileVisualRecord visual,
        OracleRuntimeState? movementMemory = null)
    {
        _owner = owner;
        _room = room;
        _movementMemory = movementMemory;
        Position = position.Floor(); // ecom_spawnProjectile copies high Y/X into a fresh PART.
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
    internal bool InitializationPending => !_initialized;
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
            QueueRedraw();
            return;
        }
        if (!_returning)
        {
            // State 1 checks the current quarter-tile collision and decrements
            // counter1 before changing speed or applying movement.
            bool blocked = _room.HasSimpleCollision(Position);
            if (!blocked && _counter != 0) _counter--;
            if (blocked || _counter == 0)
            {
                BeginReturn();
            }
            else
            {
                _speedCounter = (_speedCounter - 1) & 255;
                if (_speedCounter == 0)
                {
                    _speedCounter = _behavior.DecelerationInterval;
                    if (_speed >= _behavior.SpeedStepRaw) _speed -= _behavior.SpeedStepRaw;
                }
                Move();
            }
        }
        else
        {
            if ((frameCounter & _behavior.ReturnAccelerationMask) == 0)
            {
                int candidate = (_speed + _behavior.SpeedStepRaw) & 255;
                // func_541a rejects the addition at $50; an early return
                // can already have speed $50 and must retain it.
                if (candidate < _behavior.ReturnMaximumSpeedRaw + _behavior.SpeedStepRaw)
                    _speed = candidate;
            }
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, _owner.Position);
            int radius = _behavior.CatchRadius;
            if ((byte)(OracleObjectPosition.HighByte(Position.X)-OracleObjectPosition.HighByte(_owner.Position.X)+radius) < radius*2+1 &&
                (byte)(OracleObjectPosition.HighByte(Position.Y)-OracleObjectPosition.HighByte(_owner.Position.Y)+radius) < radius*2+1)
            {
                Finish(returned: true);
                return;
            }
            Move();
        }
        if (player.OverlapsEnemyCollision(CollisionBounds))
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
    private void Move() => Position = NativeObjectMovement.ApplySpeed(
        _movementMemory, OracleObjectPosition.FromPixels(Position), _speed, _angle).PrecisePosition;

    private void Finish(bool returned)
    {
        if (returned)
            _owner.ReturnBoomerang();
        Finished = true;
        Visible = false;
    }
}
