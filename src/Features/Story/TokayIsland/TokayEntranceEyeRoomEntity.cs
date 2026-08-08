using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DECORATION $80:$05/$06 at room 1:ba.</summary>
internal sealed partial class TokayEntranceEyeRoomEntity : TransitionOffsetNode2D,
    IRoomEntity,
    IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity
{
    private readonly EnemyAnimationPlayer _animation;

    public Node2D Node => this;
    internal TokayEntranceEyeRecord Record { get; }
    internal int AnimationFrame => _animation.FrameIndex;

    internal TokayEntranceEyeRoomEntity(TokayEntranceEyeRecord record)
    {
        Record = record;
        Name = $"TokayEntranceEye_{record.SubId:x2}";
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
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}

internal sealed record TokayEntranceEyeSpawn(TokayEntranceEyeRecord Record)
    : RoomEntitySpawn(UpdateThisFrame: true);
