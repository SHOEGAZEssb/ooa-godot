using Godot;

namespace Oracles;

/// <summary>PART_ENEMY_ARROW $1a fired by Moblin archers.</summary>
public partial class EnemyArrowProjectile
    : TransitionOffsetNode2D, IHostileProjectile, ILinkContactEntity
{
    private EnemyArrowDatabase? _behaviorSourceCache;
    private EnemyArrowDatabase _behavior => _behaviorSourceCache ??= EnemyArrowDatabase.For(AssetRoot);
    private Vector2 _spawnOffset;
    private EnemyAnimationPlayer _animation = null!;
    private HostileProjectileLifecycle _lifecycle = null!;

    public bool Finished => _lifecycle.Finished;
    bool IHostileProjectile.InitializationPending => State == HostileProjectileState.Initializing;
    public Rect2 CollisionBounds => _lifecycle.CollisionBounds;
    internal HostileProjectileState State => _lifecycle.State;
    internal int Angle => _lifecycle.Angle;
    internal int Counter => _lifecycle.Counter;
    internal int ZFixed => _lifecycle.ZFixed;
    internal int ElapsedFrames => _lifecycle.ElapsedFrames;
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationCounter => _animation.RemainingUpdates;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal int SubId { get; private set; }
    internal int NativeState => State switch
    {
        HostileProjectileState.Initializing => 0,
        HostileProjectileState.Launching => 1,
        HostileProjectileState.Bouncing => SubId == 0 ? 2 : 3,
        _ => SubId == 0 ? 1 : 2
    };

    internal void Initialize(
        EnemyArrowRecord record,
        OracleRoomData room,
        Vector2 position,
        int angle,
        int subId = 0)
    {
        AssetRoot = record.AssetRoot;
        if (subId is < 0 or > 1) throw new System.NotSupportedException($"enemyArrow.s: PART$1a subid${subId:x2}.");
        SubId = subId;
        int cardinalAngle = angle & ObjectAngle.CardinalMask;
        int direction = cardinalAngle / 8;
        // ecom_spawnProjectile calls objectCopyPosition: only whole-pixel
        // XYZ are copied into a freshly cleared PART, never the parent's fractions.
        Position = new(OracleObjectPosition.HighByte(position.X), OracleObjectPosition.HighByte(position.Y));
        _spawnOffset = subId == 0 ? _behavior.SpawnOffsets[direction].Vector : Vector2.Zero;
        Visible = false;
        _lifecycle = new HostileProjectileLifecycle(
            this,
            room,
            new HostileProjectileProfile(
                "object_code/common/parts/enemyArrow.s:partCode1a",
                record.DamageQuarters,
                record.SpeedRaw,
                RingDamageSource.Generic,
                subId == 0 ? _behavior.CollisionRadii[direction].Vector : _behavior.LaunchRadii,
                HostileProjectileTileProbe.CurrentPosition,
                HostileProjectileSwordWindow.AnyActiveState,
                ResetZOnBounce: false,
                LaunchDelay: subId == 0 ? 0 : _behavior.LaunchDelay,
                NativePartBoundary: true,
                LaunchBoundaryOffsets: subId == 0 ? null : CommonEnemyBehavior.Shared.ProjectileBoundaryOffsets),
            cardinalAngle);
        _animation = new EnemyAnimationPlayer(this,5);
        _animation.Load(OracleGraphicsCache.LoadImage(AssetRoot + $"gfx/{record.SpriteName}.png"),
            [record.UpAnimation,record.RightAnimation,record.DownAnimation,record.LeftAnimation,record.BounceAnimation],
            record.TileBase,record.Palette);
        _animation.SetAnimation(direction);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        var previous = State;
        InitializePosition();
        _lifecycle.UpdateFrame(player);
        if (State == HostileProjectileState.Bouncing && !Finished)
        {
            if (previous != HostileProjectileState.Bouncing) _animation.SetAnimation(4);
            else _animation.Advance();
        }
    }

    internal void BindMovementMemory(OracleRuntimeState memory) => _lifecycle.BindMovementMemory(memory);
    internal void InitializeFlight()
    {
        InitializePosition();
        _lifecycle.InitializeFlight();
    }

    private void InitializePosition()
    {
        if (State != HostileProjectileState.Initializing) return;
        Position = new(unchecked((byte)(OracleObjectPosition.HighByte(Position.X) + (int)_spawnOffset.X)),
            unchecked((byte)(OracleObjectPosition.HighByte(Position.Y) + (int)_spawnOffset.Y)));
        Visible = true;
    }

    internal bool DeflectWithSword()
    {
        if (!_lifecycle.DeflectWithSword()) return false;
        _animation.SetAnimation(4);
        return true;
    }

    void IHostileProjectile.UpdateFrame(Player player) =>
        UpdateFrame(player);
    bool IHostileProjectile.DeflectWithSword() =>
        DeflectWithSword();
    void ILinkContactEntity.HandleLinkContact(Player player) => _lifecycle.HandleLinkContact(player);

    public override void _Draw()
    {
        if (!Finished)
            DrawTexture(_animation.CurrentTexture,
                _animation.CurrentOffset + Vector2.Down * (ZFixed >> 8) + SourceOamDrawOffset);
    }
}
