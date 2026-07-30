using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_OWL_STATUE ($13). The part installs its own solid floor cell, accepts
/// only a Mystery Seed collision, emits six sparkles, and selects TX_39xx from
/// its source subid.
/// </summary>
internal sealed partial class OwlStatueRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, ISeedHittableRoomEntity
{
    private readonly OwlStatueRecord _record;
    private readonly Action<int, string, Vector2> _messageRequested;
    private readonly EnemyAnimationPlayer _animation;
    private int _counter;

    internal OwlStatueRoomEntity(
        RoomObjectRecord source,
        OwlStatueRecord record,
        OracleRoomData room,
        Action<int, string, Vector2> messageRequested,
        Func<long> animationTick)
    {
        if (source.Kind != RoomObjectKind.ReservingPart ||
            source.Id != OwlStatueDatabase.PartId ||
            source.SubId != record.SubId ||
            source.PackedPosition < 0)
        {
            throw new InvalidOperationException(
                $"{source.Source} cannot create PART_OWL_STATUE.");
        }

        _record = record;
        _messageRequested = messageRequested;
        Name = $"OwlStatue_{record.SubId:x2}";
        // objectSetVisible83 fixes PART_OWL_STATUE at source priority 3.
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        Position = new Vector2(
            (source.PackedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (source.PackedPosition >> 4) * OracleRoomData.MetatileSize + 8);

        room.SetPositionTileAndCollision(
            Position,
            (byte)record.FloorTile,
            (byte)record.FloorCollision,
            animationTick());

        Image sourceImage = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        _animation = new EnemyAnimationPlayer(this, 2);
        _animation.Load(
            sourceImage,
            [record.IdleAnimation, record.SpeakingAnimation],
            record.TileBase,
            record.Palette);
        _animation.SetAnimation(0);
        Visible = true;
    }

    public Node2D Node => this;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);
    internal OwlStatueState State { get; private set; }
    internal int Counter => _counter;
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationIndex => _animation.AnimationIndex;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_record.RadiusX, _record.RadiusY),
        new Vector2(_record.RadiusX * 2, _record.RadiusY * 2));

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        ElapsedUpdates++;
        switch (State)
        {
            case OwlStatueState.Idle:
                return;

            case OwlStatueState.Activating:
                _counter--;
                if (_counter == 0)
                {
                    State = OwlStatueState.Speaking;
                    _counter = _record.SpeakingCounter;
                    _animation.SetAnimation(1);
                    return;
                }
                if ((_counter & 0x07) == 0)
                {
                    int offsetIndex = (_counter >> 3) - 1;
                    spawns.Add(new OwlStatueSparkleSpawn(
                        Position + _record.SparkleOffsets[offsetIndex],
                        _record.Sparkle));
                }
                return;

            case OwlStatueState.Speaking:
                _counter--;
                if (_counter == 0)
                {
                    State = OwlStatueState.Idle;
                    _animation.SetAnimation(0);
                    return;
                }
                if (_counter == _record.TextCounter)
                {
                    _messageRequested(
                        _record.TextId,
                        _record.Message,
                        Position);
                }
                return;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(State), State, "Unknown Owl Statue state.");
        }
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem != OwlStatueDatabase.MysterySeedItem ||
            !hitbox.Intersects(CollisionBounds))
        {
            return SeedHitResult.None;
        }

        // func_07_47b7 still terminates the Mystery Seed against a priority-
        // guarded Owl while the part's state >= 2; only the activation is
        // suppressed.
        if (State == OwlStatueState.Idle)
        {
            State = OwlStatueState.Activating;
            _counter = _record.ActivationCounter;
        }
        return SeedHitResult.Activate;
    }

    public override void _Draw()
    {
        if (Visible && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}

internal enum OwlStatueState
{
    Idle,
    Activating,
    Speaking
}

internal sealed record OwlStatueSparkleSpawn(
    Vector2 Position,
    OwlStatueSparkleRecord Visual) : RoomEntitySpawn;
