using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_SHOP_ITEM $47, INTERAC_SHOPKEEPER $46, and
/// INTERAC_COMPANION_SCRIPTS $71:$0c data used by past room $2:$5e.
/// </summary>
internal sealed class LynnaShopDatabase
{
    internal readonly record struct ItemRecord(
        int SubId,
        int Order,
        int Y,
        int X,
        int PriceTile,
        int Price,
        int TreasureId,
        int Parameter,
        int PromptTextId,
        int ItemTextId,
        string Sprite,
        int TileBase,
        int Palette,
        int AnimationIndex,
        string Animation,
        int ReplacementAddress,
        int ReplacementMask,
        int ReplacementSubId,
        int ReplacementXOffset);

    internal readonly record struct StockRecord(
        ItemRecord Item,
        int Order,
        int Y,
        int X);

    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ItemRecord> _items = new();
    private readonly List<ItemRecord> _placements = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<(int InteractionId, int Animation), string> _animations = new();

    public int Group => Constant("group");
    public int Room => Constant("room");
    public int TextboxPosition => Constant("textbox-position");
    public int ItemCollisionRadius => Constant("item-collision-radius");
    public int LinkCollisionRadius => Constant("link-collision-radius");
    public int GrabNegativePointOffset => Constant("grab-negative-point-offset");
    public int GrabPositivePointOffset => Constant("grab-positive-point-offset");
    public int ShopkeeperRadiusY => Constant("shopkeeper-radius-y");
    public int ShopkeeperRadiusX => Constant("shopkeeper-radius-x");
    public int AButtonPointOffset => Constant("a-button-point-offset");
    public int SelectionLinkYLimit => Constant("selection-link-y-limit");
    public int SelectionXRadius => Constant("selection-x-radius");
    public int TheftLinkY => Constant("theft-link-y");
    public int BoughtItems1Address => Constant("bought-items-1-address");
    public int BoughtItems2Address => Constant("bought-items-2-address");
    public int DimitriStateAddress => Constant("dimitri-state-address");
    public int DimitriSavedMask => Constant("dimitri-saved-mask");
    public int DimitriDisappearMask => Constant("dimitri-disappear-mask");
    public int GlobalCanBuyFlute => Constant("global-can-buy-flute");
    public int NormalGashaBoughtMask => Constant("normal-gasha-bought-mask");
    public int FluteStockMask => Constant("flute-stock-mask");
    public int BombchuOwnedMask => Constant("bombchu-owned-mask");
    public int BombchuMissingMask => Constant("bombchu-missing-mask");
    public int SpecialObjectDimitri => Constant("specialobject-dimitri");
    public IReadOnlyList<ItemRecord> Placements => _placements;

    public LynnaShopDatabase()
    {
        LoadConstants();
        LoadItems();
        LoadTexts();
        LoadAnimations();
        Validate();
    }

    public ItemRecord Item(int subId) => _items.TryGetValue(subId, out ItemRecord item)
        ? item
        : throw new KeyNotFoundException(
            $"Lynna shop item $47:${subId:x2} was not imported.");

    public string Text(int textId) => _texts.TryGetValue(textId, out string? message)
        ? message
        : throw new KeyNotFoundException(
            $"Lynna shop text TX_{textId:x4} was not imported.");

    public string Animation(int interactionId, int animation) =>
        _animations.TryGetValue((interactionId, animation), out string? encoded)
            ? encoded
            : throw new KeyNotFoundException(
                $"Lynna shop animation ${interactionId:x2}:${animation:x2} was not imported.");

    public IReadOnlyList<StockRecord> ResolveStock(OracleSaveData? save)
    {
        UpdateStockMemory(save);
        var stock = new List<StockRecord>(_placements.Count);
        foreach (ItemRecord placement in _placements)
        {
            if (placement.SubId == 0x04 &&
                save is not null && !save.HasTreasure(TreasureDatabase.TreasureBombs))
            {
                continue;
            }

            int subId = placement.SubId;
            if (subId == 0x03 && save?.IsLinkedGame == true)
                subId = 0x13;

            int x = placement.X;
            var visited = new HashSet<int>();
            while (true)
            {
                if (!visited.Add(subId))
                {
                    throw new InvalidOperationException(
                        $"Lynna shop replacement chain loops at $47:${subId:x2}.");
                }
                ItemRecord current = Item(subId);
                bool replace = save is not null && current.ReplacementMask != 0 &&
                    (save.ReadWramByte(current.ReplacementAddress) &
                        current.ReplacementMask) != 0;
                if (!replace)
                {
                    stock.Add(new StockRecord(
                        current, placement.Order, placement.Y, x));
                    break;
                }
                if (current.ReplacementSubId == 0xff)
                    break;
                x += current.ReplacementXOffset;
                subId = current.ReplacementSubId;
            }
        }
        return stock;
    }

    public void ApplyCompanionEntryState(OracleSaveData? save)
    {
        if (save is null)
            return;
        byte state = save.ReadWramByte(DimitriStateAddress);
        if ((state & DimitriSavedMask) == 0)
            return;
        if (save.WriteWramByte(
            DimitriStateAddress, (byte)(state | DimitriDisappearMask)))
        {
            save.CommitInventoryChange();
        }
    }

    private void UpdateStockMemory(OracleSaveData? save)
    {
        if (save is null)
            return;

        byte bought2 = save.ReadWramByte(BoughtItems2Address);
        bought2 &= unchecked((byte)~(FluteStockMask |
            BombchuOwnedMask | BombchuMissingMask));
        if (!save.HasTreasure(0x0e) && save.HasGlobalFlag(GlobalCanBuyFlute))
            bought2 |= (byte)FluteStockMask;
        bought2 |= (byte)(save.HasTreasure(0x0b)
            ? BombchuOwnedMask
            : BombchuMissingMask);
        if (save.WriteWramByte(BoughtItems2Address, bought2))
            save.CommitInventoryChange();
    }

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Lynna shop constant '{key}' was not imported.");

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/lynna_shop_constants.tsv",
            new GeneratedTableSchema(
                "Lynna shop constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _constants.Add(row.RequiredString(0), row.Decimal(1));
        }
    }

    private void LoadItems()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/lynna_shop_items.tsv",
            new GeneratedTableSchema(
                "Lynna shop items",
                GeneratedTableKeySemantics.Unique,
                [
                    "subid", "order", "y", "x", "price-tile", "price", "treasure",
                    "parameter", "prompt-text", "item-text", "sprite", "tile-base",
                    "palette", "animation-index", "encoded-animation", "replacement-address",
                    "replacement-mask", "replacement-subid", "replacement-x-offset"
                ],
                ["subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var item = new ItemRecord(
                row.HexByte(0),
                row.Decimal(1), row.Decimal(2), row.Decimal(3),
                row.HexByte(4), row.UnsignedDecimal(5),
                row.HexByte(6), row.HexByte(7),
                row.HexWord(8), row.HexWord(9),
                row.RequiredString(10), row.HexByte(11),
                row.HexByte(12), row.HexByte(13),
                row.RequiredString(14), row.HexWord(15),
                row.HexByte(16), row.HexByte(17), row.Decimal(18));
            _items.Add(item.SubId, item);
            if (item.Order >= 0)
                _placements.Add(item);
        }
        _placements.Sort(static (a, b) => a.Order.CompareTo(b.Order));
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/lynna_shop_texts.tsv",
            new GeneratedTableSchema(
                "Lynna shop text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int textId = row.HexWord(0);
            string message = row.Base64Utf8(1);
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidOperationException($"Invalid Lynna shop text TX_{textId:x4}.");
            _texts.Add(textId, message);
        }
    }

    private void LoadAnimations()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/lynna_shop_animations.tsv",
            new GeneratedTableSchema(
                "Lynna shop animations",
                GeneratedTableKeySemantics.Unique,
                ["interaction-id", "animation", "encoded-animation"],
                ["interaction-id", "animation"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _animations.Add(
                (row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        }
    }

    private void Validate()
    {
        if (Group != 2 || Room != 0x5e || TextboxPosition != 0 ||
            ItemCollisionRadius != 7 || LinkCollisionRadius != 6 ||
            GrabNegativePointOffset != 6 || GrabPositivePointOffset != 5 ||
            ShopkeeperRadiusY != 6 ||
            ShopkeeperRadiusX != 0x14 || AButtonPointOffset != 10 ||
            SelectionLinkYLimit != 0x3d || SelectionXRadius != 0x0d ||
            TheftLinkY != 0x69 || BoughtItems1Address != 0xc642 ||
            BoughtItems2Address != 0xc643 || DimitriStateAddress != 0xc647 ||
            DimitriSavedMask != 0x20 || DimitriDisappearMask != 0x40 ||
            GlobalCanBuyFlute != 0x1d || SpecialObjectDimitri != 0x0c ||
            _placements.Count != 3 || _items.Count != 7 ||
            Item(0x01).Price != 10 || Item(0x0d).Price != 150 ||
            Item(0x13).ReplacementSubId != 0x03 ||
            !Text(0x0e02).Contains("\\opt()OK", StringComparison.Ordinal) ||
            Text(0x0e02).Contains("\\jump", StringComparison.Ordinal) ||
            Text(0x0e02).Contains("\\cmd8", StringComparison.Ordinal) ||
            !Text(0x0e2a).Contains("No thanks", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Lynna shop data does not match room $2:$5e's imported contract.");
        }
    }

}
