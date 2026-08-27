using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Common PART_ORB $03 placed directly or spawned by an event.</summary>
internal sealed partial class DungeonOrbRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, ISwordHittableRoomEntity,
    IItemCollisionHittableRoomEntity, ISeedHittableRoomEntity,
    IObjectCollisionHeightRoomEntity, ISeedPreMovementCollisionTarget
{
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _toggleMask;
    private int _hitLockout;

    public Node2D Node => this;
    public int CollisionZ => 0;
    internal int ToggleMask => _toggleMask;
    internal int Palette => IsOn ? 2 : 1;
    internal int HitLockout => _hitLockout;
    internal bool IsOn =>
        (_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) &
         ToggleMask) != 0;
    internal Texture2D CurrentTexture =>
        _animation.CurrentTextureForPalette(Palette);
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(
            _data.MoonlitOrbRadiusX,
            _data.MoonlitOrbRadiusY),
        new Vector2(
            _data.MoonlitOrbRadiusX * 2,
            _data.MoonlitOrbRadiusY * 2));

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
            1 << (record.SubId & 0x07),
            data,
            visual,
            roomData,
            runtime,
            animationTick,
            playSound)
    {
        if (record.Id != 0x03 || record.SubId > 0x07)
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

        roomData.SetPositionTileAndCollision(
            Position,
            roomData.GetMetatile(Position),
            (byte)data.MoonlitOrbCollision,
            animationTick(),
            preserveRenderedTile: true);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_hitLockout > 0)
            _hitLockout--;
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        Toggle(hitbox);
        return false;
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (collision is RoomEntityItemCollision.ThrownObject or
            RoomEntityItemCollision.Bomb or
            RoomEntityItemCollision.SwordBeam)
        {
            Toggle(hitbox);
        }
        return false;
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns) =>
        Toggle(hitbox, applyHitLockout: false)
            ? SeedHitResult.Activate
            : SeedHitResult.None;

    private bool Toggle(Rect2 hitbox, bool applyHitLockout = true)
    {
        if (_hitLockout != 0 || !hitbox.Intersects(CollisionBounds))
            return false;
        byte state = _runtime.ReadWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress);
        _runtime.SetWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress,
            (byte)(state ^ ToggleMask));
        _hitLockout = applyHitLockout ? _data.SwitchHitLockout : 0;
        _playSound(_data.SwitchSound);
        QueueRedraw();
        return true;
    }

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
