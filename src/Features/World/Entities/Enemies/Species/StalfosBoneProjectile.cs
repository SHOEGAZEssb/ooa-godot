using Godot;
using System;

namespace Oracles;

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
    private OracleRuntimeState? _movementMemory;
    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;
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
        _animation.Load(OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{record.Sprite}.png"),
            [record.Animation], record.TileBase, record.Palette);
        _animation.SetAnimation(0);
        Visible = false;
    }

    internal void UpdateFrame(Player player, int frameCounter) =>
        UpdateFrame(player.EnemyContactPosition, frameCounter);

    internal void UpdateFrame(Vector2 linkTarget, int frameCounter)
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
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position, linkTarget);
            State = HostileProjectileState.Flying;
            Visible = true;
            return;
        }
        if (State == HostileProjectileState.Bouncing)
        {
            Counter = Math.Max(0, Counter - 1);
            if (Counter == 0) { Finish(); return; }
            OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _bounce.Gravity);
            Move(_bounce.SpeedRaw);
            if ((frameCounter & 1) == 0) _animation.Advance();
            QueueRedraw();
            return;
        }
        // partCommon_checkTileCollisionOrOutOfBounds precedes movement;
        // objectCheckWithinScreenBoundary follows it.
        Vector2 point = new(OracleObjectPosition.HighByte(Position.X), OracleObjectPosition.HighByte(Position.Y));
        if (_room.GetTerrainInfo(point).Collision == 0xff)
        { Finish(); return; }
        if (_room.IsSolid(point)) { BeginBounce(); return; }
        Move(_record.SpeedRaw);
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
        if (player.OverlapsEnemyCollision(CollisionBounds, _zFixed >> 8) &&
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

    private void Move(int speed) => Position = NativeObjectMovement.ApplySpeed(
        _movementMemory, OracleObjectPosition.FromPixels(Position), speed, Angle).PrecisePosition;

    private void Finish() { Finished = true; Visible = false; }
    public override void _Draw()
    {
        if (!Finished && Visible)
            DrawTexture(_animation.CurrentTexture,
                _animation.CurrentOffset + new Vector2(0, _zFixed >> 8) + TransitionDrawOffset);
    }
}
