using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived INTERAC_TOKAY, INTERAC_TOKAY_SHOP_ITEM, and Wild Tokay
/// contracts shared by the island's room entities and event owner.
/// </summary>
internal sealed class TokayIslandDatabase
{
    private readonly Dictionary<string, TokayConstant> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, string> _animations = new();
    private readonly Dictionary<int, TokayHeldItemRecord> _heldItems = new();
    private readonly Dictionary<int, WildTokayPrizeRecord> _wildPrizes = new();
    private readonly List<WildTokayStartTileRecord> _wildStartTiles = new();
    private readonly Dictionary<int, WildTokayMeatAccessoryRecord>
        _wildMeatAccessory = new();
    private readonly Dictionary<int, TokayShopPlacementRecord> _shopVisuals = new();
    private readonly List<TokayShopPlacementRecord> _shopPlacements = new();
    private readonly Dictionary<(int Level, int RandomIndex), WildTokayPatternRecord>
        _wildPatterns = new();

    internal int PastGameGroup => Constant("group-past-manager");
    internal int PastGameRoom => Constant("room-past-manager");
    internal int PresentGameGroup => Constant("group-present-manager");
    internal int PresentGameRoom => Constant("room-present-manager");
    internal int ShopGroup => Constant("group-shop");
    internal int ShopRoom => Constant("room-shop");
    internal int ShopItemCollisionRadius =>
        Constant("shop-item-collision-radius");
    internal int DimitriStateAddress => Constant("dimitri-state-address");
    internal int WildLevelAddress => Constant("wild-level-address");
    internal int BoughtFeatherFlag => Constant("global-bought-feather");
    internal int BoughtBraceletFlag => Constant("global-bought-bracelet");
    internal int FinishedGameFlag => Constant("global-finished-game");
    internal int BeganSecretFlag => Constant("global-began-secret");
    internal int DoneSecretFlag => Constant("global-done-secret");
    internal int SoundGetSeed => Constant("sound-get-seed");
    internal int SoundJump => Constant("sound-jump");
    internal int SoundError => Constant("sound-error");
    internal int SoundSuccess => Constant("sound-success");
    internal int ParticipantLeftX => Constant("participant-left-x");
    internal int ParticipantRightX => Constant("participant-right-x");
    internal int ParticipantStartY => Constant("participant-start-y");
    internal int ParticipantAnimation => Constant("participant-animation");
    internal int GameLinkY => Constant("game-link-y");
    internal int GameLinkX => Constant("game-link-x");
    internal int GameReturnPosition => Constant("game-return-position");
    internal int WildCycleCount(int level) =>
        Constant($"wild-cycle-count-level-{level}");
    internal int GameSpawnDelay => Constant("game-spawn-delay");
    internal int GameStartDelay => Constant("game-start-delay");
    internal int GameFadeInDelay => Constant("game-fade-in-delay");
    internal int MeatStartY => Constant("meat-start-y");
    internal int MeatStartX => Constant("meat-start-x");
    internal int MeatStartZ => Constant("meat-start-z");
    internal int MeatFallDelay => Constant("meat-fall-delay");
    internal int MeatFallGravity => Constant("meat-fall-gravity");
    internal string MeatSprite => TextConstant("meat-sprite");
    internal int MeatTileBase => Constant("meat-tile-base");
    internal int MeatPalette => Constant("meat-palette");
    internal string MeatAnimation => TextConstant("meat-animation");
    internal IReadOnlyList<TokayShopPlacementRecord> ShopPlacements =>
        _shopPlacements;
    internal IReadOnlyList<WildTokayStartTileRecord> WildStartTiles =>
        _wildStartTiles;

    internal TokayIslandDatabase()
    {
        LoadConstants();
        LoadTexts();
        LoadAnimations();
        LoadHeldItems();
        LoadWildPrizes();
        LoadWildStartTiles();
        LoadWildMeatAccessory();
        LoadShopPlacements();
        LoadWildPatterns();
        Validate();
    }

    internal int Constant(string key) =>
        _constants.TryGetValue(key, out TokayConstant value)
            ? value.Value
            : throw new KeyNotFoundException(
                $"Tokay Island constant '{key}' was not imported.");

    internal string TextConstant(string key) =>
        _constants.TryGetValue(key, out TokayConstant value) && value.Text != "-"
            ? value.Text
            : throw new KeyNotFoundException(
                $"Tokay Island text constant '{key}' was not imported.");

    internal string Text(int textId) =>
        _texts.TryGetValue(textId, out string? text)
            ? text
            : throw new KeyNotFoundException(
                $"Tokay Island text TX_{textId:x4} was not imported.");

    internal string Animation(int animation) =>
        _animations.TryGetValue(animation, out string? encoded)
            ? encoded
            : throw new KeyNotFoundException(
                $"INTERAC_TOKAY animation ${animation:x2} was not imported.");

    internal TokayHeldItemRecord HeldItem(int subId) =>
        _heldItems.TryGetValue(subId, out TokayHeldItemRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"INTERAC_TOKAY holder ${subId:x2} was not imported.");

    internal WildTokayPrizeRecord WildPrize(int prizeCode) =>
        _wildPrizes.TryGetValue(prizeCode, out WildTokayPrizeRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Wild Tokay prize code ${prizeCode:x2} was not imported.");

    internal WildTokayMeatAccessoryRecord WildMeatAccessory(int parameter) =>
        _wildMeatAccessory.TryGetValue(
            parameter, out WildTokayMeatAccessoryRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Wild Tokay held-meat parameter ${parameter:x2} was not imported.");

    internal TokayShopPlacementRecord ShopVisual(int subId) =>
        _shopVisuals.TryGetValue(subId, out TokayShopPlacementRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"INTERAC_TOKAY_SHOP_ITEM visual ${subId:x2} was not imported.");

    internal WildTokayPatternRecord WildPattern(int level, int randomIndex) =>
        _wildPatterns.TryGetValue((level, randomIndex), out WildTokayPatternRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Wild Tokay pattern level {level}, random ${randomIndex:x2} was not imported.");

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_island_constants.tsv",
            new GeneratedTableSchema(
                "Tokay Island constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "text"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(
                row.RequiredString(0),
                new TokayConstant(row.Decimal(1), row.RequiredString(2)));
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_island_texts.tsv",
            new GeneratedTableSchema(
                "Tokay Island texts",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
    }

    private void LoadAnimations()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_island_animations.tsv",
            new GeneratedTableSchema(
                "Tokay animations",
                GeneratedTableKeySemantics.Unique,
                ["animation", "encoded"],
                ["animation"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _animations.Add(row.HexByte(0), row.RequiredString(1));
    }

    private void LoadHeldItems()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_item_holders.tsv",
            new GeneratedTableSchema(
                "Tokay held items",
                GeneratedTableKeySemantics.Unique,
                [
                    "subid", "treasure", "item-graphic", "grant-object",
                    "grant-subid", "grant-parameter", "item-sprite",
                    "item-tile-base", "item-palette", "item-animation"
                ],
                ["subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new TokayHeldItemRecord(
                row.HexByte(0), row.HexByte(1), row.HexByte(2),
                row.RequiredString(3), row.HexByte(4), row.HexByte(5),
                row.RequiredString(6), row.HexByte(7), row.HexByte(8),
                row.RequiredString(9));
            _heldItems.Add(record.SubId, record);
        }
    }

    private void LoadShopPlacements()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_shop_items.tsv",
            new GeneratedTableSchema(
                "Tokay shop items",
                GeneratedTableKeySemantics.Ordered,
                [
                    "order", "placed-subid", "y", "x", "sprite", "tile-base",
                    "palette", "animation"
                ],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new TokayShopPlacementRecord(
                row.Decimal(0), row.HexByte(1), row.HexByte(2),
                row.HexByte(3), row.RequiredString(4), row.HexByte(5),
                row.HexByte(6), row.RequiredString(7));
            _shopVisuals.Add(record.PlacedSubId, record);
            if (record.Order >= 0)
                _shopPlacements.Add(record);
        }
    }

    private void LoadWildPrizes()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_prizes.tsv",
            new GeneratedTableSchema(
                "Wild Tokay prizes",
                GeneratedTableKeySemantics.Unique,
                [
                    "prize-code", "accessory-subid", "sprite", "tile-base",
                    "palette", "animation"
                ],
                ["prize-code"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new WildTokayPrizeRecord(
                row.UnsignedDecimal(0), row.HexByte(1), row.RequiredString(2),
                row.HexByte(3), row.HexByte(4), row.RequiredString(5));
            _wildPrizes.Add(record.PrizeCode, record);
        }
    }

    private void LoadWildStartTiles()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_start_tiles.tsv",
            new GeneratedTableSchema(
                "Wild Tokay start tiles",
                GeneratedTableKeySemantics.Ordered,
                ["order", "tile", "packed-position"],
                headerRequired: true));
        for (int index = 0; index < table.Rows.Count; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            int order = row.UnsignedDecimal(0);
            if (order != index)
                throw row.Invalid(0, $"ordered index {index}");
            _wildStartTiles.Add(new WildTokayStartTileRecord(
                order, row.HexByte(1), row.HexByte(2)));
        }
    }

    private void LoadWildMeatAccessory()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_meat_accessory.tsv",
            new GeneratedTableSchema(
                "Wild Tokay held-meat accessory",
                GeneratedTableKeySemantics.Unique,
                [
                    "parameter", "y-offset", "x-offset", "visible",
                    "animation", "sprite", "tile-base", "palette", "encoded"
                ],
                ["parameter"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new WildTokayMeatAccessoryRecord(
                row.UnsignedDecimal(0), row.Decimal(1), row.Decimal(2),
                row.HexByte(3), row.HexByte(4), row.RequiredString(5),
                row.HexByte(6), row.HexByte(7), row.RequiredString(8));
            _wildMeatAccessory.Add(record.Parameter, record);
        }
    }

    private void LoadWildPatterns()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_patterns.tsv",
            new GeneratedTableSchema(
                "Wild Tokay patterns",
                GeneratedTableKeySemantics.Unique,
                ["level", "random-index", "pattern", "left-count", "right-count"],
                ["level", "random-index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int[] left = Pair(row.RequiredString(3), row, "left-count");
            int[] right = Pair(row.RequiredString(4), row, "right-count");
            _wildPatterns.Add(
                (row.UnsignedDecimal(0), row.UnsignedDecimal(1)),
                new WildTokayPatternRecord(
                    row.UnsignedDecimal(2), left[0], left[1], right[0], right[1]));
        }
    }

    private static int[] Pair(string encoded, GeneratedTableRow row, string field)
    {
        string[] values = encoded.Split(',');
        if (values.Length != 2 ||
            !int.TryParse(values[0], out int first) ||
            !int.TryParse(values[1], out int second))
        {
            throw new InvalidOperationException(
                $"{row.Path}:{row.LineNumber}: invalid {field} pair '{encoded}'.");
        }
        return [first, second];
    }

    private void Validate()
    {
        if (PastGameGroup != 2 || PastGameRoom != 0xde ||
            PresentGameGroup != 2 || PresentGameRoom != 0xe5 ||
            ShopGroup != 2 || ShopRoom != 0xe4 ||
            ShopItemCollisionRadius != 0x06 ||
            ParticipantAnimation != 0x02 || GameReturnPosition != 0x57 ||
            WildCycleCount(0) != 5 || WildCycleCount(1) != 5 ||
            WildCycleCount(2) != 5 || WildCycleCount(3) != 6 ||
            WildCycleCount(4) != 7 ||
            GameStartDelay != 30 ||
            GameFadeInDelay != 10 ||
            MeatFallDelay != 30 || MeatFallGravity != 0x28 ||
            DimitriStateAddress != 0xc647 || WildLevelAddress != 0xc6ea ||
            _texts.Count != 96 || _animations.Count != 10 ||
            _heldItems.Count != 5 || _shopPlacements.Count != 3 ||
            _shopVisuals.Count != 7 || _wildPrizes.Count != 6 ||
            _wildPatterns.Count != 80 || _wildStartTiles.Count != 6 ||
            _wildMeatAccessory.Count != 5 ||
            WildPrize(0).AccessorySubId != 0x3e ||
            WildPrize(4).AccessorySubId != 0x2d ||
            WildPrize(5).AccessorySubId != 0x0e ||
            _wildStartTiles[0] != new WildTokayStartTileRecord(0, 0xef, 0x01) ||
            _wildStartTiles[3] != new WildTokayStartTileRecord(3, 0xef, 0x78) ||
            _wildStartTiles[5] != new WildTokayStartTileRecord(5, 0x7a, 0x75) ||
            WildMeatAccessory(0) is not { YOffset: 0, XOffset: -13 } ||
            WildMeatAccessory(3) is not { YOffset: -12, XOffset: -1 } ||
            WildMeatAccessory(4) is not { YOffset: -12, XOffset: 0 } ||
            HeldItem(0x06).Treasure != TreasureDatabase.TreasureSword ||
            HeldItem(0x06).GrantSubId != 0x06 ||
            HeldItem(0x06).GrantParameter != 0x01 ||
            HeldItem(0x07).GrantObject != "TREASURE_OBJECT_SHOVEL_01" ||
            HeldItem(0x07).GrantSubId != 0x01 ||
            HeldItem(0x07).GrantParameter != 0x00 ||
            HeldItem(0x0a).Treasure != TreasureDatabase.TreasureSeedSatchel ||
            HeldItem(0x0a).ItemAnimation.Length == 0 ||
            !Text(0x0a01).Contains("Stink Bag", StringComparison.Ordinal) ||
            Text(0x0a13).Contains("\\jump", StringComparison.Ordinal) ||
            !Text(0x1c10).Contains("shovel", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Tokay Island generated data does not match the traced source contract.");
        }
    }
}

internal readonly record struct TokayConstant(int Value, string Text);
internal readonly record struct TokayHeldItemRecord(
    int SubId, int Treasure, int ItemGraphic, string GrantObject,
    int GrantSubId, int GrantParameter, string ItemSprite, int ItemTileBase,
    int ItemPalette, string ItemAnimation);
internal readonly record struct WildTokayPrizeRecord(
    int PrizeCode, int AccessorySubId, string Sprite, int TileBase,
    int Palette, string Animation);
internal readonly record struct WildTokayStartTileRecord(
    int Order, int Tile, int PackedPosition);
internal readonly record struct WildTokayMeatAccessoryRecord(
    int Parameter, int YOffset, int XOffset, int Visible, int Animation,
    string Sprite, int TileBase, int Palette, string EncodedAnimation);
internal readonly record struct TokayShopPlacementRecord(
    int Order, int PlacedSubId, int Y, int X, string Sprite,
    int TileBase, int Palette, string Animation);
internal readonly record struct WildTokayPatternRecord(
    int Pattern, int LeftBlue, int LeftRed, int RightBlue, int RightRed);
