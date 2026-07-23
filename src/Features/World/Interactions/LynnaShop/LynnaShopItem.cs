using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Liftable INTERAC_SHOP_ITEM $47 product with its source OAM animation and
/// background-tile price display.
/// </summary>
internal partial class LynnaShopItem : TransitionOffsetNode2D
{

    private readonly List<LynnaShopItemFrame> _frames = new();
    private Texture2D[] _digitTextures = Array.Empty<Texture2D>();
    private int _frame;
    private Vector2 _shelfPosition;
    private Vector2 _pricePosition;

    public ItemRecord Record { get; private set; }
    public int Order { get; private set; }
    public bool Held { get; private set; }
    public bool Removed { get; private set; }
    public Vector2 ShelfPosition => _shelfPosition;
    internal int AnimationFrame => _frame;
    internal Vector2 PricePosition => _pricePosition;
    internal ulong CurrentPixelHash => HashImage(_frames[_frame].Texture.GetImage());
    internal ulong DigitPixelHash => HashImage(_digitTextures[0].GetImage());
    internal int DigitColorCount => CountColors(_digitTextures[0].GetImage());

    public void Initialize(
        StockRecord stock,
        OracleRoomData room)
    {
        Record = stock.Item;
        Order = stock.Order;
        _shelfPosition = new Vector2(stock.X, stock.Y);
        Position = _shelfPosition;
        _pricePosition = new Vector2(
            (Record.PriceTile & 0x1f) * 8,
            (Record.PriceTile >> 5) * 8);

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{Record.Sprite}.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(Record.Animation);
        foreach (AnimationFrameDefinition sourceFrame in
            animation.Frames)
        {
            (Texture2D texture, Vector2 offset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source, sourceFrame.EncodedOam,
                    Record.TileBase, Record.Palette,
                    paletteOverride: null,
                    sourceGrayscaleInverted: true);
            _frames.Add(new LynnaShopItemFrame(texture, offset, sourceFrame.Duration));
        }
        if (_frames.Count == 0)
            throw new InvalidOperationException(
                $"Lynna shop item $47:${Record.SubId:x2} has no animation frames.");
        // roomTileChangesAfterLoad04 loads TREE_GFXH_03
        // (gfx_inventory_hud_1) into the $9200 tree slot. Consequently the
        // source's price tile base $30 addresses this sheet's tile $10, not
        // gfx_hud tile $00.
        Image inventoryHud = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/inventory/gfx_inventory_hud_1.png");
        _digitTextures = new Texture2D[10];
        for (int digit = 0; digit < _digitTextures.Length; digit++)
            _digitTextures[digit] = room.BuildBackgroundTileTexture(
                inventoryHud, 0x10 + digit, rawPalette: 6);
        QueueRedraw();
    }

    public void UpdateFrame(Player player)
    {
        if (Removed)
            return;
        if (Held)
        {
            UpdateHeldPosition(player);
            QueueRedraw();
        }
    }

    public bool CanPickup(
        Player player,
        int itemCollisionRadius,
        int linkCollisionRadius,
        int negativePointOffset,
        int positivePointOffset)
    {
        if (Held || Removed || player.CutsceneControlled ||
            player.IsHoldingItemOneHand || player.IsHoldingItemTwoHands ||
            player.IsCarryingObject)
        {
            return false;
        }

        // checkGrabbableObjects uses Link's high-byte position with the
        // asymmetric $fa/$05 direction table, then combines Link's $06/$06
        // radii with the shop item's $07/$07 radii. Preserve the source's
        // [-radius,+radius) comparison rather than an absolute-distance test.
        Vector2I direction = player.FacingVector;
        Vector2 pointOffset = direction == Vector2I.Up
            ? new Vector2(0, -negativePointOffset)
            : direction == Vector2I.Right
                ? new Vector2(positivePointOffset, 0)
                : direction == Vector2I.Down
                    ? new Vector2(0, positivePointOffset)
                    : new Vector2(-negativePointOffset, 0);
        Vector2 point = OracleObjectMath.ToPixelPosition(player.Position) + pointOffset;
        Vector2 delta = Position - point;
        int combinedRadius = itemCollisionRadius + linkCollisionRadius;
        return delta.Y >= -combinedRadius && delta.Y < combinedRadius &&
            delta.X >= -combinedRadius && delta.X < combinedRadius;
    }

    public void Pickup(Player player)
    {
        if (Held || Removed)
            throw new InvalidOperationException("A shop item cannot be lifted twice.");
        Held = true;
        ZIndex = NpcCharacter.InFrontOfLinkZIndex;
        player.BeginCarriedObjectPose();
        UpdateHeldPosition(player);
        QueueRedraw();
    }

    public void ReturnToShelf(Player player)
    {
        if (!Held || Removed)
            return;
        Held = false;
        player.EndCarriedObjectPose();
        Position = _shelfPosition;
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        QueueRedraw();
    }

    public void FinishPurchase(Player player)
    {
        if (Removed)
            return;
        Held = false;
        Removed = true;
        player.EndCarriedObjectPose();
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Removed)
            return;
        LynnaShopItemFrame frame = _frames[_frame];
        DrawTexture(frame.Texture, frame.Offset + TransitionDrawOffset);
        if (Held)
            return;

        string price = Record.Price.ToString();
        Vector2 destination = _pricePosition - Position + TransitionDrawOffset;
        for (int index = 0; index < price.Length; index++)
        {
            DrawTexture(
                _digitTextures[price[index] - '0'],
                destination + new Vector2(index * 8, 0));
        }
    }

    private void UpdateHeldPosition(Player player)
    {
        // shopItemState2 writes wLinkGrabState2=$08. The generic grabbed-object
        // position table then uses Z=-$0d for both walk phase 2 facings and for
        // phase 3, except right/left phase 2 use Z=-$0e. X remains unchanged.
        Vector2I facing = player.FacingVector;
        bool sideFacing = facing == Vector2I.Right || facing == Vector2I.Left;
        int zOffset = player.CarriedObjectAnimationFrame == 0 && sideFacing
            ? -14
            : -13;
        Position = OracleObjectMath.ToPixelPosition(player.Position) +
            new Vector2(0, zOffset);
    }

    private static ulong HashImage(Image image)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in image.GetData())
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static int CountColors(Image image)
    {
        var colors = new HashSet<uint>();
        byte[] pixels = image.GetData();
        for (int offset = 0; offset < pixels.Length; offset += 4)
            colors.Add(BitConverter.ToUInt32(pixels, offset));
        return colors.Count;
    }
}

internal sealed record LynnaShopItemFrame(Texture2D Texture, Vector2 Offset, int Duration);
