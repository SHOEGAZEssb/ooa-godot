using Godot;
using System.Collections.Generic;

namespace oracleofages;

// decoration.s: $80:$09/$0a. State0 initializes graphics without advancing
// animation. State1 calls interactionAnimate; ordinary interaction gates apply.
internal sealed partial class FountainDecorationRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity
{
    private readonly EnemyAnimationPlayer _animation;
    private bool _initialized;
    internal FountainPlacement Placement { get; }
    internal bool RuinedPalette { get; }
    internal int AnimationFrame => _animation.FrameIndex;
    internal Texture2D Texture => _animation.CurrentTexture;
    internal Vector2 FrameOffset => _animation.CurrentOffset;
    public Node2D Node => this;

    internal FountainDecorationRoomEntity(FountainPlacement placement, FountainDatabase data, OracleSaveData? save)
    {
        Placement = placement;
        Name = $"Fountain_{placement.SubId:x2}";
        Position = placement.Position;
        // The basin keeps visible83; the stream switches to visible80.
        ZIndex = placement.SubId == 9 ? ObjectDrawPriority.FixedLowPriorityZIndex
            : ObjectDrawPriority.FixedHighPriorityZIndex;
        RuinedPalette = data.UsesRuinedPalette(placement.Group, placement.Room, save);
        FountainVisual visual = data.Visual(placement.SubId);
        _animation = new(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{visual.Sprite}.png"),
            [visual.Animation], visual.TileBase, visual.Palette,
            paletteOverrides: data.Palettes(RuinedPalette), positionedOam: true);
        _animation.SetAnimation(0);
        Visible = false;
    }

    private void InitializeState()
    {
        _initialized = true;
        Visible = true;
        QueueRedraw();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) InitializeState();
        else _animation.Advance();
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        InitializeState();
        return ScreenTransitionPresentation.Visible;
    }

    public new void SetTransitionDrawOffset(Vector2 offset) => base.SetTransitionDrawOffset(offset);
    public override void _Draw() => DrawTexture(_animation.CurrentTexture,
        _animation.CurrentOffset + SourceOamDrawOffset);
}
