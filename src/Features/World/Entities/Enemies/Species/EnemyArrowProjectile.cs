using Godot;

namespace oracleofages;

/// <summary>PART_ENEMY_ARROW $1a fired by Moblin archers.</summary>
public partial class EnemyArrowProjectile
    : TransitionOffsetNode2D, IHostileProjectile, ILinkContactEntity
{
    private readonly EnemyBehaviorTables _behavior = EnemyBehaviorTables.Shared;
    private Texture2D _texture = null!;
    private Texture2D _bounceTexture = null!;
    private HostileProjectileLifecycle _lifecycle = null!;

    public bool Finished => _lifecycle.Finished;
    public Rect2 CollisionBounds => _lifecycle.CollisionBounds;
    internal HostileProjectileState State => _lifecycle.State;
    internal int Angle => _lifecycle.Angle;
    internal int Counter => _lifecycle.Counter;
    internal int ZFixed => _lifecycle.ZFixed;
    internal int ElapsedFrames => _lifecycle.ElapsedFrames;
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
        if (subId is < 0 or > 1) throw new System.NotSupportedException($"enemyArrow.s: PART$1a subid${subId:x2}.");
        SubId = subId;
        var wall = _behavior.WallArrowShooter;
        int cardinalAngle = angle & ObjectAngle.CardinalMask;
        int direction = cardinalAngle / 8;
        Position =
            subId == 0 ? position + _behavior.EnemyArrowSpawnOffsets[direction].Vector : position.Floor();
        _lifecycle = new HostileProjectileLifecycle(
            this,
            room,
            new HostileProjectileProfile(
                "object_code/common/parts/enemyArrow.s:partCode1a",
                record.DamageQuarters,
                record.SpeedRaw,
                RingDamageSource.Generic,
                subId == 0 ? _behavior.EnemyArrowCollisionRadii[direction].Vector : wall.ArrowRadii,
                HostileProjectileTileProbe.CurrentPosition,
                HostileProjectileSwordWindow.AnyActiveState,
                ClearCollisionOnBounce: true,
                ResetZOnBounce: false,
                LaunchDelay: subId == 0 ? 0 : wall.LaunchDelay,
                NativePartBoundary: subId != 0,
                LaunchBoundaryOffsets: subId == 0 ? null : wall.BoundaryOffsets),
            cardinalAngle);
        string animation = direction switch
        {
            0 => record.UpAnimation,
            1 => record.RightAnimation,
            2 => record.DownAnimation,
            _ => record.LeftAnimation
        };
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        AnimationFrameDefinition frame =
            OracleGraphicsCache.GetAnimationDefinition(animation).Frames[0];
        _texture = NpcCharacter.BuildOamTexture(
            source, frame.EncodedOam, record.TileBase, record.Palette);
        AnimationFrameDefinition bounceFrame =
            OracleGraphicsCache.GetAnimationDefinition(
                record.BounceAnimation).Frames[0];
        _bounceTexture = NpcCharacter.BuildOamTexture(
            source, bounceFrame.EncodedOam, record.TileBase, record.Palette);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player) =>
        _lifecycle.UpdateFrame(player);

    internal void BindMovementMemory(OracleRuntimeState memory) => _lifecycle.BindMovementMemory(memory);
    internal void InitializeFlight() => _lifecycle.InitializeFlight();

    internal bool DeflectWithSword() => _lifecycle.DeflectWithSword();

    void IHostileProjectile.UpdateFrame(Player player) =>
        UpdateFrame(player);
    bool IHostileProjectile.DeflectWithSword() =>
        DeflectWithSword();
    void ILinkContactEntity.HandleLinkContact(Player player) => _lifecycle.HandleLinkContact(player);

    public override void _Draw()
    {
        if (!Finished)
            DrawTexture(
                State == HostileProjectileState.Bouncing
                    ? _bounceTexture
                    : _texture,
                new Vector2(-16, -16 + (ZFixed >> 8)) +
                TransitionDrawOffset);
    }
}
