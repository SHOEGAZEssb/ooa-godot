using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_ORB $03:$04 created by room 4:56's event.</summary>
internal sealed partial class MoonlitGrottoOrbRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, ISwordHittableRoomEntity,
    IItemCollisionHittableRoomEntity, ISeedHittableRoomEntity,
    IObjectCollisionHeightRoomEntity
{
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly EnemyAnimationPlayer _animation;
    private int _hitLockout;

    public Node2D Node => this;
    public int CollisionZ => 0;
    internal int ToggleMask => _data.MoonlitOrbMask;
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

    internal MoonlitGrottoOrbRoomEntity(
        int group,
        int room,
        DungeonMechanicDatabase data,
        DungeonInteractionVisual visual,
        OracleRoomData roomData,
        OracleRuntimeState runtime,
        Func<long> animationTick,
        Action<int> playSound)
    {
        _data = data;
        _runtime = runtime;
        _playSound = playSound;
        Name = $"GrottoOrb_{group}_{room:x2}";
        Position = Point(data.MoonlitOrbPosition);
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
        // ENEMYCOLLISION_SWITCH applies ENEMYDMG_34. Its signed $e4
        // invincibility counter increments through zero over 28 updates,
        // preventing one multi-frame bomb explosion from hitting repeatedly.
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
        Toggle(hitbox) ? SeedHitResult.Consume : SeedHitResult.None;

    private bool Toggle(Rect2 hitbox)
    {
        if (_hitLockout != 0 || !hitbox.Intersects(CollisionBounds))
            return false;
        byte state = _runtime.ReadWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress);
        _runtime.SetWramByte(
            OracleRuntimeState.ToggleBlocksStateAddress,
            (byte)(state ^ ToggleMask));
        _hitLockout = _data.SwitchHitLockout;
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
