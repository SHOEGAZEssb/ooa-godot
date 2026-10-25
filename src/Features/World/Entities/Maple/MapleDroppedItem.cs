using Godot;
using System;

namespace Oracles;

/// <summary>
/// PART_ITEM_FROM_MAPLE $14/$15. Scattered items share the global RNG, 8.8
/// vertical bounce, original screen clamp, Maple pull states, and Link reward
/// callback.
/// </summary>
public partial class MapleDroppedItem : TransitionOffsetNode2D, ITerrainShadowSource
{
    // itemFromMaple.s state0 selects visiblec3. Maple's state4 pull/rise
    // retains bit 6 through collection; this is separate from PART_ITEM_DROP.
    int? ITerrainShadowSource.TerrainShadowZHigh =>
        _state == MapleDroppedItemState.Initializing || _hasCarrierSubid || Finished ? null : _zFixed >> 8;

    private static readonly int[] Speeds =
        [0x14, 0x1e, 0x28, 0x32, 0x3c, 0x46, 0x50, 0x5a];
    private static readonly int[] InitialZSpeeds =
        [-0x180, -0x1c0, -0x200, -0x240, -0x280, -0x2c0, -0x300, -0x340];

    private MapleItemRecord _record;
    private MapleEncounterState _encounter = null!;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private OracleRuntimeState? _movementMemory;
    private Func<MapleItemRecord, Player, bool> _collected = null!;
    private Texture2D _texture = null!;
    private Vector2 _precisePosition;
    private MapleDroppedItemState _state;
    private int _zFixed;
    private int _speedZ;
    private int _speedRaw;
    private int _angle;
    private int _mapleVehicle;
    private Func<Vector2>? _maplePosition;
    private bool _swordCollectionPending;
    private bool _mapleRetirementRequested;
    private Func<ItemDropCarrier?>? _carrier;
    private bool _attachmentPending;
    private int? _carrierId;
    private bool _hasCarrierSubid;

    internal int ItemIndex => _record.Index;
    internal int Slot { get; private set; }
    internal void SetPartSlot(int slot) => Slot = slot;
    internal int ZFixed => _zFixed;
    internal MapleDroppedItemState State => _state;
    internal bool Finished { get; private set; }
    internal bool CanMapleTarget =>
        !Finished && !_hasCarrierSubid;
    internal Rect2 CollisionBounds => new(Position - new Vector2(6, 6), new Vector2(12, 12));
    internal bool CanAttachToItem => !Finished && !_swordCollectionPending && !_attachmentPending && !_mapleRetirementRequested &&
        _state is MapleDroppedItemState.Grounded or MapleDroppedItemState.MaplePulling or MapleDroppedItemState.Attached;
    internal void AttachToItem(Func<ItemDropCarrier?> carrier) { _carrier = carrier; _attachmentPending = true; }
    internal bool MapleCollectionReady =>
        !Finished && _state == MapleDroppedItemState.MapleReady;
    internal bool AttachedToItem => _hasCarrierSubid;

    internal void Initialize(
        MapleItemRecord record,
        MapleEncounterState encounter,
        OracleRoomData room,
        OracleRandom random,
        int slot,
        Vector2 sourcePosition,
        int sourceZFixed,
        Func<MapleItemRecord, Player, bool> collected,
        OracleRuntimeState? movementMemory = null)
    {
        _record = record;
        _encounter = encounter;
        _room = room;
        _random = random;
        _movementMemory = movementMemory;
        _collected = collected;
        Slot = slot;
        _precisePosition = OracleObjectMath.ToPixelPosition(sourcePosition);
        Position = _precisePosition;
        _zFixed = (sourceZFixed >> 8) << 8;
        _state = MapleDroppedItemState.Initializing;
        Image source = OracleGraphicsCache.LoadImage(
            AgesGameContent.AssetRoot + $"gfx/{record.Sprite}.png");
        AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        if (definition.Frames.Length != 1)
        {
            throw new InvalidOperationException(
                $"Maple item ${record.Index:x2} should have one static frame.");
        }
        _texture = OracleOamCompositor.BuildOamTexture(
            source,
            definition.Frames[0].EncodedOam,
            record.TileBase,
            record.Palette);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;

        if (_swordCollectionPending)
        {
            CollectByLink(player);
            return;
        }
        if (_attachmentPending)
        {
            // Unlike PART_ITEM_DROP, each JUST_HIT visit explicitly clears
            // substate, so repeated effect$24 contacts recache the live slot ID.
            _attachmentPending = false; _state = MapleDroppedItemState.Attached; _carrierId = null; _hasCarrierSubid = true;
        }
        switch (_state)
        {
            case MapleDroppedItemState.Initializing:
            {
                byte first = _random.Next().Value;
                _speedRaw = Speeds[(first & 0x70) >> 4];
                _speedZ = InitialZSpeeds[(first & 0x0e) >> 1];
                _angle = _random.Next().Value & 0x1f;
                _state = MapleDroppedItemState.Bouncing;
                Visible = true;
                QueueRedraw();
                return;
            }

            case MapleDroppedItemState.Bouncing:
                ApplyHorizontalSpeed();
                ClampPosition();
                if (OracleObjectMath.UpdateSpeedZ(
                        ref _zFixed, ref _speedZ, 0x20))
                {
                    if (OracleObjectMath.NegateAndHalveSpeedZ(ref _speedZ))
                        _state = MapleDroppedItemState.Grounded;
                }
                if (_zFixed == 0 &&
                    _room.GetTerrainInfo(
                        Position + new Vector2(0, 5)).Hazard != HazardType.None)
                {
                    Finish();
                }
                QueueRedraw();
                return;

            case MapleDroppedItemState.MaplePulling:
                UpdateMaplePull();
                return;

            case MapleDroppedItemState.MapleRising:
                OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0);
                if (((_zFixed >> 8) & 0xff) < 0xf7)
                    _state = MapleDroppedItemState.MapleReady;
                QueueRedraw();
                return;
            case MapleDroppedItemState.MapleReady:
                // The companion writes var03 bit7 before Link/ITEM/ENEMY.
                // Only this later PART visit performs partDelete, retaining
                // the physical slot through intermediate checked producers.
                if (_mapleRetirementRequested) Finish();
                return;
            case MapleDroppedItemState.Attached:
                UpdateAttachment(player);
                return;
        }
    }

    internal bool TryCollectByLink(Player player)
    {
        if (Finished ||
            _encounter.ObjectsDisabled ||
            _state != MapleDroppedItemState.Grounded)
        {
            return false;
        }
        if (!Player.EnemyCollisionOverlaps(player.ActiveLinkObjectPosition, CollisionBounds))
            return false;

        // collisionEffect23 publishes health0 in the post-object scan. The
        // next eligible PART visit enters @linkCollectedItem, even if Link
        // has moved away or a temporary control mask delayed the reward.
        _swordCollectionPending = true;
        return true;
    }

    internal bool TryCollectWithSword(Rect2 hitbox)
    {
        if (Finished || _swordCollectionPending ||
            _encounter.ObjectsDisabled ||
            _state != MapleDroppedItemState.Grounded)
        {
            return false;
        }
        if (!hitbox.Intersects(CollisionBounds))
            return false;

        _swordCollectionPending = true;
        return true;
    }

    private void CollectByLink(Player player)
    {
        if (_encounter.ObjectsDisabled) return; // @linkCollectedItem retains status while bit0 is set.
        _encounter.LinkScore =
            (_encounter.LinkScore + _record.Value) & 0xff;
        // Heart Piece createTreasure is checked. Its source score addition
        // precedes admission and repeats on failure; retain this PART to retry.
        if (_collected(_record, player)) Finish();
    }

    private void UpdateAttachment(Player player)
    {
        ItemDropCarrier? carrier = _carrier!();
        if (_carrierId is null)
        {
            _carrierId = carrier?.ItemId ?? -1;
            _zFixed &= 0xff;
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex; Visible = true;
        }
        // itemFromMaple state3 checks Link before carrier liveness/ID, using
        // objectCheckCollidedWithLink's Z-aware check (unlike PART_ITEM_DROP).
        if (RoomEntityManager.ObjectCollisionZOverlaps(unchecked((sbyte)player.ObjectZHigh), _zFixed >> 8, 7) &&
            Player.EnemyCollisionOverlaps(player.ActiveLinkObjectPosition, CollisionBounds))
            CollectByLink(player);
        else if (carrier is { } live && live.ItemId == _carrierId)
        {
            _precisePosition = new Vector2(unchecked((byte)(int)live.Position.X), unchecked((byte)(int)live.Position.Y)) +
                (_precisePosition - _precisePosition.Floor());
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            _zFixed = (unchecked((sbyte)(byte)live.ZHigh) << 8) | (_zFixed & 0xff);
        }
        else Finish();
        QueueRedraw();
    }

    internal void BeginMapleCollection(
        int vehicle,
        Func<Vector2> maplePosition)
    {
        if (Finished)
            return;
        _mapleVehicle = vehicle;
        _maplePosition = maplePosition;
        _speedRaw = vehicle == 1 ? ObjectSpeed.Speed80 : ObjectSpeed.Speed100;
        _state = MapleDroppedItemState.MaplePulling;
    }

    internal void CompleteMapleCollection()
    {
        if (!Finished)
            _mapleRetirementRequested = true;
    }

    internal void ReleaseFromMaple()
    {
        if (Finished ||
            _state is not
                (MapleDroppedItemState.MaplePulling or
                 MapleDroppedItemState.MapleRising or
                 MapleDroppedItemState.MapleReady))
        {
            return;
        }
        _maplePosition = null;
        // mapleState7 writes only state1 and angle$80 at bomb stun. Preserve
        // current Z, speedZ and planar speed for the later shared bounce.
        _angle = 0x80;
        _state = MapleDroppedItemState.Bouncing;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                new Vector2(-16, -16 + (_zFixed >> 8)) +
                TransitionDrawOffset);
        }
    }

    private void UpdateMaplePull()
    {
        if (_maplePosition is null)
        {
            Finish();
            return;
        }
        if (_mapleVehicle == 0)
        {
            _state = MapleDroppedItemState.MapleReady;
            return;
        }

        Vector2 target = OracleObjectMath.ToPixelPosition(_maplePosition());
        Vector2 current = OracleObjectMath.ToPixelPosition(_precisePosition);
        // moveToMaple always applies speed, even at equal high coordinates,
        // and tests exact high-byte arrival after that movement. No snap.
        _angle = OracleObjectMovement.Shared.RelativeAngle(current, target);
        Position = NativeObjectMovement.ApplySpeed(_movementMemory,
            ref _precisePosition, _speedRaw, _angle);
        if (Position == target)
        {
            _state = MapleDroppedItemState.MapleRising;
            _speedZ = -0x40; // Arrival retains the item's current Z.
        }
        QueueRedraw();
    }

    private void ApplyHorizontalSpeed()
    {
        Position = NativeObjectMovement.ApplySpeed(_movementMemory,
            ref _precisePosition, _speedRaw, _angle);
    }

    private void ClampPosition()
    {
        _precisePosition = MapleScreenBounds.Clamp(_precisePosition);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
        QueueRedraw();
    }
}

internal enum MapleDroppedItemState
{
    Initializing,
    Bouncing,
    Grounded,
    MaplePulling,
    MapleRising,
    MapleReady,
    Attached
}
