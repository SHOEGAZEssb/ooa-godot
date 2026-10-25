using Godot;
using static Oracles.OracleOamCompositor;
using System;

namespace Oracles;

/// <summary>
/// INTERAC_TOKAY $48:$06-$0a plus the related INTERAC_ACCESSORY $63 created
/// by tokayInitHeldItem. The accessory is frozen on its initial frame and
/// follows the Tokay at the source offset bc=$f400 (Y -12, X 0).
/// </summary>
internal partial class TokayHoldingItemCharacter : TokayCharacter
{
    private static readonly Vector2 HeldItemOffset = new(0, -12);

    private Texture2D? _heldItemTexture;
    private Vector2 _heldItemFrameOffset;
    private bool _heldItemVisible;

    internal bool HeldItemVisible => _heldItemVisible && Active;
    internal Vector2 HeldItemPosition => Position + HeldItemOffset;
    internal Vector2I HeldItemTextureSize => _heldItemTexture is null
        ? Vector2I.Zero
        : new Vector2I(_heldItemTexture.GetWidth(), _heldItemTexture.GetHeight());
    internal int HeldItemOpaquePixels
    {
        get
        {
            if (_heldItemTexture is null)
                return 0;
            Image image = _heldItemTexture.GetImage();
            int count = 0;
            for (int y = 0; y < image.GetHeight(); y++)
            for (int x = 0; x < image.GetWidth(); x++)
                if (image.GetPixel(x, y).A > 0.1f)
                    count++;
            return count;
        }
    }

    internal void InitializeHoldingItem(
        NpcRecord record,
        TokayHeldItemRecord item,
        bool itemReturned)
    {
        Initialize(record);
        if (record.Id != AgesInteractionId.Tokay || record.SubId != item.SubId ||
            record.SubId is < 0x06 or > 0x0a)
        {
            throw new InvalidOperationException(
                $"NPC {record.Group}:{record.Room:x2} " +
                $"${record.Id:x2}:${record.SubId:x2} cannot hold imported " +
                $"Tokay item ${item.SubId:x2}.");
        }

        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(item.ItemAnimation);
        if (animation.Frames.Length == 0)
        {
            throw new InvalidOperationException(
                $"INTERAC_ACCESSORY $63:${item.ItemGraphic:x2} has no " +
                "imported animation frames.");
        }
        Image source = OracleGraphicsCache.LoadImage(
            AgesGameContent.AssetRoot + $"gfx/{item.ItemSprite}.png");
        AnimationFrameDefinition frame = animation.Frames[0];
        (_heldItemTexture, _heldItemFrameOffset) =
            BuildPositionedOamTexture(
                source, frame.EncodedOam, item.ItemTileBase,
                item.ItemPalette, paletteOverride: null,
                sourceGrayscaleInverted: true);
        _heldItemVisible = !itemReturned;
        QueueRedraw();
    }

    internal void RemoveHeldItem()
    {
        if (!_heldItemVisible)
            return;
        _heldItemVisible = false;
        QueueRedraw();
    }

    internal void DrawHeldItem(CanvasItem canvas)
    {
        if (_heldItemTexture is not null)
        {
            canvas.DrawTexture(
                _heldItemTexture,
                _heldItemFrameOffset + SourceOamWrapOffset);
        }
    }
}
