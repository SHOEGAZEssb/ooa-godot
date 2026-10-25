using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// PART_SMALL_FAIRY $28 has no drop lifetime and uses its own bounded flight.
internal sealed partial class SmallFairyRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IPostObjectLinkContactRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, ISwordHittableRoomEntity, ILinkSwordStateAwareRoomEntity,
    IBoomerangCollisionRoomEntity, ISwitchHookHittableRoomEntity, IAttachableCollectibleRoomEntity,
    IObjectCollisionHeightRoomEntity, INativePartHealthRoomEntity, ITerrainShadowSource,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly PlacedPartDatabase _data;
    private Vector2 _precisePosition;
    private bool _pendingCollection, _attachmentPending;
    private Func<ItemDropCarrier?>? _carrier;
    private int? _carrierId;
    private int _swordCollision = ItemCollisionType.L1Sword;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Speed { get; private set; }
    internal int Angle { get; private set; }
    internal int CurrentAnimation => AnimationIndex;
    internal Vector2 PrecisePosition => _precisePosition;
    internal int Health => _pendingCollection ? 0 : _data.Definition(PartId.SmallFairy).Health;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public int CollisionZ { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public bool MeleeReportsContact => false; // collisionEffect23 does not write Item.var2a.
    int? ITerrainShadowSource.TerrainShadowZHigh => State == 2 ? null : CollisionZ;
    private bool CollisionEnabled => State != 0 && !Finished && !_pendingCollection && !_attachmentPending;
    private Rect2 Bounds
    {
        get
        {
            var definition = _data.Definition(PartId.SmallFairy);
            var radius = new Vector2(definition.RadiusX, definition.RadiusY);
            return new(Position - radius, radius * 2);
        }
    }

    internal SmallFairyRoomEntity(RoomEntityManager owner, PlacedPartDatabase data, Vector2 position)
    {
        _owner = owner; _data = data; _precisePosition = position.Floor();
        InitializeVisual(data.Definition(PartId.SmallFairy).Visual, position.Floor(), positionedOam: true);
        Visible = false; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    private void Initialize() { State = 1; CollisionZ = -6; Visible = true; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { Initialize(); return; }
        if (_pendingCollection) { Collect(frame.Player); return; }
        if (_attachmentPending) { _attachmentPending = false; State = 2; }
        if (State == 2)
        {
            ItemDropCarrier? carrier = _carrier!();
            if (_carrierId is null)
            {
                _carrierId = carrier?.ItemId ?? -1; CollisionZ = 0;
                ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex;
            }
            // Unlike ITEM_DROP state3, smallFairy.s uses the Z-aware check.
            if (frame.Player.EnemyContactHeightOverlaps(CollisionZ) &&
                Player.EnemyCollisionOverlaps(frame.Player.ActiveLinkObjectPosition, Bounds)) Collect(frame.Player);
            else if (carrier is { } live && live.ItemId == _carrierId)
            {
                _precisePosition = new(unchecked((byte)(int)live.Position.X) + _precisePosition.X % 1,
                    unchecked((byte)(int)live.Position.Y) + _precisePosition.Y % 1);
                Position = _precisePosition.Floor(); CollisionZ = live.ZHigh;
            }
            else { Finished = true; Visible = false; }
            QueueRedraw(); return;
        }
        if (Counter != 0) Counter--;
        if (Counter != 0 && ProbeInsideFlightBounds())
            Position = NativeObjectMovement.ApplySpeed(_owner.RuntimeState, ref _precisePosition, Speed, Angle);
        else
        {
            Counter = (_owner.Random.Next().Value & 0x3e) + 8;
            Speed = _data.Table("fairy-speeds")[_owner.Random.Next().Value & 3];
            Angle = _owner.Random.Next().Value & 0x1e;
            if ((Angle & 0x0f) != 0)
            {
                int animation = Angle < 0x10 ? 1 : 0;
                if (animation != AnimationIndex) SetAnimation(animation);
            }
        }
        QueueRedraw();
    }
    private bool ProbeInsideFlightBounds()
    {
        int rounded = (Angle & 7) == 0 ? Angle : (Angle & 0x18) + 4;
        var offsets = _data.Table("fairy-offsets");
        int x = unchecked((byte)((int)Position.X + (sbyte)offsets[rounded / 2 + 1]));
        int y = unchecked((byte)((int)Position.Y + (sbyte)offsets[rounded / 2]));
        return unchecked((byte)(x - 0x38)) < 0x80 && unchecked((byte)(y - 0x18)) < 0x50;
    }
    private void Collect(Player player)
    {
        // @collected calls giveTreasure even at full health, including its
        // obtained flag and Gasha maturity writes. No drop pickup cue is played.
        player.Inventory.GiveTreasure(TreasureId.HeartRefill,
            24 * RingEffects.DropMultiplier(player.Inventory, RingDropKind.Heart));
        Finished = true; Visible = false;
    }
    public void ClearHealthAndCollision() => _pendingCollection = true;
    public void HandleLinkContact(Player player)
    {
        if (CollisionEnabled && player.NativeObjectVulnerable && player.OverlapsEnemyCollision(Bounds, CollisionZ))
            _pendingCollection = true;
    }
    public void SetLinkSwordState(SwordActionState state, int level, int? collision = null) =>
        _swordCollision = collision ?? SwordCollision.Type(state, level);
    public bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Overlaps(_swordCollision, bounds)) return false;
        if (_data.Effect(PartId.SmallFairy, _swordCollision) != 0x23)
            throw new NotSupportedException($"smallFairy.s: unsupported sword effect for item${_swordCollision:x2}.");
        _pendingCollection = true; return true;
    }
    private bool Overlaps(int collision, Rect2 bounds) => CollisionEnabled && _data.Active(PartId.SmallFairy, collision) &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Bounds, bounds);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns) =>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) &&
        Overlaps(ItemCollisionType.L1Boomerang, item.CollisionBounds) ? new(true, true) : default;
    public void AttachToItem(Func<ItemDropCarrier?> carrier) { _carrier = carrier; _attachmentPending = true; }
    public bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!hook.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, hook.ZHigh, 7) ||
            !Overlaps(ItemCollisionType.SwitchHook, hook.CollisionBounds)) return false;
        AttachToItem(() => _owner.SwitchHook?.Item is { Finished: false } live
            ? new ItemDropCarrier(ItemId.SwitchHook, live.Position, live.ZHigh) : null);
        hook.NotifyObjectCollision(); return true;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) Initialize();
        return ScreenTransitionPresentation.Visible;
    }
    public override void _Draw()
    {
        if (Visible) DrawTexture(CurrentTexture, CurrentDrawOffset + new Vector2(0, CollisionZ) + TransitionDrawOffset);
    }
}
