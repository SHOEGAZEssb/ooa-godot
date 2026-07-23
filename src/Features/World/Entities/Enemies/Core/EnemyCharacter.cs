using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Record-neutral character mechanics shared by single-body enemies. Species
/// retain their typed imported records and state machines; this base owns only
/// animation, health, collision radii, invulnerability, and lifetime state.
/// </summary>
public abstract partial class EnemyCharacter : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private int _animationCount;
    private int _collisionRadiusX;
    private int _collisionRadiusY;
    private int _frameCounter;

    public bool IsDead { get; protected set; }
    internal int Health { get; set; }
    internal int InvincibilityCounter { get; set; }
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal Texture2D CurrentAnimationTexture => _animation.CurrentTexture;
    internal virtual bool CollisionEnabled => !IsDead && Visible;
    public virtual Rect2 CollisionBounds => new(
        Position - new Vector2(_collisionRadiusX, _collisionRadiusY),
        new Vector2(_collisionRadiusX * 2, _collisionRadiusY * 2));
    internal EnemyAnimationPlayer Animation => _animation;
    protected virtual bool DrawsAnimation => !IsDead && CollisionEnabled;
    protected virtual Vector2 AnimationDrawOffset => new(-16, -16);

    internal void InitializeEnemy(
        Vector2 position,
        EnemyCharacterConfiguration configuration,
        int initialAnimation = 0)
    {
        Position = position;
        Health = configuration.Health;
        IsDead = false;
        InvincibilityCounter = 0;
        _frameCounter = 0;
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

    protected void BeginFrame()
    {
        _frameCounter = (_frameCounter + 1) & 0xff;
        if (InvincibilityCounter > 0)
            InvincibilityCounter--;
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

    protected void Revive(int health)
    {
        IsDead = false;
        Health = health;
        InvincibilityCounter = 0;
        Visible = true;
    }

    protected void Finish()
    {
        IsDead = true;
        Visible = false;
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
            InvincibilityCounter > 0 && (_frameCounter & 4) == 0
                ? _animation.DamageTexture
                : _animation.CurrentTexture,
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
}

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
            DamagePalette: null,
            SourceGrayscaleInverted: true);
}
