using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Common PART_ORB $03 placed directly or spawned by an event.</summary>
internal sealed partial class DungeonOrbRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, ISwordHittableRoomEntity,
    IPostObjectItemCollisionRoomEntity, ISeedCollisionTarget,
    IObjectCollisionHeightRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    ISwitchHookHittableRoomEntity, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, INativePartHealthRoomEntity
{
    private readonly DungeonMechanicDatabase _data;
    private readonly PartOrbDatabase _orbData = PartOrbDatabase.Shared;
    private readonly OracleRoomData _room;
    private readonly Func<long> _animationTick;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _toggleMask;
    private int _hitLockout;
    private bool _initialized;
    private bool _pending;
    private bool _collisionEnabled;

    public Node2D Node => this;
    public int CollisionZ => 0;
    public bool MeleeReportsContact => false;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int ToggleMask => _toggleMask;
    internal int Palette { get; private set; } = 1;
    internal int HitLockout => _hitLockout;
    internal bool PendingHit => _pending;
    internal bool IsOn =>
        (_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) &
         ToggleMask) != 0;
    internal Texture2D CurrentTexture =>
        _animation.CurrentTextureForPalette(Palette);
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(
            _orbData.RadiusX,
            _orbData.RadiusY),
        new Vector2(
            _orbData.RadiusX * 2,
            _orbData.RadiusY * 2));

    internal DungeonOrbRoomEntity(
        DungeonMechanicDatabaseRecord record,
        DungeonMechanicDatabase data,
        DungeonInteractionVisual visual,
        OracleRoomData roomData,
        OracleRuntimeState runtime,
        Func<long> animationTick,
        Action<int> playSound)
        : this(
            record.Group,
            record.Room,
            record.PackedPosition,
            1 << (record.SubId & PartOrbDatabase.Shared.SubidMask),
            data,
            visual,
            roomData,
            runtime,
            animationTick,
            playSound)
    {
        if (record.Id != InteractionId.Splash || record.SubId > 0x07)
            throw new ArgumentOutOfRangeException(nameof(record));
    }

    internal DungeonOrbRoomEntity(
        int group,
        int room,
        int packedPosition,
        int toggleMask,
        DungeonMechanicDatabase data,
        DungeonInteractionVisual visual,
        OracleRoomData roomData,
        OracleRuntimeState runtime,
        Func<long> animationTick,
        Action<int> playSound)
    {
        if (toggleMask is <= 0 or > 0x80 || (toggleMask & (toggleMask - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(toggleMask));
        _data = data;
        _room = roomData;
        _animationTick = animationTick;
        _runtime = runtime;
        _playSound = playSound;
        _toggleMask = toggleMask;
        Name = $"DungeonOrb_{group}_{room:x2}_{packedPosition:x2}";
        Position = Point(packedPosition);
        ZIndex = NpcCharacter.BehindLinkZIndex;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted,
            paletteVariants: [1, 2]);
        _animation.SetAnimation(0);
        Visible = false;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            _collisionEnabled = true;
            _pending = false;
            _hitLockout = 0;
            // objectMakeTileSolid returns ceXX. ld h,Part.zh ($cf) then
            // writes $0a to cfXX: logical layout only, preserving the floor
            // image and underlying buffer. It does not alter Part.zh.
            _room.SetPositionTileAndCollision(Position, _orbData.BackgroundTile,
                _orbData.TileCollision, _animationTick(), preserveRenderedTile: true);
            Palette = IsOn ? 2 : 1;
            Visible = true;
        }
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) { PrepareForScreenTransition(spawns); return; }
        if (_hitLockout > 0)
            _hitLockout--;
        if (!_pending) return;
        _pending = false;
        _runtime.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,
            (byte)(_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) ^ ToggleMask));
        Palette = (Palette & 1) + 1;
        _playSound(_data.SwitchSound);
        QueueRedraw();
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        return Accept(hitbox, ItemCollisionType.L1Sword);
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        return Accept(hitbox, (int)collision);
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("PART_ORB $03 requires Mystery's live collision type.");
        return new SeedSatchelDatabase().TryGet(seedItem, out var seed)
            ? ApplySeedCollision(hitbox, sourcePosition, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect : SeedHitResult.None;
    }

    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 sourcePosition,
        SeedRecord seed, int collisionType, ICollection<RoomEntitySpawn> spawns) => Accept(hitbox, collisionType)
        ? new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true) : default;

    public bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(0, hook.ZHigh, 7) || !Accept(hook.CollisionBounds, ItemCollisionType.SwitchHook)) return false;
        hook.NotifyObjectCollision(); return true;
    }

    private bool Accept(Rect2 hitbox, int collision)
    {
        int lockout = _orbData.HitLockout(collision);
        if (!_collisionEnabled || _pending || _hitLockout != 0 || lockout < 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, hitbox))
            return false;
        _pending = true;
        _hitLockout = lockout;
        return true;
    }

    public void ClearHealthAndCollision() => _collisionEnabled = false;

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible)
        {
            DrawTexture(
                CurrentTexture,
                _animation.CurrentOffset + TransitionDrawOffset);
        }
    }

    private static Vector2 Point(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
