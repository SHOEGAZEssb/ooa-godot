using Godot;
using System;

namespace oracleofages;

internal partial class StalfosBoneProjectile : TransitionOffsetNode2D
{
    private readonly ProjectileBounceBehaviorProfile _bounce = EnemyBehaviorTables.Shared.ProjectileBounce;
    private OracleRoomData _room = null!;
    private StalfosBoneRecord _record;
    private EnemyAnimationPlayer _animation = null!;
    private Func<Vector2, Vector2> _worldToScreen = null!;
    private int _zFixed;
    private int _speedZ;
    private bool _collisionPending;
    private bool _deletePending;
    private bool _healthCleared;
    internal void ClearHealthAndCollision() => _healthCleared = true;
    internal bool Finished { get; private set; }
    internal HostileProjectileState State { get; private set; }
    internal int Angle { get; private set; }
    internal int Counter { get; private set; }
    internal int ZFixed => _zFixed;
    internal int AnimationFrame => _animation.FrameIndex;
    internal bool CollisionEnabled => !Finished && !_healthCleared && !_collisionPending && !_deletePending && State != HostileProjectileState.Bouncing;
    internal Rect2 CollisionBounds => CollisionEnabled
        ? new(Position - new Vector2(_record.RadiusX, _record.RadiusY), new Vector2(_record.RadiusX * 2, _record.RadiusY * 2))
        : new(Position, Vector2.Zero);

    internal void Initialize(StalfosBoneRecord record, OracleRoomData room, Vector2 position,
        Func<Vector2, Vector2> worldToScreen, int zHigh = 0)
    {
        _record = record;
        _room = room;
        _worldToScreen = worldToScreen;
        Position = position;
        _zFixed = zHigh << 8;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{record.Sprite}.png"),
            [record.Animation], record.TileBase, record.Palette);
        _animation.SetAnimation(0);
        Visible = false;
    }

    internal void UpdateFrame(Player player, int frameCounter)
    {
        if (Finished) return;
        if (State == HostileProjectileState.Initializing)
            _healthCleared = false; // State-zero part properties overwrite the raw write.
        if (_deletePending) { Finish(); return; }
        if (_healthCleared)
        {
            // partCode1c takes its nonzero-status branch on EVERY update.
            // With collision bit 7 already clear, the bounce helper returns
            // before setting counter, speedZ, angle or animation.
            _collisionPending = false;
            State = HostileProjectileState.Bouncing;
            return;
        }
        if (_collisionPending)
        {
            _collisionPending = false;
            BeginBounce();
            return;
        }
        if (State == HostileProjectileState.Initializing)
        {
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position, player.Position);
            State = HostileProjectileState.Flying;
            Visible = true;
            return;
        }
        if (State == HostileProjectileState.Bouncing)
        {
            if (--Counter == 0) { Finish(); return; }
            OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _bounce.Gravity);
            Position += OracleObjectMovement.Shared.Delta(_bounce.SpeedRaw, Angle);
            if ((frameCounter & 1) == 0) _animation.Advance();
            QueueRedraw();
            return;
        }
        // partCommon_checkTileCollisionOrOutOfBounds precedes movement;
        // objectCheckWithinScreenBoundary follows it.
        if (Position.X < 0 || Position.X >= _room.Width || Position.Y < 0 || Position.Y >= _room.Height ||
            _room.GetTerrainInfo(Position).Collision == 0xff)
        { Finish(); return; }
        if (_room.IsSolid(Position)) { BeginBounce(); return; }
        Position += OracleObjectMovement.Shared.Delta(_record.SpeedRaw, Angle);
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(_worldToScreen(Position)))
        { Finish(); return; }
        _animation.Advance();
        QueueRedraw();
    }

    internal void HandleLinkContact(Player player)
    {
        if (!CollisionEnabled || State == HostileProjectileState.Initializing || _collisionPending) return;
        if (player.TryBlockWithShield(CollisionBounds))
        {
            _collisionPending = true;
            return;
        }
        if (player.OverlapsEnemyCollision(CollisionBounds) &&
            player.ApplyEnemyContactDamage(Position, _record.DamageQuarters, RingDamageSource.Generic))
            _deletePending = true; // part var2a=$80 deletes on its next dispatch; no bounce.
    }

    internal bool DeflectWithSword()
    {
        if (!CollisionEnabled || _collisionPending) return false;
        _collisionPending = true;
        return true;
    }

    private void BeginBounce()
    {
        State = HostileProjectileState.Bouncing;
        Counter = _bounce.Frames;
        _speedZ = _bounce.InitialSpeedZ;
        Angle ^= 0x10;
        _animation.SetAnimation(0);
        QueueRedraw();
    }

    private void Finish() { Finished = true; Visible = false; }
    public override void _Draw()
    {
        if (!Finished && Visible)
            DrawTexture(_animation.CurrentTexture,
                _animation.CurrentOffset + new Vector2(0, _zFixed >> 8) + TransitionDrawOffset);
    }
}
