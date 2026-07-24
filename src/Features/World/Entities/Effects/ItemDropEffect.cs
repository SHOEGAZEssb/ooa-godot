using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_ITEM_DROP ($01). Drops use vertical 8.8 fixed-point motion; shovel
/// drops additionally copy Link's angle and SPEED_a0 until they finish
/// bouncing. Grounded drops wait for 240 alternating-frame countdown ticks.
/// </summary>
public partial class ItemDropEffect : TransitionOffsetNode2D
{

    private const int InitialSpeedZ = -0x160;
    private const int DugUpSpeed = 0x19;
    private const int Gravity = 0x20;
    private const int StopBounceSpeed = 0x100;
    private const int LifetimeTicks = 240;
    private const int FlickerTicks = 60;
    private const int CombinedCollisionRadius = 10;
    private const int CollisionRadius = 4;
    private const int ZCollisionRadius = 7;
    private const int SwordZ = -2;

    private OracleRoomData _room = null!;
    private Texture2D _texture = null!;
    private DropState _state;
    private int _zFixed;
    private int _speedZ;
    private int _counter;
    private bool _collisionEnabled;
    private Vector2 _precisePosition;
    private int _angle;
    private int _speed;
    private Action<int> _soundRequested = static _ => { };
    private int _collectionSound;
    private bool _swordCollectionPending;

    public int SubId { get; private set; }
    public bool Finished { get; private set; }
    public bool Collected { get; private set; }
    internal HazardType FinishedHazard { get; private set; }
    internal DropState State => _state;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int Counter => _counter;
    internal bool CollisionEnabled => _collisionEnabled;
    internal int ElapsedFrames { get; private set; }
    internal Vector2 PrecisePosition => _precisePosition;
    internal int Angle => _angle;
    internal int Speed => _speed;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(CollisionRadius, CollisionRadius),
        new Vector2(CollisionRadius * 2, CollisionRadius * 2));

    internal void Initialize(
        int subId,
        Vector2 position,
        OracleRoomData room,
        ItemDropDatabaseVisualRecord visual,
        int angle = 0,
        bool dugUp = false,
        Action<int>? soundRequested = null,
        int collectionSound = 0)
    {
        if (angle is < 0 or >= 0x20)
            throw new ArgumentOutOfRangeException(nameof(angle));
        SubId = subId;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        _room = room;
        _angle = angle;
        _speed = dugUp && subId != 0 ? DugUpSpeed : 0;
        _soundRequested = soundRequested ?? (static _ => { });
        _collectionSound = collectionSound;
        _speedZ = InitialSpeedZ;
        _state = DropState.Initializing;
        _texture = BuildTexture(visual);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player, int globalFrameCounter)
    {
        if (Finished)
            return;

        ElapsedFrames++;
        if (_swordCollectionPending)
        {
            Collect(player);
            return;
        }
        if (_state == DropState.Initializing)
        {
            _state = DropState.Bouncing;
            return;
        }

        if (_collisionEnabled && OverlapsLink(player.Position))
        {
            Collect(player);
            return;
        }

        if (_state == DropState.Bouncing)
        {
            UpdateBounce();
            // objectCheckIsOnHazard ignores an object while zh is negative.
            // On the first ground-height update, the hazard replaces the drop
            // instead of allowing another bounce.
            if (_zFixed == 0)
            {
                HazardType hazard = _room.GetTerrainInfo(
                    Position + new Vector2(0, 5)).Hazard;
                if (hazard != HazardType.None)
                {
                    FinishedHazard = hazard;
                    FinishWithoutCollection();
                }
            }
            QueueRedraw();
            return;
        }

        // itemDrop_countdownToDisappear decrements only when
        // (wFrameCounter XOR the object's slot page) is odd. The managed
        // object has no WRAM page, so use the odd global-frame phase.
        if ((globalFrameCounter & 1) == 0)
            return;

        _counter--;
        if (_counter <= 0)
        {
            FinishWithoutCollection();
            return;
        }
        if (_counter < FlickerTicks)
            Visible = !Visible;
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        int zPixel = _zFixed >> 8;
        DrawTexture(_texture, new Vector2(-16, -16 + zPixel) + TransitionDrawOffset);
    }

    private void UpdateBounce()
    {
        UpdateHorizontalSpeed();
        if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, Gravity))
        {
            if (_speedZ >= 0)
                _collisionEnabled = true;
            return;
        }

        if (_speedZ < StopBounceSpeed)
        {
            _speedZ = 0;
            _state = DropState.Grounded;
            _counter = LifetimeTicks;
            _collisionEnabled = true;
            return;
        }

        _speedZ = -_speedZ / 2;
    }

    private void UpdateHorizontalSpeed()
    {
        if (_speed == 0)
            return;

        Vector2 direction = OracleObjectMath.StrictCardinalVector(_angle);
        // partCommon_anglePositionOffsets probes five pixels toward up/left
        // and four toward right/down before objectCheckTileCollision_allowHoles
        // checks the current high-byte position.
        Vector2 frontOffset = new(
            direction.X < 0 ? -5 : direction.X > 0 ? 4 : 0,
            direction.Y < 0 ? -5 : direction.Y > 0 ? 4 : 0);
        Vector2 front = OracleObjectMath.ToPixelPosition(_precisePosition) + frontOffset;
        Vector2 current = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (IsBlocked(front) || IsBlocked(current))
        {
            return;
        }

        _precisePosition += direction * (_speed / 40.0f);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private bool IsBlocked(Vector2 point) =>
        point.X < 0 || point.X >= _room.Width ||
        point.Y < 0 || point.Y >= _room.Height ||
        _room.IsSolid(point);

    private bool OverlapsLink(Vector2 linkPosition)
    {
        int zPixel = _zFixed >> 8;
        return Mathf.Abs(zPixel) < ZCollisionRadius &&
            Mathf.Abs(linkPosition.X - Position.X) < CombinedCollisionRadius &&
            Mathf.Abs(linkPosition.Y - Position.Y) < CombinedCollisionRadius;
    }

    internal bool TryCollectWithSword(Rect2 hitbox)
    {
        if (Finished || _swordCollectionPending || !_collisionEnabled ||
            !RoomEntityManager.ObjectCollisionZOverlaps(
                _zFixed >> 8, SwordZ, ZCollisionRadius) ||
            !hitbox.Intersects(CollisionBounds))
        {
            return false;
        }

        // ENEMYCOLLISION_ITEM + sword collision types $04-$0b selects
        // COLLISIONEFFECT_23. The original part observes zero health and
        // grants the treasure at the start of its next update.
        _swordCollectionPending = true;
        return true;
    }

    private void Collect(Player player)
    {
        switch (SubId)
        {
            case ItemDropDatabase.Heart:
                player.Heal(4 * RingEffects.DropMultiplier(
                    player.Inventory, RingDropKind.Heart));
                break;
            case ItemDropDatabase.OneRupee:
                player.AddRupees(RingEffects.DropMultiplier(
                    player.Inventory, RingDropKind.Rupee));
                break;
            case ItemDropDatabase.FiveRupees:
                player.AddRupees(5 * RingEffects.DropMultiplier(
                    player.Inventory, RingDropKind.Rupee));
                break;
            case ItemDropDatabase.OneHundredRupeesOrEnemy:
                player.AddRupees(100 * RingEffects.DropMultiplier(
                    player.Inventory, RingDropKind.Rupee));
                break;
            case ItemDropDatabase.Bombs:
                player.Inventory.GiveTreasure(
                    TreasureDatabase.TreasureBombs,
                    RingEffects.DropMultiplier(player.Inventory, RingDropKind.Other) == 2
                        ? 0x08
                        : 0x04);
                break;
            case >= ItemDropDatabase.EmberSeeds and <= ItemDropDatabase.MysterySeeds:
                player.Inventory.GiveTreasure(
                    TreasureDatabase.TreasureEmberSeeds + SubId - ItemDropDatabase.EmberSeeds,
                    RingEffects.DropMultiplier(player.Inventory, RingDropKind.Other) == 2
                        ? 0x0a
                        : 0x05);
                break;
            default:
                return;
        }

        if (_collectionSound != 0)
            _soundRequested(_collectionSound);
        Collected = true;
        Finished = true;
        Visible = false;
    }

    private void FinishWithoutCollection()
    {
        Finished = true;
        Visible = false;
    }

    private static Texture2D BuildTexture(ItemDropDatabaseVisualRecord visual)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_items.png");

        AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames;
        if (frames.Length == 0)
            throw new InvalidOperationException(
                $"PART_ITEM_DROP ${visual.SubId:x2} has malformed animation data.");
        return NpcCharacter.BuildOamTexture(
            source, frames[0].EncodedOam, visual.TileBase, visual.Palette);
    }
}

internal enum DropState
{
    Initializing,
    Bouncing,
    Grounded
}
