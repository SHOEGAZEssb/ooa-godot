using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_ITEM_FROM_MAPLE $14/$15. Scattered items share the global RNG, 8.8
/// vertical bounce, original screen clamp, Maple pull states, and Link reward
/// callback.
/// </summary>
public partial class MapleDroppedItem : TransitionOffsetNode2D
{
    private static readonly int[] Speeds =
        [0x14, 0x1e, 0x28, 0x32, 0x3c, 0x46, 0x50, 0x5a];
    private static readonly int[] InitialZSpeeds =
        [-0x180, -0x1c0, -0x200, -0x240, -0x280, -0x2c0, -0x300, -0x340];

    private MapleItemRecord _record;
    private MapleEncounterState _encounter = null!;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Action<MapleItemRecord, Player> _collected = null!;
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

    internal int ItemIndex => _record.Index;
    internal int Slot { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int SpeedRaw => _speedRaw;
    internal int Angle => _angle;
    internal MapleDroppedItemState State => _state;
    internal bool Finished { get; private set; }
    internal bool CanMapleTarget =>
        !Finished && _state is MapleDroppedItemState.Bouncing
            or MapleDroppedItemState.Grounded;
    internal bool MapleCollectionReady =>
        !Finished && _state == MapleDroppedItemState.MapleReady;

    internal void Initialize(
        MapleItemRecord record,
        MapleEncounterState encounter,
        OracleRoomData room,
        OracleRandom random,
        int slot,
        Vector2 sourcePosition,
        int sourceZFixed,
        Action<MapleItemRecord, Player> collected)
    {
        _record = record;
        _encounter = encounter;
        _room = room;
        _random = random;
        _collected = collected;
        Slot = slot;
        _precisePosition = OracleObjectMath.ToPixelPosition(sourcePosition);
        Position = _precisePosition;
        _zFixed = (sourceZFixed >> 8) << 8;
        _state = MapleDroppedItemState.Initializing;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        if (definition.Frames.Length != 1)
        {
            throw new InvalidOperationException(
                $"Maple item ${record.Index:x2} should have one static frame.");
        }
        _texture = NpcCharacter.BuildOamTexture(
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
                    if (_speedZ < 0x100)
                    {
                        _speedZ = 0;
                        _state = MapleDroppedItemState.Grounded;
                    }
                    else
                    {
                        _speedZ = -_speedZ / 2;
                    }
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
                _zFixed -= 0x40;
                if ((_zFixed >> 8) < -8)
                    _state = MapleDroppedItemState.MapleReady;
                QueueRedraw();
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
        Vector2 delta = player.Position - Position;
        if (Mathf.Abs(delta.X) >= 12 || Mathf.Abs(delta.Y) >= 12)
            return false;

        CollectByLink(player);
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
        const int radius = 3;
        var bounds = new Rect2(
            Position - new Vector2(radius, radius),
            new Vector2(radius * 2, radius * 2));
        if (!hitbox.Intersects(bounds))
            return false;

        _swordCollectionPending = true;
        return true;
    }

    private void CollectByLink(Player player)
    {
        _encounter.LinkScore =
            (_encounter.LinkScore + _record.Value) & 0xff;
        Finish();
        _collected(_record, player);
    }

    internal void BeginMapleCollection(
        int vehicle,
        Func<Vector2> maplePosition)
    {
        if (!CanMapleTarget)
            return;
        _mapleVehicle = vehicle;
        _maplePosition = maplePosition;
        _speedRaw = vehicle == 1 ? 0x14 : 0x28;
        _state = MapleDroppedItemState.MaplePulling;
    }

    internal void CompleteMapleCollection()
    {
        if (!Finished)
            Finish();
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
        if (current != target)
        {
            int angle = OracleObjectMath.AngleToward(current, target);
            _precisePosition +=
                OracleObjectMath.VectorFromAngle32(angle) *
                (_speedRaw / 40.0f);
            Vector2 next = OracleObjectMath.ToPixelPosition(_precisePosition);
            if (Mathf.Abs(target.X - next.X) <= 1 &&
                Mathf.Abs(target.Y - next.Y) <= 1)
            {
                _precisePosition = target;
            }
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            QueueRedraw();
            return;
        }

        _state = MapleDroppedItemState.MapleRising;
        _zFixed = 0;
        QueueRedraw();
    }

    private void ApplyHorizontalSpeed()
    {
        _precisePosition += OracleObjectMath.VectorFromAngle32(_angle) *
            (_speedRaw / 40.0f);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private void ClampPosition()
    {
        _precisePosition = new Vector2(
            Mathf.Clamp(_precisePosition.X, 8, 152.999f),
            Mathf.Clamp(_precisePosition.Y, 32, 120.999f));
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
    MapleReady
}
