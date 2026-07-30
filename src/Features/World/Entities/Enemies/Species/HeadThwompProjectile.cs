using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_HEAD_THWOMP_FIREBALL $39 and
/// PART_HEAD_THWOMP_CIRCULAR_PROJECTILE $3c.
/// </summary>
internal sealed partial class HeadThwompProjectile
    : TransitionOffsetNode2D, IHostileProjectile
{
    private const int LinkRadius = 6;
    private readonly HeadThwompProjectileKind _kind;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _animationCount;
    private int _angle;
    private readonly int _speed;
    private int _counter;
    private int _alternateCounter;
    private int _zFixed;
    private int _speedZ;
    private bool _breaking;

    internal HeadThwompProjectile(
        HeadThwompProjectileSpawn spawn,
        DungeonInteractionVisual visual,
        OracleRoomData room)
    {
        _kind = spawn.Kind;
        _room = room;
        Position = spawn.Position;
        _angle = spawn.Angle & 0x1f;
        _speed = spawn.Speed;
        Name = _kind == HeadThwompProjectileKind.Fireball
            ? "HeadThwompFireball"
            : "HeadThwompCircularProjectile";
        ZIndex = 11;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animationCount = visual.Animations.Length;
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(0);

        if (_kind == HeadThwompProjectileKind.Fireball)
        {
            _counter = 30;
            _speedZ = -0x3e0;
        }
        else
        {
            _counter = 2;
            _alternateCounter = 0;
        }
    }

    public bool Finished { get; private set; }
    public Rect2 CollisionBounds
    {
        get
        {
            Vector2 center = DrawPosition;
            Vector2 radii = _kind == HeadThwompProjectileKind.Fireball
                ? new Vector2(5, 5)
                : new Vector2(4, 4);
            return new Rect2(center - radii, radii * 2.0f);
        }
    }

    internal int Angle => _angle;
    internal int ZFixed => _zFixed;

    public void UpdateFrame(Player player)
    {
        if (Finished)
            return;

        _animation.Advance();
        if (_breaking)
        {
            if ((_animation.CurrentParameter & 0x80) != 0 ||
                --_counter == 0)
            {
                Finish();
            }
            else
            {
                QueueRedraw();
            }
            return;
        }
        if (_kind == HeadThwompProjectileKind.Fireball)
            UpdateFireball();
        else
            UpdateCircular();
        if (Finished)
            return;

        Rect2 linkBounds = new(
            player.Position - Vector2.One * LinkRadius,
            Vector2.One * (LinkRadius * 2));
        if (CollisionBounds.Intersects(linkBounds))
        {
            player.ApplyEnemyContactDamage(
                DrawPosition,
                2,
                _kind == HeadThwompProjectileKind.Circular
                    ? RingDamageSource.Beam
                    : RingDamageSource.Generic);
            Finish();
            return;
        }
        QueueRedraw();
    }

    public bool DeflectWithSword()
    {
        if (Finished)
            return false;
        Finish();
        return true;
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        Vector2 offset = _kind == HeadThwompProjectileKind.Fireball
            ? new Vector2(0, _zFixed / 256.0f)
            : Vector2.Zero;
        DrawTexture(
            _animation.CurrentTexture,
            new Vector2(-16, -16) + offset + TransitionDrawOffset);
    }

    private Vector2 DrawPosition =>
        Position + new Vector2(0, _zFixed / 256.0f);

    private void UpdateFireball()
    {
        Position += OracleObjectMovement.Shared.Delta(_speed, _angle);
        _zFixed += _speedZ;
        _speedZ += 0x20;
        if (_counter > 0)
            _counter--;
        Vector2 point = DrawPosition;
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y >= _room.Height + 16 ||
            point.Y < -32)
        {
            Finish();
            return;
        }
        if (_counter == 0 && _room.IsSolid(point))
        {
            if (_animation.AnimationIndex == 0 &&
                _animationCount > 1)
            {
                _animation.SetAnimation(1);
                _counter = 32;
                _speedZ = 0;
                _breaking = true;
                return;
            }
            Finish();
        }
    }

    private void UpdateCircular()
    {
        _counter--;
        if (_counter == 0)
        {
            _alternateCounter ^= 1;
            _counter = 2 + _alternateCounter;
            _angle = (_angle + (_speed < 0 ? -2 : 2)) & 0x1f;
        }
        Position += OracleObjectMovement.Shared.Delta(
            0x64, _angle);
        if (Position.X < -8 || Position.X >= _room.Width + 8 ||
            Position.Y < -8 || Position.Y >= _room.Height + 8)
        {
            Finish();
        }
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
    }
}

internal enum HeadThwompProjectileKind
{
    Fireball,
    Circular
}
