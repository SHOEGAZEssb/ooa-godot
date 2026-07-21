using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/lynna_shop_constants.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 2 ||
                !_constants.TryAdd(fields[0], int.Parse(fields[1])))
            {
                throw new InvalidOperationException(
                    $"Malformed Lynna shop constant: {line}");
            }
        }
    }

    private void LoadItems()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/lynna_shop_items.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 19)
                throw new InvalidOperationException($"Malformed Lynna shop item: {line}");
            var item = new ItemRecord(
                Convert.ToInt32(fields[0], 16),
                int.Parse(fields[1]), int.Parse(fields[2]), int.Parse(fields[3]),
                Convert.ToInt32(fields[4], 16), int.Parse(fields[5]),
                Convert.ToInt32(fields[6], 16), Convert.ToInt32(fields[7], 16),
                Convert.ToInt32(fields[8], 16), Convert.ToInt32(fields[9], 16),
                fields[10], Convert.ToInt32(fields[11], 16),
                Convert.ToInt32(fields[12], 16), Convert.ToInt32(fields[13], 16),
                fields[14], Convert.ToInt32(fields[15], 16),
                Convert.ToInt32(fields[16], 16), Convert.ToInt32(fields[17], 16),
                int.Parse(fields[18]));
            if (!_items.TryAdd(item.SubId, item))
                throw new InvalidOperationException(
                    $"Duplicate Lynna shop item $47:${item.SubId:x2}.");
            if (item.Order >= 0)
                _placements.Add(item);
        }
        _placements.Sort(static (a, b) => a.Order.CompareTo(b.Order));
    }

    private void LoadTexts()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/lynna_shop_texts.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 2)
                throw new InvalidOperationException($"Malformed Lynna shop text: {line}");
            int textId = Convert.ToInt32(fields[0], 16);
            string message = Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
            if (!_texts.TryAdd(textId, message) || string.IsNullOrWhiteSpace(message))
                throw new InvalidOperationException($"Invalid Lynna shop text TX_{textId:x4}.");
        }
    }

    private void LoadAnimations()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/lynna_shop_animations.tsv");
        foreach (string line in DataLines(source))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 3 ||
                !_animations.TryAdd(
                    (Convert.ToInt32(fields[0], 16), Convert.ToInt32(fields[1], 16)),
                    fields[2]))
            {
                throw new InvalidOperationException(
                    $"Malformed Lynna shop animation: {line}");
            }
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

    private static IEnumerable<string> DataLines(string source)
    {
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
                yield return line;
        }
    }
}
