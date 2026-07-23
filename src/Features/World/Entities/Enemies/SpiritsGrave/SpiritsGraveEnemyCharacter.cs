using Godot;
using System;

namespace oracleofages;

internal abstract partial class SpiritsGraveEnemyCharacter : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private int _health;
    private int _invincibility;
    private int _frameCounter;

    internal EnemyRecord Record { get; private set; }
    internal bool IsDead { get; private set; }
    internal int Health => _health;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationFrame => _animation.FrameIndex;
    internal Texture2D CurrentAnimationTexture => _animation.CurrentTexture;
    internal virtual bool CollisionEnabled => !IsDead && Visible;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(Record.RadiusX, Record.RadiusY),
        new Vector2(Record.RadiusX * 2, Record.RadiusY * 2));

    protected void InitializeEnemy(
        EnemyRecord record,
        Vector2 position,
        int initialAnimation = 0)
    {
        Record = record;
        Position = position;
        _health = record.Health;
        _animation = new EnemyAnimationPlayer(this, record.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(record.Sprites),
            record.Animations,
            record.TileBase,
            record.Palette,
            record.Palette == 5 ? 2 : 5,
            sourceGrayscaleInverted: record.SourceGrayscaleInverted);
        SetAnimation(initialAnimation);
        QueueRedraw();
    }

    internal virtual bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!CollisionEnabled || _invincibility > 0)
            return false;
        return ApplyDamage(damage, SwordInvincibilityFrames);
    }

    internal virtual bool TakeBurnHit(int damage) =>
        CollisionEnabled && ApplyDamage(damage, invincibilityFrames: 0);

    internal bool OverlapsLink(Vector2 linkPosition) =>
        CollisionEnabled &&
        Mathf.Abs(linkPosition.X - Position.X) < Record.RadiusX + 6 &&
        Mathf.Abs(linkPosition.Y - Position.Y) < Record.RadiusY + 6;

    protected void BeginFrame()
    {
        _frameCounter = (_frameCounter + 1) & 0xff;
        if (_invincibility > 0)
            _invincibility--;
    }

    protected virtual int SwordInvincibilityFrames => 0x15;

    protected void SetAnimation(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, Record.Animations.Length - 1);
        if (_animation.AnimationIndex != safeIndex || !_animation.HasFrames)
            _animation.SetAnimation(safeIndex);
    }

    protected void AdvanceAnimation(int decrement = 1) =>
        _animation.Advance(decrement);

    protected void Revive(int health)
    {
        IsDead = false;
        _health = health;
        _invincibility = 0;
        Visible = true;
    }

    protected void Finish()
    {
        IsDead = true;
        Visible = false;
    }

    public override void _Draw()
    {
        if (CollisionEnabled)
            DrawCurrentAnimation();
    }

    protected void DrawCurrentAnimation()
    {
        if (!_animation.HasFrames)
            return;
        DrawTexture(
            _invincibility > 0 && (_frameCounter & 4) == 0
                ? _animation.DamageTexture
                : _animation.CurrentTexture,
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    private bool ApplyDamage(int damage, int invincibilityFrames)
    {
        _health = Math.Max(0, _health - Math.Max(1, damage));
        if (_health == 0)
            Finish();
        else if (invincibilityFrames > 0)
            _invincibility = invincibilityFrames;
        return true;
    }
}
