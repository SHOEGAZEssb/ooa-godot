using Godot;
using System;

namespace oracleofages;

internal partial class KeeseFirePart : TransitionOffsetNode2D
{
    private readonly KeeseFireRecord _record = KeeseFireRecord.Shared;
    private EnemyAnimationPlayer _animation = null!;
    private Action<int> _sound = null!;
    private bool _initialized;
    private int _invincibility;
    private bool _collisionCleared;
    internal bool CollisionEnabled => !Finished && !_collisionCleared && _initialized;
    // partCode20 ignores PARTSTATUS_DEAD; only its collision bit changes.
    internal void ClearHealthAndCollision() => _collisionCleared = true;
    internal bool Finished { get; private set; }
    internal int Counter { get; private set; }
    internal int ZHigh { get; private set; }
    internal Rect2 CollisionBounds => new(Position - Vector2.One * _record.Radius, Vector2.One * (_record.Radius * 2));
    internal void Initialize(Vector2 position, int zHigh, Action<int> sound)
    {
        Position = position;
        ZHigh = zHigh;
        _sound = sound;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{_record.Sprite}.png"), [_record.Animation], _record.TileBase, _record.Palette);
        _animation.SetAnimation(0);
        Visible = false;
    }
    internal void UpdateFrame()
    {
        if (Finished) return;
        if (_invincibility > 0) _invincibility--;
        if (!_initialized)
        {
            _collisionCleared = false; // partLoadGraphicsAndProperties reloads state-zero properties.
            _initialized = true;
            Counter = _record.Frames;
            Visible = true;
            return;
        }
        if (--Counter == 0) { Finished = true; Visible = false; return; }
        _animation.Advance();
    }
    internal void HandleLinkContact(Player player)
    {
        if (!CollisionEnabled || !player.EnemyContactHeightOverlaps(ZHigh)) return;
        if (player.Inventory.ShieldLevel >= 3 && player.IsUsingShield && player.ShieldCollisionBounds.Intersects(CollisionBounds))
        {
            if (_invincibility == 0 && player.CanAcceptShieldCollision)
            {
                _invincibility = _record.ShieldInvincibility;
                player.ApplyShieldCollisionRecoil(Position, 0, _record.ShieldKnockback);
                _sound(_record.ShieldSound);
            }
            return;
        }
        if (player.OverlapsEnemyCollision(CollisionBounds))
            player.ApplyEnemyContactDamage(Position, _record.Damage, RingDamageSource.Generic, _record.LinkInvincibility, _record.LinkKnockback);
    }
    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + Vector2.Down * ZHigh + TransitionDrawOffset);
    }
}
