using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_GREAT_FAIRY_HEART $30: 32-angle orbit, three updates per angle.</summary>
internal sealed partial class FountainFairyHeartRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly FountainFairyRoomEntity _owner;
    private readonly Func<int> _displayedHealth;
    private readonly EnemyAnimationPlayer _animation;
    private readonly OracleRuntimeState _memory;
    private int _counter = 3;
    internal int Angle { get; private set; }
    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal FountainFairyHeartRoomEntity(FountainFairyRoomEntity owner,
        FountainFairyDatabase database, Func<int> displayedHealth, OracleRuntimeState memory)
    {
        _owner = owner;
        _displayedHealth = displayedHealth;
        _memory = memory;
        Name = "FountainFairyHeart_30";
        ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(EnemyVisualSource.LoadComposite([database.HeartSprite]),
            [database.HeartAnimation], database.HeartTileBase, database.HeartPalette,
            sourceGrayscaleInverted: database.HeartInverted);
        _animation.SetAnimation(0);
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        // objectSetPositionInCircleArc multiplies the signed 8.8 SPEED_100
        // vector by $20, then adds only the wrapping high bytes to Link.
        Vector2I offset = NativeObjectMovement.CircleArcOffset(_memory,32,Angle);
        Vector2 target=frame.LinkTarget??frame.Player.EnemyContactPosition;
        Position = new Vector2(
            ((int)target.X + offset.X) & 0xff,
            ((int)target.Y + offset.Y) & 0xff);
        if (--_counter == 0)
        {
            _counter = 3;
            Angle = (Angle - 1) & ObjectAngle.Mask;
            if (Angle == ObjectAngle.Up && _displayedHealth() == frame.Player.MaxHealthQuarters)
            {
                Finished = true;
                Visible = false;
                _owner.HeartFinished();
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
            DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + SourceOamDrawOffset);
    }
}
