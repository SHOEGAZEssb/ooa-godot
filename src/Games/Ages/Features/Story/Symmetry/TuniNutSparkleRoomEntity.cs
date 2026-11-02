
namespace Oracles;

// sparkle.s:$84:$07 takes its parent's Y/X/Z on its own always-update turn.
internal sealed partial class TuniNutSparkleRoomEntity : TransitionOffsetNode2D, INativeInteractionRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly RoomEventContext _context;
    private readonly TuniNutRoomEntity _parent;
    private readonly EnemyAnimationPlayer _animation;
    internal int State { get; private set; }
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public Node2D Node => this;
    internal TuniNutSparkleRoomEntity(RoomEventContext context, TuniNutRoomEntity parent, TimedSparkleVisual visual)
    {
        _context = context; _parent = parent; AssetRoot = visual.AssetRoot;
        Position = parent.Position; Visible = false; ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        _animation = new(this, 1);
        int count = OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames.Length;
        _animation.Load(OracleGraphicsCache.LoadImage(AssetRoot + $"gfx/{visual.Sprite}.png"),
            [visual.Animation], visual.TileBase, visual.Palette, positionedOam: true,
            animationSourceOffsets: [Enumerable.Repeat(visual.SourceOffset, count).ToArray()]);
        _animation.SetAnimation(0);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        => Advance(frame.Counter);
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance(frame.Counter);
    private void Advance(int counter)
    {
        if (State == 0) { State = 1; Visible = true; return; }
        Position = _parent.Position;
        if ((_context.Entities.RuntimeState.ReadWramByte(AgesWramAddress.wTmpcfc0) & 1) != 0)
        { Finished = true; Visible = false; return; }
        _animation.Advance(); Visible = (counter & 1) == 0; QueueRedraw();
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public override void _Draw()
    { if (Visible && !Finished) DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + new Vector2(0, _parent.Height >> 8) + TransitionDrawOffset); }
}
