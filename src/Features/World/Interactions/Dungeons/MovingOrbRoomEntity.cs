using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Common PART_MOVING_ORB $0b: script movement and delayed hit toggle.</summary>
internal sealed partial class MovingOrbRoomEntity : TransitionOffsetNode2D, IRoomEntity, IFixedRoomEntity,
    ISeedCollisionTarget, IObjectCollisionHeightRoomEntity, ISwordHittableRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, ISwitchHookHittableRoomEntity, IPostObjectItemCollisionRoomEntity,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    INativePartHealthRoomEntity
{
    private readonly PartOrbDatabase _data = PartOrbDatabase.Shared;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _sound;
    private readonly int _mask;
    private readonly int _switchSound;
    private readonly EnemyAnimationPlayer _animation;
    private bool _pending;
    private bool _collisionEnabled = true;
    public Node2D Node => this;
    public int CollisionZ => 0;
    public bool MeleeReportsContact => false;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal int State { get; private set; }
    internal int Palette { get; private set; }
    internal int HitLockout { get; private set; }
    internal bool PendingHit => _pending;
    internal Rect2 CollisionBounds => new(Position - Vector2.One * 4, Vector2.One * 8);
    internal MovingOrbRoomEntity(DungeonObjectRecord record, DungeonInteractionVisual visual,
        OracleRuntimeState runtime, Action<int> sound, int switchSound)
    {
        if (record.Id != InteractionId.Id0b || record.SubId != 0 || record.Var03 == 0)
            throw new InvalidOperationException($"Unsupported moving orb at {record.Source}.");
        Position = record.Position; _mask = record.Var03; _runtime = runtime; _sound = sound; _switchSound = switchSound;
        Name = "MovingOrb"; ZIndex = NpcCharacter.BehindLinkZIndex;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations,
            visual.TileBase, visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted, paletteVariants: [1, 2]);
        _animation.SetAnimation(0); Visible = false;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0)
        {
            State = 9;
            Palette = (_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) & _mask) == 0 ? 1 : 2;
            _collisionEnabled = true; _pending = false; Visible = true;
        }
        return ScreenTransitionPresentation.Visible;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { PrepareForScreenTransition(spawns); return; }
        if (HitLockout > 0) HitLockout--;
        if (_pending)
        {
            _pending = false;
            _runtime.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,
                (byte)(_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) ^ _mask));
            Palette = Palette == 1 ? 2 : 1; _sound(_switchSound);
        }
        int target = State == 9 ? _data.Right : _data.Left;
        int high = OracleObjectPosition.HighByte(Position.X);
        if (State == 9 ? high < target : high > target)
        {
            var velocity = OracleObjectMovement.Shared.Velocity(_data.Speed, State == 9 ? ObjectAngle.Right : ObjectAngle.Left);
            Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
        }
        else
        {
            Position = new Vector2(target + Position.X - Mathf.Floor(Position.X), Position.Y);
            State = State == 9 ? 11 : 9;
        }
        QueueRedraw();
    }
    private bool Accept(Rect2 hitbox, int collision)
    {
        int lockout = _data.HitLockout(collision);
        if (!_collisionEnabled || _pending || HitLockout != 0 || lockout < 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, hitbox)) return false;
        HitLockout = lockout; _pending = true; return true;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns) => Accept(hitbox, collisionType)
        ? new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true) : default;
    public SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("PART_MOVING_ORB $0b requires Mystery's live collision type.");
        return new SeedSatchelDatabase().TryGet(seedItem, out var seed)
            ? ApplySeedCollision(hitbox, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect : SeedHitResult.None;
    }
    public bool ApplySwordHit(Rect2 hitbox, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => Accept(hitbox, ItemCollisionType.L1Sword);
    public bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 origin)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(0, hook.ZHigh, 7) || !Accept(hook.CollisionBounds, ItemCollisionType.SwitchHook)) return false;
        hook.NotifyObjectCollision(); return true;
    }
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox, Vector2 source, int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        return Accept(hitbox, (int)collision);
    }
    public void ClearHealthAndCollision() => _collisionEnabled = false;
    public override void _Draw()
    {
        if (Visible) DrawTexture(_animation.CurrentTextureForPalette(Palette), _animation.CurrentOffset + SourceOamDrawOffset);
    }
    public new void SetTransitionDrawOffset(Vector2 offset) => base.SetTransitionDrawOffset(offset);
}
