using Godot;
using System.Collections.Generic;

namespace oracleofages;

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
    internal int AnimationFrame => _animation.FrameIndex;
    internal int OpaquePixels => _animation.HasFrames
        ? CountOpaquePixels(_animation.CurrentTexture.GetImage())
        : 0;

    internal TokaySeedlingDecorationRoomEntity(
        TokaySeedlingPlotRecord record)
    {
        Record = record;
        Name = "TokayScentSeedling";
        Position = new Vector2(record.X, record.Y);
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
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

    private static int CountOpaquePixels(Image image)
    {
        int count = 0;
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            if (image.GetPixel(x, y).A > 0.1f)
                count++;
        }
        return count;
    }
}

internal sealed record TokaySeedlingDecorationSpawn(
    TokaySeedlingPlotRecord Record)
    : RoomEntitySpawn(UpdateThisFrame: true);
