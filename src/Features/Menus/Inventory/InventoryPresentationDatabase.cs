using static Oracles.MenuPresentationTable;

namespace Oracles;

internal sealed class InventoryPresentationDatabase
{
    public string AssetRoot { get; }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<InventoryPresentationDatabase>> Packs = new(StringComparer.Ordinal);
    internal static InventoryPresentationDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<InventoryPresentationDatabase>(() => new(root))).Value;

    private readonly Dictionary<string, IReadOnlyList<MenuOamPart>> _inventoryOam =
        new(StringComparer.Ordinal);
    internal IReadOnlyList<MenuOamPart> InventoryMakuSeed => _inventoryOam["maku-seed"];
    internal IReadOnlyList<MenuOamPart> InventoryHarp(int song) => _inventoryOam[$"harp-{song}"];
    internal IReadOnlyList<MenuOamPart> InventorySubmenuMask(int index) => _inventoryOam[$"submenu-mask-{index}"];
    public IReadOnlyList<MenuTilePosition> InventoryItemSlots { get; }
    public IReadOnlyList<InventoryItemSubmenuLayout> InventoryItemSubmenus { get; }
    public IReadOnlyList<InventorySeedSubmenuSprite> InventorySeedSubmenuSprites { get; }
    public IReadOnlyList<PassiveTreasureLayout> PassiveTreasures { get; }
    public IReadOnlyList<SecondaryCursorLayout> SecondaryCursors { get; }
    public IReadOnlyList<MenuTilePosition> EssenceTiles { get; }
    public IReadOnlyList<EssenceCursorLayout> EssenceCursors { get; }
    internal IReadOnlyList<InventoryGraphicsUpload> Graphics { get; }
    internal IReadOnlyList<InventoryBlurbLayout> Blurbs { get; }
    internal byte[] EssenceTextKeys { get; } = new byte[11];
    internal Color?[,] SpriteColors { get; } = new Color?[8, 4];

    private InventoryPresentationDatabase(string assetRoot)
    {
        _ = OracleAssetPack.GameFor(assetRoot);
        AssetRoot = assetRoot;
        InventoryItemSlots = LoadTilePositions(
            AssetRoot + "menu/inventory_item_slots.tsv",
            "inventory item slots", 16);
        InventoryItemSubmenus = LoadInventoryItemSubmenus();
        InventorySeedSubmenuSprites = LoadInventorySeedSubmenuSprites();
        PassiveTreasures = LoadPassiveTreasures();
        SecondaryCursors = LoadSecondaryCursors();
        EssenceTiles = LoadTilePositions(
            AssetRoot + "menu/inventory_essence_tiles.tsv",
            "inventory essence tiles", 8);
        EssenceCursors = LoadEssenceCursors();
        Graphics = LoadGraphics();
        Blurbs = LoadBlurbs();
        LoadTextKeys();
        LoadSpriteColors();
        var oamCounts = new Dictionary<string, int>(StringComparer.Ordinal) { ["maku-seed"] = 4 };
        // bank2.s only assembles the Harp and submenu masking sprites for Ages.
        if (OracleAssetPack.GameFor(assetRoot) == OracleGame.Ages)
        {
            oamCounts.Add("harp-0", 1);
            for (int song = 1; song < 4; song++) oamCounts.Add($"harp-{song}", 2);
            oamCounts.Add("submenu-mask-0", 8);
            oamCounts.Add("submenu-mask-1", 2);
            oamCounts.Add("submenu-mask-2", 6);
        }
        LoadOamLayouts(AssetRoot + "menu/inventory_oam.tsv", "inventory OAM", _inventoryOam, oamCounts);
    }

    public InventoryItemSubmenuLayout InventoryItemSubmenu(int optionCount) =>
        optionCount is >= 2 and <= 5
            ? InventoryItemSubmenus[optionCount - 2]
            : throw new ArgumentOutOfRangeException(nameof(optionCount));

    private IReadOnlyList<InventoryGraphicsUpload> LoadGraphics()
    {
        var table = GeneratedTable.Load(AssetRoot + "menu/inventory_graphics.tsv",
            new("inventory graphics uploads", GeneratedTableKeySemantics.Ordered,
                ["index", "bank", "tile", "asset", "interleaved", "inverted", "source"], headerRequired: true));
        var result = new List<InventoryGraphicsUpload>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            RequireNextIndex(row, row.UnsignedDecimal(0), result.Count);
            result.Add(new(row.Decimal(1, 0, 1), row.HexByte(2), AssetRoot + row.RequiredString(3),
                row.Boolean01(4), row.Boolean01(5), row.RequiredString(6)));
        }
        RequireCount(table, result.Count, 16);
        return result.AsReadOnly();
    }

    private IReadOnlyList<InventoryBlurbLayout> LoadBlurbs()
    {
        var table = GeneratedTable.Load(AssetRoot + "menu/inventory_blurbs.tsv",
            new("inventory time/season blurbs", GeneratedTableKeySemantics.Ordered,
                ["index", "sprite-0", "flags-0", "sprite-1", "flags-1", "sprite-2", "flags-2", "sprite-3", "flags-3", "source"], headerRequired: true));
        var result = new List<InventoryBlurbLayout>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            RequireNextIndex(row, row.UnsignedDecimal(0), result.Count);
            (int Sprite, int Flags)[] sprites = [(row.HexByte(1), row.HexByte(2)), (row.HexByte(3), row.HexByte(4)),
                (row.HexByte(5), row.HexByte(6)), (row.HexByte(7), row.HexByte(8))];
            result.Add(new(Array.AsReadOnly(sprites), row.RequiredString(9)));
        }
        RequireCount(table, result.Count, OracleAssetPack.GameFor(AssetRoot) == OracleGame.Ages ? 2 : 4);
        return result.AsReadOnly();
    }

    private void LoadTextKeys()
    {
        var table = GeneratedTable.Load(AssetRoot + "menu/inventory_essence_texts.tsv",
            new("inventory essence-page text indices", GeneratedTableKeySemantics.Ordered,
                ["index", "text-low", "source"], headerRequired: true));
        RequireCount(table, table.Rows.Count, 11);
        for (int index = 0; index < 11; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            RequireNextIndex(row, row.UnsignedDecimal(0), index);
            EssenceTextKeys[index] = (byte)row.HexByte(1);
            _ = row.RequiredString(2);
        }
    }

    private void LoadSpriteColors()
    {
        var table = GeneratedTable.Load(AssetRoot + "menu/inventory_sprite_palettes.tsv",
            new("PALH_0a sprite writes", GeneratedTableKeySemantics.Unique,
                ["palette", "shade", "red", "green", "blue", "source"], ["palette", "shade"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int palette = row.Decimal(0, 0, 7), shade = row.Decimal(1, 0, 3);
            SpriteColors[palette, shade] = new Color(row.Decimal(2, 0, 31) / 31.0f,
                row.Decimal(3, 0, 31) / 31.0f, row.Decimal(4, 0, 31) / 31.0f);
            _ = row.RequiredString(5);
        }
        RequireCount(table, table.Rows.Count, OracleAssetPack.GameFor(AssetRoot) == OracleGame.Ages ? 24 : 28);
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
            if (SpriteColors[palette, shade].HasValue != (palette < 6 || palette == 7 && OracleAssetPack.GameFor(AssetRoot) == OracleGame.Seasons))
                throw new InvalidOperationException($"{table.Path}: PALH_0a must retain unaddressed sprite slots and fully replace every addressed slot.");
    }

    private static IReadOnlyList<MenuTilePosition> LoadTilePositions(
        string path,
        string name,
        int expectedCount)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                name,
                GeneratedTableKeySemantics.Unique,
                ["index", "tilemap-offset", "source-label", "source"],
                ["index"],
                headerRequired: true));
        var result = new List<MenuTilePosition>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            RequireNextIndex(row, index, result.Count);
            result.Add(new MenuTilePosition(
                index, row.HexInt(1), row.RequiredString(2),
                row.RequiredString(3)));
        }
        RequireCount(table, result.Count, expectedCount);
        return result.AsReadOnly();
    }

    private IReadOnlyList<PassiveTreasureLayout> LoadPassiveTreasures()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/inventory_passive_treasures.tsv",
            new GeneratedTableSchema(
                "inventory passive treasures",
                GeneratedTableKeySemantics.Unique,
                [
                    "index", "treasure", "treasure-id", "position", "slot",
                    "source-label", "source"
                ],
                ["index"],
                headerRequired: true));
        var result = new List<PassiveTreasureLayout>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            RequireNextIndex(row, index, result.Count);
            result.Add(new PassiveTreasureLayout(
                index, row.RequiredString(1), row.HexByte(2), row.HexByte(3),
                row.Decimal(4, 0, 14), row.RequiredString(5),
                row.RequiredString(6)));
        }
        RequireCount(table, result.Count, OracleAssetPack.GameFor(AssetRoot) == OracleGame.Ages ? 32 : 27);
        return result.AsReadOnly();
    }

    private IReadOnlyList<InventoryItemSubmenuLayout>
        LoadInventoryItemSubmenus()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/inventory_item_submenu_layout.tsv",
            new GeneratedTableSchema(
                "inventory item submenu layouts",
                GeneratedTableKeySemantics.Unique,
                [
                    "option-count", "index", "max-width", "x-nibble",
                    "source-label", "source"
                ],
                ["option-count", "index"],
                headerRequired: true));
        var result = new List<InventoryItemSubmenuLayout>(4);
        int rowIndex = 0;
        for (int optionCount = 2; optionCount <= 5; optionCount++)
        {
            var positions = new List<InventoryItemSubmenuPosition>(optionCount);
            int maxWidth = -1;
            for (int index = 0; index < optionCount; index++)
            {
                if (rowIndex >= table.Rows.Count)
                {
                    throw new InvalidOperationException(
                        $"{table.Path}: missing option {index} for " +
                        $"{optionCount}-option inventory submenu.");
                }
                GeneratedTableRow row = table.Rows[rowIndex++];
                if (row.Decimal(0, 2, 5) != optionCount)
                    throw row.Invalid(0, $"ordered option count {optionCount}");
                RequireNextIndex(row, row.UnsignedDecimal(1), index);
                int rowWidth = row.HexByte(2);
                if (maxWidth < 0)
                    maxWidth = rowWidth;
                else if (rowWidth != maxWidth)
                    throw row.Invalid(2, $"shared width ${maxWidth:x2}");
                positions.Add(new InventoryItemSubmenuPosition(
                    index, row.HexByte(3), row.RequiredString(4),
                    row.RequiredString(5)));
            }
            result.Add(new InventoryItemSubmenuLayout(
                optionCount, maxWidth, positions.AsReadOnly()));
        }
        RequireCount(table, rowIndex, 14);
        return result.AsReadOnly();
    }

    private IReadOnlyList<InventorySeedSubmenuSprite>
        LoadInventorySeedSubmenuSprites()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/inventory_seed_submenu_oam.tsv",
            new GeneratedTableSchema(
                "inventory seed submenu OAM",
                GeneratedTableKeySemantics.Unique,
                [
                    "seed-type", "y-offset", "x-offset", "tile",
                    "attributes", "source-label", "source"
                ],
                ["seed-type"],
                headerRequired: true));
        var result = new List<InventorySeedSubmenuSprite>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int seedType = row.UnsignedDecimal(0);
            RequireNextIndex(row, seedType, result.Count);
            result.Add(new InventorySeedSubmenuSprite(
                seedType, row.HexByte(1), row.HexByte(2), row.HexByte(3),
                row.HexByte(4), row.RequiredString(5),
                row.RequiredString(6)));
        }
        RequireCount(table, result.Count, 5);
        return result.AsReadOnly();
    }

    private IReadOnlyList<SecondaryCursorLayout>
        LoadSecondaryCursors()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/inventory_secondary_cursors.tsv",
            new GeneratedTableSchema(
                "inventory secondary cursors",
                GeneratedTableKeySemantics.Unique,
                ["index", "packed", "source-label", "source"],
                ["index"],
                headerRequired: true));
        var result = new List<SecondaryCursorLayout>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            RequireNextIndex(row, index, result.Count);
            result.Add(new SecondaryCursorLayout(
                index, row.HexByte(1), row.RequiredString(2),
                row.RequiredString(3)));
        }
        RequireCount(table, result.Count, 21);
        return result.AsReadOnly();
    }

    private IReadOnlyList<EssenceCursorLayout> LoadEssenceCursors()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/inventory_essence_cursors.tsv",
            new GeneratedTableSchema(
                "inventory essence cursors",
                GeneratedTableKeySemantics.Unique,
                ["index", "raw-y", "raw-x", "source-label", "source"],
                ["index"],
                headerRequired: true));
        var result = new List<EssenceCursorLayout>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            RequireNextIndex(row, index, result.Count);
            result.Add(new EssenceCursorLayout(
                index, row.HexByte(1), row.HexByte(2),
                row.RequiredString(3), row.RequiredString(4)));
        }
        RequireCount(table, result.Count, 11);
        return result.AsReadOnly();
    }

}

internal readonly record struct MenuTilePosition(
    int Index,
    int TilemapOffset,
    string SourceLabel,
    string Source);

internal sealed record InventoryItemSubmenuLayout(
    int OptionCount,
    int MaxWidth,
    IReadOnlyList<InventoryItemSubmenuPosition> Positions);

internal readonly record struct InventoryItemSubmenuPosition(
    int Index,
    int RawX,
    string SourceLabel,
    string Source);

internal readonly record struct InventorySeedSubmenuSprite(
    int SeedType,
    int Y,
    int X,
    int Tile,
    int Attributes,
    string SourceLabel,
    string Source)
{
    public int VramBank => (Attributes >> 3) & 1;
}

internal readonly record struct PassiveTreasureLayout(
    int Index,
    string Treasure,
    int TreasureId,
    int Position,
    int Slot,
    string SourceLabel,
    string Source);

internal readonly record struct SecondaryCursorLayout(
    int Index,
    int Packed,
    string SourceLabel,
    string Source);

internal readonly record struct EssenceCursorLayout(
    int Index,
    int RawY,
    int RawX,
    string SourceLabel,
    string Source);

internal readonly record struct InventoryGraphicsUpload(int Bank, int Tile, string Asset,
    bool Interleaved, bool Inverted, string Source);

internal readonly record struct InventoryBlurbLayout(IReadOnlyList<(int Sprite, int Flags)> Sprites, string Source);
