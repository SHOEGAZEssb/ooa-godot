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
    private static readonly int[] FireballSpeeds =
        [0x0f, 0x19, 0x23, 0x2d];
    private readonly HeadThwompProjectileKind _kind;
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly Action<int> _playSound;
    private readonly OracleRuntimeState? _memory;
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
    private int _speedZ;
    private bool _initialized;
    private bool _breaking;

    internal HeadThwompProjectile(
        HeadThwompProjectileSpawn spawn,
        DungeonInteractionVisual visual,
        DungeonInteractionVisual? impactVisual,
        OracleRoomData room,
        OracleRandom random,
        Action<int> playSound,
        OracleRuntimeState? memory = null)
    {
        _kind = spawn.Kind;
        _room = room;
        _random = random;
        _playSound = playSound;
        _memory = memory;
        Position = spawn.Position;
        _angle = spawn.Angle & ObjectAngle.Mask;
        _speed = spawn.Speed;
        _randomizeLaunch = spawn.RandomizeLaunch;
        Name = _kind == HeadThwompProjectileKind.Fireball
            ? "HeadThwompFireball"
            : "HeadThwompCircularProjectile";
        ZIndex = ObjectDrawPriority.BehindLinkZIndex; // both source PART handlers use visible82.
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
    bool IHostileProjectile.InitializationPending => !_initialized;
    public Rect2 CollisionBounds
    {
        get
        {
            Vector2 center = Position;
            Vector2 radii = new(_radiusX, _radiusY);
            return new Rect2(center - radii, radii * 2.0f);
        }
    }

    internal int Angle => _angle;
    internal int Speed => _speed;
    internal int ZFixed => 0;
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
                _playSound(SoundId.SndFallInHole);
            }
            else
            {
                _counter = 2;
                _turnPeriod = 2;
                _alternateCounter = 0;
                _playSound(SoundId.SndBeam);
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
        DrawTexture(
            ActiveAnimation.CurrentTexture,
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    private void UpdateFireball()
    {
        Position = NativeObjectMovement.ApplySpeed(_memory,
            OracleObjectPosition.FromPixels(Position), _speed, _angle).Add(_speedZ, 0).PrecisePosition;
        _speedZ = unchecked((short)(_speedZ + 0x20));
        if (_counter > 0)
            _counter--;
        if (_counter != 0)
            return; // PART$39's counter gates both bounds and floor probes.
        Vector2 point = Position.Floor();
        if (point.Y >= 0xb0)
        {
            Finish();
            return;
        }
        if (IsSolidExceptHole(point + Vector2.Down * 6))
        {
            if (_impactAnimation is not null)
            {
                _impactAnimation.SetAnimation(0);
                _breaking = true;
                _playSound(SoundId.SndBreakRock);
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
        if (_counter != 0)
            _counter--;
        if (_counter == 0)
        {
            _alternateCounter ^= 1;
            _turnPeriod = (_turnPeriod + _alternateCounter) & 0xff;
            _counter = _turnPeriod;
            _angle = (_angle + _speed) & ObjectAngle.Mask;
        }
        Position = NativeObjectMovement.ApplySpeed(_memory,
            OracleObjectPosition.FromPixels(Position), 0x64, _angle).PrecisePosition;
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
        if (!player.OverlapsEnemyCollision(CollisionBounds))
            return;
        player.ApplyEnemyContactDamage(
            Position,
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
        return !NativePartBoundary.IsInside(_room,Position,_angle,
            EnemyBehaviorTables.Shared.WallArrowShooter.BoundaryOffsets);
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
