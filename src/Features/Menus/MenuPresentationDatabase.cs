using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-ordered presentation tables shared by the map, inventory, file, and
/// ring menus. Procedural menu state remains in the owning screen/controller.
/// </summary>
internal sealed class MenuPresentationDatabase
{
    private static readonly Lazy<MenuPresentationDatabase> LazyShared =
        new(static () => new MenuPresentationDatabase());

    private readonly Dictionary<string, IReadOnlyList<MenuOamPart>> _fileOam =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<MenuOamPart>> _ringOam =
        new(StringComparer.Ordinal);

    public static MenuPresentationDatabase Shared => LazyShared.Value;

    public IReadOnlyList<MapIconLayout> MapIcons { get; }
    public IReadOnlyList<DungeonFloorListLayout> DungeonFloorLists { get; }
    public IReadOnlyList<DungeonBlurbLayout> DungeonBlurbs { get; }
    public IReadOnlyList<MenuTilePosition> InventoryItemSlots { get; }
    public IReadOnlyList<PassiveTreasureLayout> PassiveTreasures { get; }
    public IReadOnlyList<SecondaryCursorLayout> SecondaryCursors { get; }
    public IReadOnlyList<MenuTilePosition> EssenceTiles { get; }
    public IReadOnlyList<EssenceCursorLayout> EssenceCursors { get; }
    public IReadOnlyList<RingBoxOamOffset> RingBoxOffsets { get; }

    public MenuPresentationDatabase()
    {
        MapIcons = LoadMapIcons();
        DungeonFloorLists = LoadDungeonFloorLists();
        DungeonBlurbs = LoadDungeonBlurbs();
        InventoryItemSlots = LoadTilePositions(
            "res://assets/oracle/menu/inventory_item_slots.tsv",
            "inventory item slots", 16);
        PassiveTreasures = LoadPassiveTreasures();
        SecondaryCursors = LoadSecondaryCursors();
        EssenceTiles = LoadTilePositions(
            "res://assets/oracle/menu/inventory_essence_tiles.tsv",
            "inventory essence tiles", 8);
        EssenceCursors = LoadEssenceCursors();
        LoadOamLayouts(
            "res://assets/oracle/menu/file_oam.tsv",
            "file/save menu OAM", _fileOam,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["decorations"] = 16,
                ["acorn-cursor"] = 1,
                ["text-speed-cursor"] = 1,
                ["name-character-cursor"] = 2,
                ["name-lower-option-cursor"] = 2,
                ["name-entry-cursor"] = 1,
                ["save-quit-acorn"] = 1
            });
        LoadOamLayouts(
            "res://assets/oracle/menu/ring_oam.tsv",
            "ring menu OAM", _ringOam,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["list-cursor"] = 1,
                ["page-arrows"] = 2,
                ["equipped-marker"] = 1,
                ["box-cursor"] = 1,
                ["list-box-marker"] = 1
            });
        RingBoxOffsets = LoadRingBoxOffsets();
    }

    public IReadOnlyList<MenuOamPart> FileOam(string layout) =>
        _fileOam.TryGetValue(layout, out IReadOnlyList<MenuOamPart>? parts)
            ? parts
            : throw new InvalidOperationException(
                $"Imported file/save OAM layout '{layout}' does not exist.");

    public IReadOnlyList<MenuOamPart> RingOam(string layout) =>
        _ringOam.TryGetValue(layout, out IReadOnlyList<MenuOamPart>? parts)
            ? parts
            : throw new InvalidOperationException(
                $"Imported ring OAM layout '{layout}' does not exist.");

    public int DungeonFloorListOffset(int dungeon) =>
        dungeon >= 0 && dungeon < DungeonFloorLists.Count
            ? DungeonFloorLists[dungeon].Offset
            : 0x80;

    public DungeonBlurbLayout DungeonBlurb(int dungeon)
    {
        if (dungeon < 0 || dungeon >= DungeonBlurbs.Count)
            throw new ArgumentOutOfRangeException(nameof(dungeon));
        return DungeonBlurbs[dungeon];
    }

    private static IReadOnlyList<MapIconLayout> LoadMapIcons()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/menu/map_icons.tsv",
            new GeneratedTableSchema(
                "map icon OAM",
                GeneratedTableKeySemantics.Unique,
                [
                    "index", "label", "alias-of", "sprite-count",
                    "left-y", "left-x", "left-tile", "left-attributes",
                    "right-y", "right-x", "right-tile", "right-attributes",
                    "source"
                ],
                ["index"],
                headerRequired: true));
        var result = new List<MapIconLayout>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            RequireNextIndex(row, index, result.Count);
            int count = row.Decimal(3, 0, 2);
            if (count is not (0 or 2))
                throw row.Invalid(3, "map-icon sprite count 0 or 2");
            var left = new MenuOamPart(
                0, row.HexByte(4), row.HexByte(5), row.HexByte(6),
                row.HexByte(7), row.RequiredString(1), row.String(2),
                row.RequiredString(12));
            var right = new MenuOamPart(
                1, row.HexByte(8), row.HexByte(9), row.HexByte(10),
                row.HexByte(11), row.RequiredString(1), row.String(2),
                row.RequiredString(12));
            if (count == 0 &&
                (left.Y | left.X | left.Tile | left.Attributes |
                    right.Y | right.X | right.Tile | right.Attributes) != 0)
            {
                throw new InvalidOperationException(
                    $"{row.Path}:{row.LineNumber}: empty map icon {index} " +
                    "contains nonzero OAM bytes.");
            }
            result.Add(new MapIconLayout(
                index, row.RequiredString(1), row.String(2), count,
                left, right, row.RequiredString(12)));
        }
        RequireCount(table, result.Count, 26);
        if (result[20].AliasOf != result[0].Label)
        {
            throw new InvalidOperationException(
                "Map icon $14 must retain its empty @mapIcon00 alias.");
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<DungeonFloorListLayout>
        LoadDungeonFloorLists()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/menu/dungeon_floor_list.tsv",
            new GeneratedTableSchema(
                "dungeon floor-list positions",
                GeneratedTableKeySemantics.Unique,
                ["dungeon", "offset", "source-label", "source"],
                ["dungeon"],
                headerRequired: true));
        var result = new List<DungeonFloorListLayout>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int dungeon = row.UnsignedDecimal(0);
            RequireNextIndex(row, dungeon, result.Count);
            result.Add(new DungeonFloorListLayout(
                dungeon, row.HexByte(1), row.RequiredString(2),
                row.RequiredString(3)));
        }
        RequireCount(table, result.Count, 14);
        return result.AsReadOnly();
    }

    private static IReadOnlyList<DungeonBlurbLayout> LoadDungeonBlurbs()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/menu/dungeon_blurbs.tsv",
            new GeneratedTableSchema(
                "dungeon blurb selectors",
                GeneratedTableKeySemantics.Unique,
                [
                    "dungeon", "gfx-header", "graphic", "asset",
                    "alias-of", "source"
                ],
                ["dungeon"],
                headerRequired: true));
        var result = new List<DungeonBlurbLayout>(table.Rows.Count);
        var graphicsByHeader = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int dungeon = row.UnsignedDecimal(0);
            RequireNextIndex(row, dungeon, result.Count);
            string header = row.RequiredString(1);
            string graphic = row.RequiredString(2);
            string alias = row.String(4);
            if (alias.Length != 0 &&
                (!graphicsByHeader.TryGetValue(alias, out string? aliasedGraphic) ||
                    aliasedGraphic != graphic))
            {
                throw new InvalidOperationException(
                    $"{row.Path}:{row.LineNumber}: blurb alias '{alias}' " +
                    $"does not precede and select '{graphic}'.");
            }
            graphicsByHeader.Add(header, graphic);
            result.Add(new DungeonBlurbLayout(
                dungeon, header, graphic, row.RequiredString(3), alias,
                row.RequiredString(5)));
        }
        RequireCount(table, result.Count, 16);
        return result.AsReadOnly();
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

    private static IReadOnlyList<PassiveTreasureLayout> LoadPassiveTreasures()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/menu/inventory_passive_treasures.tsv",
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
        RequireCount(table, result.Count, 32);
        return result.AsReadOnly();
    }

    private static IReadOnlyList<SecondaryCursorLayout>
        LoadSecondaryCursors()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/menu/inventory_secondary_cursors.tsv",
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

    private static IReadOnlyList<EssenceCursorLayout> LoadEssenceCursors()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/menu/inventory_essence_cursors.tsv",
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

    private static void LoadOamLayouts(
        string path,
        string name,
        Dictionary<string, IReadOnlyList<MenuOamPart>> destination,
        IReadOnlyDictionary<string, int> expectedCounts)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                name,
                GeneratedTableKeySemantics.Unique,
                [
                    "layout", "part", "y", "x", "tile", "attributes",
                    "source-label", "alias-of", "source"
                ],
                ["layout", "part"],
                headerRequired: true));
        var grouped = new Dictionary<string, List<MenuOamPart>>(
            StringComparer.Ordinal);
        foreach (GeneratedTableRow row in table.Rows)
        {
            string layout = row.RequiredString(0);
            if (!expectedCounts.ContainsKey(layout))
                throw row.Invalid(0, $"one of {string.Join(", ", expectedCounts.Keys)}");
            if (!grouped.TryGetValue(layout, out List<MenuOamPart>? parts))
            {
                parts = new List<MenuOamPart>();
                grouped.Add(layout, parts);
            }
            int part = row.UnsignedDecimal(1);
            RequireNextIndex(row, part, parts.Count);
            parts.Add(new MenuOamPart(
                part, row.HexByte(2), row.HexByte(3), row.HexByte(4),
                row.HexByte(5), row.RequiredString(6), row.String(7),
                row.RequiredString(8)));
        }
        foreach ((string layout, int count) in expectedCounts)
        {
            if (!grouped.TryGetValue(layout, out List<MenuOamPart>? parts) ||
                parts.Count != count)
            {
                throw new InvalidOperationException(
                    $"{path}: OAM layout '{layout}' expected {count} parts, got " +
                    $"{(parts is null ? 0 : parts.Count)}.");
            }
            destination.Add(layout, parts.AsReadOnly());
        }
    }

    private static IReadOnlyList<RingBoxOamOffset> LoadRingBoxOffsets()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/menu/ring_box_oam_offsets.tsv",
            new GeneratedTableSchema(
                "ring-box OAM offsets",
                GeneratedTableKeySemantics.Unique,
                ["slot", "x-offset", "source-label", "source"],
                ["slot"],
                headerRequired: true));
        var result = new List<RingBoxOamOffset>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            int slot = row.UnsignedDecimal(0);
            RequireNextIndex(row, slot, result.Count);
            result.Add(new RingBoxOamOffset(
                slot, row.HexByte(1), row.RequiredString(2),
                row.RequiredString(3)));
        }
        RequireCount(table, result.Count, 5);
        return result.AsReadOnly();
    }

    private static void RequireNextIndex(
        GeneratedTableRow row,
        int actual,
        int expected)
    {
        if (actual != expected)
            throw row.Invalid(0, $"ordered index {expected}");
    }

    private static void RequireCount(
        GeneratedTable table,
        int actual,
        int expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"{table.Path}: schema '{table.Schema.Name}' expected " +
                $"{expected} ordered records, got {actual}.");
        }
    }
}

internal readonly record struct MenuOamPart(
    int Part,
    int Y,
    int X,
    int Tile,
    int Attributes,
    string SourceLabel,
    string AliasOf,
    string Source);

internal readonly record struct MapIconLayout(
    int Index,
    string Label,
    string AliasOf,
    int SpriteCount,
    MenuOamPart Left,
    MenuOamPart Right,
    string Source);

internal readonly record struct DungeonFloorListLayout(
    int Dungeon,
    int Offset,
    string SourceLabel,
    string Source);

internal readonly record struct DungeonBlurbLayout(
    int Dungeon,
    string GfxHeader,
    string Graphic,
    string Asset,
    string AliasOf,
    string Source);

internal readonly record struct MenuTilePosition(
    int Index,
    int TilemapOffset,
    string SourceLabel,
    string Source);

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

internal readonly record struct RingBoxOamOffset(
    int Slot,
    int XOffset,
    string SourceLabel,
    string Source);
