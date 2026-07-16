using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_ITEM_DROP ($01). Enemy drops use vertical 8.8 fixed-point motion,
/// then wait for 240 alternating-frame countdown ticks before disappearing.
/// </summary>
public partial class ItemDropEffect : Node2D
{
    internal enum DropState { Initializing, Bouncing, Grounded }

    private const int InitialSpeedZ = -0x160;
    private const int Gravity = 0x20;
    private const int StopBounceSpeed = 0x100;
    private const int LifetimeTicks = 240;
    private const int FlickerTicks = 60;
    private const int CombinedCollisionRadius = 10;
    private const int ZCollisionRadius = 7;

    private OracleRoomData _room = null!;
    private Texture2D _texture = null!;
    private DropState _state;
    private int _zFixed;
    private int _speedZ;
    private int _counter;
    private bool _collisionEnabled;
    private Vector2 _transitionDrawOffset;

    public int SubId { get; private set; }
    public bool Finished { get; private set; }
    public bool Collected { get; private set; }
    internal DropState State => _state;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int Counter => _counter;
    internal bool CollisionEnabled => _collisionEnabled;
    internal int ElapsedFrames { get; private set; }
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;

    internal void Initialize(
        int subId,
        Vector2 position,
        OracleRoomData room,
        ItemDropDatabase.VisualRecord visual)
    {
        SubId = subId;
        Position = position;
        _room = room;
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
            if (_room.GetTerrainInfo(Position).Hazard != OracleRoomData.HazardType.None)
                FinishWithoutCollection();
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

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (_transitionDrawOffset.IsEqualApprox(offset))
            return;
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        int zPixel = _zFixed >> 8;
        DrawTexture(_texture, new Vector2(-16, -16 + zPixel) + _transitionDrawOffset);
    }

    private void UpdateBounce()
    {
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

    private bool OverlapsLink(Vector2 linkPosition)
    {
        int zPixel = _zFixed >> 8;
        return Mathf.Abs(zPixel) < ZCollisionRadius &&
            Mathf.Abs(linkPosition.X - Position.X) < CombinedCollisionRadius &&
            Mathf.Abs(linkPosition.Y - Position.Y) < CombinedCollisionRadius;
    }

    private void Collect(Player player)
    {
        switch (SubId)
        {
            case ItemDropDatabase.Heart:
                player.Heal(4);
                break;
            case ItemDropDatabase.OneRupee:
                player.AddRupees(1);
                break;
            case ItemDropDatabase.FiveRupees:
                player.AddRupees(5);
                break;
            default:
                return;
        }

        Collected = true;
        Finished = true;
        Visible = false;
    }

    private void FinishWithoutCollection()
    {
        Finished = true;
        Visible = false;
    }

    private static Texture2D BuildTexture(ItemDropDatabase.VisualRecord visual)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_items.png");

        OracleGraphicsCache.AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames;
        if (frames.Length == 0)
            throw new InvalidOperationException(
                $"PART_ITEM_DROP ${visual.SubId:x2} has malformed animation data.");
        return NpcCharacter.BuildOamTexture(
            source, frames[0].EncodedOam, visual.TileBase, visual.Palette);
    }
}
