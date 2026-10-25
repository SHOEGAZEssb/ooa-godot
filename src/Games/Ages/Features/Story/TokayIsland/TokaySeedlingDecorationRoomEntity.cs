using Godot;
using System.Collections.Generic;

namespace Oracles;

/// <summary>INTERAC_DECORATION $80:$04 in room 1:ac.</summary>
internal sealed partial class TokaySeedlingDecorationRoomEntity :
    TransitionOffsetNode2D,
    IRoomEntity,
    IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity
{
    private readonly EnemyAnimationPlayer _animation;

    public Node2D Node => this;
    internal TokaySeedlingPlotRecord Record { get; }

    internal TokaySeedlingDecorationRoomEntity(
        TokaySeedlingPlotRecord record)
    {
        Record = record;
        Name = "TokayScentSeedling";
        Position = new Vector2(record.X, record.Y);
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            EnemyVisualSource.LoadComposite([record.Sprite]),
            [record.Animation],
            record.TileBase,
            record.Palette);
        _animation.SetAnimation(0);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        _animation.Advance();
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        Visible = true;
        return ScreenTransitionPresentation.Visible;
    }

    public new void SetTransitionDrawOffset(Vector2 offset) =>
        base.SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                -_animation.CurrentTexture.GetSize() / 2.0f +
                    TransitionDrawOffset);
        }
    }
}

internal sealed record TokaySeedlingDecorationSpawn(
    TokaySeedlingPlotRecord Record)
    : RoomEntitySpawn(UpdateThisFrame: true);
