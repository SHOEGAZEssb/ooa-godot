
namespace Oracles;

internal abstract partial class AnimatedDecorationRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity
{
    private readonly EnemyAnimationPlayer _animation;
    private readonly Vector2? _drawOrigin;
    public Node2D Node => this;

    protected AnimatedDecorationRoomEntity(string assetRoot, string name, Vector2 position,
        string sprite, string animation, int tileBase, int palette, Vector2? drawOrigin = null)
    {
        AssetRoot = assetRoot;
        Name = name;
        Position = position;
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        _drawOrigin = drawOrigin;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(EnemyVisualSource.LoadComposite([sprite], assetRoot), [animation], tileBase, palette);
        _animation.SetAnimation(0);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => _animation.Advance();

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Visible = true;
        return ScreenTransitionPresentation.Visible;
    }

    public override void _Draw()
    {
        if (Visible && _animation.HasFrames)
            DrawTexture(_animation.CurrentTexture,
                (_drawOrigin ?? -_animation.CurrentTexture.GetSize() / 2.0f) + TransitionDrawOffset);
    }
}
