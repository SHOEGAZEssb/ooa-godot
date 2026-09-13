using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_ESSENCE $7f:$02 in reserved interaction $d1.</summary>
internal sealed partial class DungeonEssenceGlow : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly DungeonEssence _owner;
    private readonly EnemyAnimationPlayer _animation;
    internal bool Initialized { get; private set; }
    internal int Z { get; private set; }
    internal int AnimationFrame => _animation.FrameIndex;
    public Node2D Node => this;
    // The reserved object's enabled=$81 bypasses text/disabled-state filters.
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;

    internal DungeonEssenceGlow(DungeonEssence owner, DungeonInteractionVisual visual)
    {
        _owner = owner;
        Position = owner.Position.Floor(); // objectCopyPosition before parent zh=-$10.
        Name = "EssenceGlow_7f02";
        ZIndex = NpcCharacter.BehindLinkZIndex; // objectSetVisible82.
        Visible = false;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations,
            visual.TileBase, visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(0);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (frame.Player.IsDying) return;
        if (!Initialized) { InitializeState(); return; }
        // Reserved $d1 runs before its dynamic parent, so these are the
        // parent's preceding high XYZ bytes, not its next movement result.
        Position = _owner.Position.Floor();
        Z = _owner.DrawZ;
        _animation.Advance();
        if (_animation.ConsumeParameter() != 0) Visible = !Visible;
        QueueRedraw();
    }
    private void InitializeState() { Initialized = true; Visible = true; }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (!Initialized) InitializeState(); return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }
    public override void _Draw()
    { if (Visible) DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + new Vector2(0, Z) + SourceOamDrawOffset); }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
