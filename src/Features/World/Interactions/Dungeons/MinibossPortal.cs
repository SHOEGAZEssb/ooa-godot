using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class MinibossPortal : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;

    internal int AnimationFrame => _animation.FrameIndex;

    internal void Initialize(DungeonEntranceInteractionDatabase data)
    {
        Name = "MinibossPortal";
        int packed = data.PortalPosition;
        Position = new Vector2(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        Visible = false;

        DungeonEntranceInteractionDatabaseVisualRecord visual = data.PortalVisual;
        Image image = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            image, new[] { visual.Animation }, visual.TileBase, visual.Palette);
        _animation.SetAnimation(0);
    }

    internal void AdvanceAnimation() => _animation.Advance();

    public override void _Draw()
    {
        if (Visible && _animation is not null && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}
