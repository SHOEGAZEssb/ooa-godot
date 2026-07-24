using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Record-neutral character mechanics shared by single-body enemies. Species
/// retain their typed imported records and state machines; this base owns only
/// animation, health, collision radii, invulnerability, common recoil/hazard
/// handling, and lifetime state.
/// </summary>
public abstract partial class EnemyCharacter : TransitionOffsetNode2D
{
    // ecom_sideviewAdjacentWallOffsetTable is a stream of cumulative Y/X
    // deltas. Knockback always uses this smaller four-probe table, including
    // for top-down enemies such as Octoroks.
    private static readonly Vector2I[,] KnockbackProbeDeltas =
    {
        {
            new(-5, -4), new(9, 0), new(-4, 4), new(0, 0)
        },
        {
            new(-5, -4), new(9, 0), new(2, 3), new(0, 6)
        },
        {
            new(0, 0), new(0, 0), new(6, -1), new(0, 6)
        },
        {
            new(-5, 7), new(9, 0), new(2, -8), new(0, 6)
        },
        {
            new(-5, 7), new(9, 0), new(-4, -7), new(0, 0)
        },
        {
            new(-5, 7), new(9, 0), new(-11, -8), new(0, 6)
        },
        {
            new(0, 0), new(0, 0), new(-7, -1), new(0, 6)
        },
        {
            new(-5, -4), new(9, 0), new(-11, 3), new(0, 6)
        }
    };

    private EnemyAnimationPlayer _animation = null!;
    private int _animationCount;
    private int _collisionRadiusX;
    private int _collisionRadiusY;
    private int _globalFrameCounter;
    private OracleRoomData? _knockbackRoom;
    private EnemyKnockbackMotion _knockbackMotion;
    private Func<Vector2>? _knockbackPosition;
    private Action<Vector2>? _setKnockbackPosition;
    private bool _knockbackChecksHazards;
    private bool _pendingKnockbackDeath;
    private bool _completedKnockbackDeath;
    private OracleRoomData? _hazardRoom;
    private Func<int>? _hazardZ;
    private bool _animateWhileFallingInHole;
    private bool _hazardActive;
    private int _hazardCounter;
    private EnemyHazardEffectRequest? _pendingHazardEffect;

    public bool IsDead { get; protected set; }
    public bool DiedInHazard { get; protected set; }
    public HazardType DeathHazard { get; protected set; }
    internal int Health { get; set; }
    internal int InvincibilityCounter { get; set; }
    internal int KnockbackCounter { get; private set; }
    internal int KnockbackAngle { get; private set; }
    internal bool PendingKnockbackDeath => _pendingKnockbackDeath;
    internal bool IsFallingIntoHole =>
        _hazardActive && DeathHazard == HazardType.Hole;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal Texture2D CurrentAnimationTexture => _animation.CurrentTexture;
    internal Texture2D CurrentDrawTexture =>
        InvincibilityCounter > 0 && (_globalFrameCounter & 4) == 0
            ? _animation.DamageTexture
            : _animation.CurrentTexture;
    internal virtual bool CollisionEnabled =>
        !IsDead && !_pendingKnockbackDeath && !_hazardActive && Visible;
    public virtual Rect2 CollisionBounds => new(
        Position - new Vector2(_collisionRadiusX, _collisionRadiusY),
        new Vector2(_collisionRadiusX * 2, _collisionRadiusY * 2));
    internal EnemyAnimationPlayer Animation => _animation;
    protected virtual bool DrawsAnimation => !IsDead && Visible;
    protected virtual Vector2 AnimationDrawOffset => new(-16, -16);

    internal void InitializeEnemy(
        Vector2 position,
        EnemyCharacterConfiguration configuration,
        int initialAnimation = 0)
    {
        Position = position;
        Health = configuration.Health;
        IsDead = false;
        DiedInHazard = false;
        DeathHazard = HazardType.None;
        InvincibilityCounter = 0;
        KnockbackCounter = 0;
        KnockbackAngle = 0;
        _globalFrameCounter = 0;
        _knockbackRoom = null;
        _knockbackMotion = EnemyKnockbackMotion.None;
        _knockbackPosition = null;
        _setKnockbackPosition = null;
        _knockbackChecksHazards = false;
        _pendingKnockbackDeath = false;
        _completedKnockbackDeath = false;
        _hazardRoom = null;
        _hazardZ = null;
        _animateWhileFallingInHole = true;
        _hazardActive = false;
        _hazardCounter = 0;
        _pendingHazardEffect = null;
        _collisionRadiusX = configuration.CollisionRadiusX;
        _collisionRadiusY = configuration.CollisionRadiusY;
        _animationCount = configuration.Animations.Count;
        _animation = new EnemyAnimationPlayer(this, _animationCount);
        _animation.Load(
            configuration.Source,
            configuration.Animations,
            configuration.TileBase,
            configuration.Palette,
            configuration.DamagePalette,
            sourceGrayscaleInverted: configuration.SourceGrayscaleInverted);
        SetAnimation(initialAnimation);
        QueueRedraw();
    }

    /// <summary>
    /// Selects the movement helper called by enemy handlers for
    /// ENEMYSTATUS_KNOCKBACK. Collision response still decides whether a
    /// particular attack writes a knockback counter.
    /// </summary>
    private protected void ConfigureSwordKnockback(
        OracleRoomData room,
        EnemyKnockbackMotion motion,
        bool checksHazards = false,
        Func<Vector2>? precisePosition = null,
        Action<Vector2>? setPrecisePosition = null)
    {
        if ((precisePosition is null) != (setPrecisePosition is null))
        {
            throw new ArgumentException(
                "Knockback precise-position callbacks must be supplied together.");
        }
        if (motion == EnemyKnockbackMotion.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(motion), motion,
                "Configured sword knockback requires a movement policy.");
        }

        _knockbackRoom = room;
        _knockbackMotion = motion;
        _knockbackChecksHazards = checksHazards;
        _knockbackPosition = precisePosition;
        _setKnockbackPosition = setPrecisePosition;
        if (checksHazards)
            ConfigureHazards(room);
    }

    /// <summary>
    /// Enables the common ecom_checkHazards path. Ground probes use the
    /// original y+$05/x-$01 then y+$05/x+$01 order; optional Z reports the
    /// signed 8.8 altitude used to ignore airborne enemies.
    /// </summary>
    private protected void ConfigureHazards(
        OracleRoomData room,
        bool animateWhileFallingInHole = true,
        Func<int>? zPosition = null)
    {
        _hazardRoom = room;
        _animateWhileFallingInHole = animateWhileFallingInHole;
        _hazardZ = zPosition;
    }

    internal void ApplySwordKnockback(
        Vector2 sourcePosition,
        EnemyKnockbackStrength strength)
    {
        if (strength == EnemyKnockbackStrength.None)
            return;
        if (_knockbackMotion == EnemyKnockbackMotion.None ||
            _knockbackRoom is null)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} accepted sword knockback without a movement policy.");
        }

        (InvincibilityCounter, KnockbackCounter) = strength switch
        {
            EnemyKnockbackStrength.Low => (0x10, 0x08),
            EnemyKnockbackStrength.Normal => (0x15, 0x0b),
            EnemyKnockbackStrength.High => (0x1a, 0x0f),
            _ => throw new ArgumentOutOfRangeException(
                nameof(strength), strength, "Unknown enemy knockback strength.")
        };

        // enemyStandardUpdate prioritizes ENEMYSTATUS_KNOCKBACK over
        // ENEMYSTATUS_DEAD. A health-zero hit disables collision immediately,
        // but the object remains visible and completes every recoil update
        // before its death handler runs on the following update.
        if (IsDead)
        {
            IsDead = false;
            Visible = true;
            _pendingKnockbackDeath = true;
        }

        Vector2 source = OracleObjectMath.ToPixelPosition(sourcePosition);
        Vector2 target = OracleObjectMath.ToPixelPosition(CurrentKnockbackPosition);
        KnockbackAngle = source == target
            ? 0x08
            : OracleObjectMath.AngleToward(source, target);
        QueueRedraw();
    }

    internal void ApplySwordNoKnockback(
        Vector2 sourcePosition,
        EnemyKnockbackStrength strength)
    {
        _ = sourcePosition;
        if (IsDead || strength == EnemyKnockbackStrength.None)
            return;

        // COLLISIONEFFECT_SWORD_NO_KNOCKBACK uses ENEMYDMG_0c: the hit still
        // grants $20 invincibility updates, but never writes knockbackCounter.
        InvincibilityCounter = 0x20;
        KnockbackCounter = 0;
        QueueRedraw();
    }

    internal virtual bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (IsDead || !CollisionEnabled || InvincibilityCounter > 0)
            return false;
        return ApplyDamage(damage, SwordInvincibilityFrames);
    }

    internal virtual bool TakeBurnHit(int damage) =>
        !IsDead && CollisionEnabled && ApplyDamage(damage, invincibilityFrames: 0);

    public bool OverlapsLink(Vector2 linkPosition) =>
        !IsDead && CollisionEnabled &&
        Mathf.Abs(linkPosition.X - Position.X) < _collisionRadiusX + 6 &&
        Mathf.Abs(linkPosition.Y - Position.Y) < _collisionRadiusY + 6;

    /// <returns>
    /// True when the enemy's handler must return after applying its shared
    /// ENEMYSTATUS_KNOCKBACK update.
    /// </returns>
    protected bool BeginFrame()
    {
        if (_hazardActive)
        {
            UpdateActiveHazard();
            QueueRedraw();
            return true;
        }
        if (InvincibilityCounter > 0)
        {
            InvincibilityCounter--;
            QueueRedraw();
        }
        if (_pendingKnockbackDeath && KnockbackCounter == 0)
        {
            _pendingKnockbackDeath = false;
            _completedKnockbackDeath = true;
            CompleteKnockbackDeath();
            QueueRedraw();
            return true;
        }
        return UpdateKnockback();
    }

    protected virtual int SwordInvincibilityFrames => 0x15;

    protected void SetAnimation(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, _animationCount - 1);
        if (_animation.AnimationIndex != safeIndex || !_animation.HasFrames)
            RestartAnimation(safeIndex);
    }

    protected void RestartAnimation(int index) =>
        _animation.SetAnimation(Mathf.Clamp(index, 0, _animationCount - 1));

    protected void AdvanceAnimation(int decrement = 1) =>
        _animation.Advance(decrement);

    /// <returns>
    /// True when ecom_checkHazards consumed the remainder of this enemy's
    /// handler update.
    /// </returns>
    protected bool CheckHazards()
    {
        if (_hazardRoom is null || _hazardActive ||
            (_hazardZ?.Invoke() ?? 0) < 0)
        {
            return false;
        }

        Vector2 pixels = OracleObjectMath.ToPixelPosition(Position);
        HazardType hazard = _hazardRoom.GetTerrainInfo(
            pixels + new Vector2(-1, 5)).Hazard;
        int xNudge = -1;
        if (hazard == HazardType.None)
        {
            hazard = _hazardRoom.GetTerrainInfo(
                pixels + new Vector2(1, 5)).Hazard;
            xNudge = 1;
        }
        if (hazard == HazardType.None)
            return false;

        BeginHazard(hazard, xNudge);
        UpdateActiveHazard();
        QueueRedraw();
        return true;
    }

    protected void Revive(int health)
    {
        IsDead = false;
        _pendingKnockbackDeath = false;
        _completedKnockbackDeath = false;
        _hazardActive = false;
        _hazardCounter = 0;
        _pendingHazardEffect = null;
        DiedInHazard = false;
        DeathHazard = HazardType.None;
        Health = health;
        InvincibilityCounter = 0;
        KnockbackCounter = 0;
        Visible = true;
    }

    protected void Finish()
    {
        _pendingKnockbackDeath = false;
        IsDead = true;
        Visible = false;
    }

    protected virtual void CompleteKnockbackDeath() => Finish();

    internal bool TakeCompletedKnockbackDeath()
    {
        bool completed = _completedKnockbackDeath;
        _completedKnockbackDeath = false;
        return completed;
    }

    internal EnemyHazardEffectRequest? TakeHazardEffect()
    {
        EnemyHazardEffectRequest? effect = _pendingHazardEffect;
        _pendingHazardEffect = null;
        return effect;
    }

    internal void SetGlobalFrameCounter(int frameCounter)
    {
        _globalFrameCounter = frameCounter & 0xff;
        if (InvincibilityCounter > 0)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (DrawsAnimation)
            DrawCurrentAnimation();
    }

    protected void DrawCurrentAnimation()
    {
        if (!_animation.HasFrames)
            return;
        DrawTexture(
            CurrentDrawTexture,
            AnimationDrawOffset + TransitionDrawOffset);
    }

    protected bool ApplyDamage(int damage, int invincibilityFrames)
    {
        Health = Math.Max(0, Health - Math.Max(1, damage));
        if (Health == 0)
            Finish();
        else if (invincibilityFrames > 0)
            InvincibilityCounter = invincibilityFrames;
        return true;
    }

    private Vector2 CurrentKnockbackPosition =>
        _knockbackPosition?.Invoke() ?? Position;

    private bool UpdateKnockback()
    {
        if (KnockbackCounter == 0)
            return false;

        KnockbackCounter--;
        bool moved = MoveKnockback();
        if (!moved)
            KnockbackCounter = 0;

        if (_knockbackChecksHazards && CheckHazards())
        {
            QueueRedraw();
            return true;
        }
        QueueRedraw();
        return true;
    }

    private void BeginHazard(HazardType hazard, int xNudge)
    {
        DeathHazard = hazard;
        DiedInHazard = true;
        InvincibilityCounter = 0;
        KnockbackCounter = 0;
        _pendingKnockbackDeath = false;
        _completedKnockbackDeath = false;
        _hazardActive = true;
        _hazardCounter = 60;
        Position += new Vector2(xNudge, 0);
    }

    private void UpdateActiveHazard()
    {
        if (!_hazardActive)
            return;

        if (DeathHazard != HazardType.Hole)
        {
            CompleteHazard();
            return;
        }

        _hazardCounter--;
        if (_hazardCounter == 0)
        {
            CompleteHazard();
            return;
        }

        if ((_hazardCounter & 0x07) == 0)
        {
            Vector2 pixels = OracleObjectMath.ToPixelPosition(Position);
            var target = new Vector2(
                (Mathf.FloorToInt(pixels.X) & 0xf0) + 8,
                ((Mathf.FloorToInt(pixels.Y) + 5) & 0xf0) + 8);
            if (pixels == target)
            {
                CompleteHazard();
                return;
            }

            int angle = OracleObjectMath.AngleToward(pixels, target);
            Vector2 unit = OracleObjectMath.VectorFromAngle32(angle);
            Position += new Vector2(
                (int)(unit.X * 0x80) / 256.0f,
                (int)(unit.Y * 0x80) / 256.0f);
        }

        // The source subtracts three from animCounter (clamped at zero) and
        // then calls enemyAnimate. Zol and Gel deliberately use the variant
        // that leaves their current frame untouched.
        if (_animateWhileFallingInHole)
            AdvanceAnimation(3);
    }

    private void CompleteHazard()
    {
        _hazardActive = false;
        _hazardCounter = 0;
        _pendingHazardEffect = new EnemyHazardEffectRequest(
            Position, DeathHazard);
        Finish();
    }

    private bool MoveKnockback()
    {
        Vector2 position = CurrentKnockbackPosition;
        Vector2I probe = new(
            Mathf.FloorToInt(position.X),
            Mathf.FloorToInt(position.Y));
        int doubledAngle = KnockbackAngle * 2;
        int tableOffset = (doubledAngle & 0x0f) == 0
            ? doubledAngle
            : (doubledAngle & 0xf0) + 8;
        int octant = tableOffset / 8;
        bool verticalBlocked = false;
        bool horizontalBlocked = false;
        for (int index = 0; index < 4; index++)
        {
            probe += KnockbackProbeDeltas[octant, index];
            bool collision = IsKnockbackCollision(probe);
            if (index < 2)
                verticalBlocked |= collision;
            else
                horizontalBlocked |= collision;
        }

        // bank3.objectSpeedTable truncates each SPEED_200 component to signed
        // 8.8 precision. At this speed every nonzero component is at least
        // $63, which ecom_applyGivenVelocityGivenAdjacentWalls counts as
        // movement even when the high byte does not change.
        Vector2 unit = OracleObjectMath.VectorFromAngle32(KnockbackAngle);
        Vector2 movement = new(
            (int)(unit.X * 0x200) / 256.0f,
            (int)(unit.Y * 0x200) / 256.0f);
        if (verticalBlocked)
            movement.Y = 0;
        if (horizontalBlocked)
            movement.X = 0;
        if (movement == Vector2.Zero)
            return false;

        Vector2 destination = position + movement;
        if (_setKnockbackPosition is not null)
            _setKnockbackPosition(destination);
        else
            Position = destination;
        return true;
    }

    private bool IsKnockbackCollision(Vector2I point)
    {
        if (_knockbackRoom is null ||
            point.X < 0 || point.X >= _knockbackRoom.Width ||
            point.Y < 0 || point.Y >= _knockbackRoom.Height)
        {
            return true;
        }

        return _knockbackMotion == EnemyKnockbackMotion.Terrain &&
            _knockbackRoom.IsSolid(point);
    }
}

internal enum EnemyKnockbackMotion
{
    None,
    Terrain,
    ScreenBoundary
}

internal readonly record struct EnemyHazardEffectRequest(
    Vector2 Position,
    HazardType Hazard);

internal readonly record struct EnemyCharacterConfiguration(
    int Health,
    int CollisionRadiusX,
    int CollisionRadiusY,
    Image Source,
    IReadOnlyList<string> Animations,
    int TileBase,
    int Palette,
    int? DamagePalette,
    bool SourceGrayscaleInverted)
{
    internal static EnemyCharacterConfiguration FromImported(
        ImportedEnemyDefinition record) =>
        new(
            record.Health,
            record.RadiusX,
            record.RadiusY,
            EnemyVisualSource.LoadComposite(record.Sprites),
            record.Animations,
            record.TileBase,
            record.Palette,
            record.Palette == 5 ? 2 : 5,
            record.SourceGrayscaleInverted);

    internal static EnemyCharacterConfiguration FromSprite(
        int health,
        int collisionRadiusX,
        int collisionRadiusY,
        string spriteName,
        IReadOnlyList<string> animations,
        int tileBase,
        int palette) =>
        new(
            health,
            collisionRadiusX,
            collisionRadiusY,
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{spriteName}.png"),
            animations,
            tileBase,
            palette,
            DamagePalette: palette == 5 ? 2 : 5,
            SourceGrayscaleInverted: true);
}
