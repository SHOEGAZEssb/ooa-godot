using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Visible, A-button-sensitive INTERAC_TOKAY_SHOP_ITEM `$81 stock.</summary>
internal partial class TokayShopItem : TransitionOffsetNode2D
{
    private readonly List<TokayShopItemFrame> _frames = new();
    private int _frame;
    private int _frameTicks;

    internal TokayShopPlacementRecord Placement { get; private set; }
    internal int OriginalSubId { get; private set; }
    internal int SubId { get; private set; }
    internal int Treasure { get; private set; }
    internal int CollisionRadius { get; private set; }
    internal bool Removed { get; private set; }

    internal void Initialize(
        TokayShopPlacementRecord placement,
        int originalSubId,
        int subId,
        int treasure,
        int collisionRadius)
    {
        if (collisionRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(collisionRadius));
        _frames.Clear();
        _frame = 0;
        _frameTicks = 0;
        Placement = placement;
        OriginalSubId = originalSubId;
        SubId = subId;
        Treasure = treasure;
        CollisionRadius = collisionRadius;
        Removed = false;
        Visible = true;
        Position = new Vector2(placement.X, placement.Y);
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{placement.Sprite}.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(placement.Animation);
        foreach (AnimationFrameDefinition frame in animation.Frames)
        {
            (Texture2D texture, Vector2 offset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source, frame.EncodedOam, placement.TileBase,
                    placement.Palette, paletteOverride: null,
                    sourceGrayscaleInverted: true);
            _frames.Add(new TokayShopItemFrame(texture, offset, frame.Duration));
        }
        if (_frames.Count == 0)
            throw new InvalidOperationException(
                $"Tokay shop item $81:${subId:x2} has no animation frames.");
        QueueRedraw();
    }

    internal bool CanInteract(Player player)
    {
        if (Removed || player.CutsceneControlled)
            return false;
        Vector2 point = OracleObjectMath.ToPixelPosition(player.Position) +
            player.FacingVector * NpcCharacter.AButtonPointOffset;
        Vector2 delta = Position - point;
        return Mathf.Abs(delta.X) < 12 && Mathf.Abs(delta.Y) < 12;
    }

    internal bool BlocksLink(Vector2 linkCenter)
    {
        if (Removed)
            return false;
        Vector2 delta = OracleObjectMath.ToPixelPosition(linkCenter) -
            OracleObjectMath.ToPixelPosition(Position);
        float combinedRadius = CollisionRadius +
            NpcCharacter.LinkCollisionRadius;
        return Mathf.Abs(delta.X) < combinedRadius &&
            Mathf.Abs(delta.Y) < combinedRadius;
    }

    internal void Remove()
    {
        Removed = true;
        Visible = false;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Removed || _frames.Count <= 1)
            return;
        _frameTicks++;
        if (_frameTicks < _frames[_frame].Duration)
            return;
        _frameTicks = 0;
        _frame = (_frame + 1) % _frames.Count;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Removed || _frames.Count == 0)
            return;
        TokayShopItemFrame frame = _frames[_frame];
        DrawTexture(frame.Texture, frame.Offset + TransitionDrawOffset);
    }
}

internal sealed record TokayShopItemFrame(
    Texture2D Texture, Vector2 Offset, int Duration);
