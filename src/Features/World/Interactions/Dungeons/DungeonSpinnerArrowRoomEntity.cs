using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_SPINNER $7d:$02 is a separately allocated interaction. A full
// pool omits it without retrying or preventing the parent from turning.
internal sealed partial class DungeonSpinnerArrowRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonSpinnerRoomEntity _parent;
    private readonly EnemyAnimationPlayer _animation;
    private bool _initialized;
    private bool _red;
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int AnimationIndex => _animation.AnimationIndex;

    internal DungeonSpinnerArrowRoomEntity(DungeonSpinnerRoomEntity parent,DungeonInteractionVisual visual)
    {
        _parent = parent; Position = parent.Position; Visible = false;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        _animation = new EnemyAnimationPlayer(this,visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites),visual.Animations,visual.TileBase,visual.Palette,
            sourceGrayscaleInverted:visual.SourceGrayscaleInverted,positionedOam:true,paletteVariants:[4,5]);
        _animation.SetAnimation(2);
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Advance();
        return ScreenTransitionPresentation.Visible;
    }
    private void Advance()
    {
        if (!_initialized)
        {
            // spinner_subid02 state0 uses H=D while copying angle, so the
            // fresh child's zero angle survives. Its state1 fallthrough then
            // selects animation3 immediately when the parent angle is$08.
            _initialized = true; Visible = true;
        }
        if (_red != _parent.Red)
        {
            _red = _parent.Red;
            _animation.SetAnimation(_red ? 3 : 2);
        }
        _animation.Advance(); QueueRedraw();
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public override void _Draw()
    {
        if (Visible) DrawTexture(_animation.CurrentTextureForPalette(_red ? 5 : 4),
            _animation.CurrentOffset+SourceOamDrawOffset);
    }
}
