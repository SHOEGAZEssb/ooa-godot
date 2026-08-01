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
    private static readonly int[] FireballSpeeds =
        [0x0f, 0x19, 0x23, 0x2d];
    private readonly HeadThwompProjectileKind _kind;
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly Action<int> _playSound;
    private readonly EnemyAnimationPlayer _animation;
    private readonly EnemyAnimationPlayer? _impactAnimation;
    private int _angle;
    private int _speed;
    private readonly bool _randomizeLaunch;
    private int _counter;
    private int _alternateCounter;
    private int _turnPeriod;
    private int _radiusY;
    private int _radiusX;
    private int _zFixed;
    private int _speedZ;
    private bool _initialized;
    private bool _breaking;

    internal HeadThwompProjectile(
        HeadThwompProjectileSpawn spawn,
        DungeonInteractionVisual visual,
        DungeonInteractionVisual? impactVisual,
        OracleRoomData room,
        OracleRandom random,
        Action<int> playSound)
    {
        _kind = spawn.Kind;
        _room = room;
        _random = random;
        _playSound = playSound;
        Position = spawn.Position;
        _angle = spawn.Angle & 0x1f;
        _speed = spawn.Speed;
        _randomizeLaunch = spawn.RandomizeLaunch;
        Name = _kind == HeadThwompProjectileKind.Fireball
            ? "HeadThwompFireball"
            : "HeadThwompCircularProjectile";
        ZIndex = 11;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(0);

        if (_kind == HeadThwompProjectileKind.Fireball)
        {
            DungeonInteractionVisual impact = impactVisual ??
                throw new ArgumentNullException(
                    nameof(impactVisual),
                    "Head Thwomp fireballs require their fixed-bank impact visual.");
            _impactAnimation = new EnemyAnimationPlayer(
                this, impact.Animations.Length);
            _impactAnimation.Load(
                EnemyVisualSource.LoadComposite(impact.Sprites),
                impact.Animations,
                impact.TileBase,
                impact.Palette,
                sourceGrayscaleInverted:
                    impact.SourceGrayscaleInverted);
            _impactAnimation.SetAnimation(0);
            _radiusY = 5;
            _radiusX = 5;
        }
        else
        {
            _radiusY = 4;
            _radiusX = 4;
        }
        Visible = false;
    }

    public bool Finished { get; private set; }
    public Rect2 CollisionBounds
    {
        get
        {
            Vector2 center = DrawPosition;
            Vector2 radii = new(_radiusX, _radiusY);
            return new Rect2(center - radii, radii * 2.0f);
        }
    }

    internal int Angle => _angle;
    internal int Speed => _speed;
    internal int ZFixed => _zFixed;
    internal bool Breaking => _breaking;
    internal int AnimationIndex => _breaking ? 1 : _animation.AnimationIndex;
    internal int AnimationFrameIndex => ActiveAnimation.FrameIndex;
    internal Texture2D CurrentTexture => ActiveAnimation.CurrentTexture;

    private EnemyAnimationPlayer ActiveAnimation =>
        _breaking ? _impactAnimation! : _animation;

    public void UpdateFrame(Player player)
    {
        if (Finished)
            return;

        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            if (_kind == HeadThwompProjectileKind.Fireball)
            {
                if (_randomizeLaunch)
                {
                    _angle = (_random.Next().Value & 0x10) + 0x08;
                    _speed = FireballSpeeds[_random.Next().Value & 3];
                }
                _counter = 30;
                _speedZ = -0x3e0;
                _playSound(OracleSoundEngine.SndFallInHole);
            }
            else
            {
                _counter = 2;
                _turnPeriod = 2;
                _alternateCounter = 0;
                _playSound(OracleSoundEngine.SndBeam);
            }
            QueueRedraw();
            return;
        }

        if (_breaking)
        {
            EnemyAnimationPlayer impact = ActiveAnimation;
            if ((impact.CurrentParameter & 0x80) != 0)
            {
                Finish();
            }
            else
            {
                SetImpactRadii(impact.CurrentParameter);
                impact.Advance();
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
        if (_breaking)
        {
            QueueRedraw();
            return;
        }

        TryDamagePlayer(player);
        if (Finished)
            return;
        _animation.Advance();
        QueueRedraw();
    }

    public bool DeflectWithSword()
    {
        // PART $39 uses active-collision row $04 (all item bits clear), while
        // PART $3c's row $06 accepts Ember Seed only. Neither responds to a
        // sword collision.
        return false;
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        Vector2 offset = _kind == HeadThwompProjectileKind.Fireball
            ? new Vector2(0, _zFixed / 256.0f)
            : Vector2.Zero;
        DrawTexture(
            ActiveAnimation.CurrentTexture,
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
        if (point.Y >= 0xb0)
        {
            Finish();
            return;
        }
        if (_counter == 0 &&
            IsSolidExceptHole(point + Vector2.Down * 6))
        {
            if (_impactAnimation is not null)
            {
                _impactAnimation.SetAnimation(0);
                _speedZ = 0;
                _breaking = true;
                _playSound(OracleSoundEngine.SndBreakRock);
                return;
            }
            Finish();
        }
    }

    private void UpdateCircular()
    {
        if (IsCircularOutOfBounds())
        {
            Finish();
            return;
        }
        _counter--;
        if (_counter == 0)
        {
            _alternateCounter ^= 1;
            _turnPeriod += _alternateCounter;
            _counter = _turnPeriod;
            _angle = (_angle + (_speed < 0 ? -2 : 2)) & 0x1f;
        }
        Position += OracleObjectMovement.Shared.Delta(
            0x64, _angle);
    }

    private void TryDamagePlayer(Player player)
    {
        // PART $39 uses active-collision row $04, whose Link and item bits
        // are all clear. PART $3c uses row $06: it damages Link, and only
        // level-2/3 shields select COLLISIONEFFECT_$1f and destroy it.
        if (_kind != HeadThwompProjectileKind.Circular)
            return;
        if (player.TryBlockWithShield(CollisionBounds, minimumLevel: 2))
        {
            Finish();
            return;
        }
        Rect2 linkBounds = new(
            player.Position - Vector2.One * LinkRadius,
            Vector2.One * (LinkRadius * 2));
        if (!CollisionBounds.Intersects(linkBounds))
            return;
        player.ApplyEnemyContactDamage(
            DrawPosition,
            4,
            RingDamageSource.Generic);
        Finish();
    }

    private void SetImpactRadii(int parameter)
    {
        ReadOnlySpan<(int Y, int X)> radii =
        [
            (0x04, 0x09),
            (0x06, 0x0b),
            (0x09, 0x0c),
            (0x0a, 0x0d),
            (0x0b, 0x0e)
        ];
        int index = parameter >> 1;
        if ((uint)index >= (uint)radii.Length)
        {
            throw new InvalidOperationException(
                $"PART_HEAD_THWOMP_FIREBALL impact parameter ${parameter:x2} is invalid.");
        }
        (_radiusY, _radiusX) = radii[index];
    }

    private bool IsSolidExceptHole(Vector2 point) =>
        _room.IsSolid(point) &&
        _room.GetTerrainInfo(point).Hazard != HazardType.Hole;

    private bool IsCircularOutOfBounds()
    {
        ReadOnlySpan<Vector2I> offsets =
        [
            new(-5, 0), new(-5, 4), new(0, 4), new(4, 4),
            new(4, 0), new(4, -5), new(0, -5), new(-5, -5)
        ];
        int roundedAngle = (_angle & 7) == 0
            ? _angle
            : (_angle & 0x18) + 4;
        Vector2 point = Position + offsets[(roundedAngle & 0x1f) >> 2];
        return point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height;
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
