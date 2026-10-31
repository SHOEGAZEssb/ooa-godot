using System;
using System.Collections.Generic;
using static Oracles.MenuPresentationTable;

namespace Oracles;

internal sealed class MapPresentationDatabase
{
    public string AssetRoot { get; }
    private readonly OracleGame _game;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<MapPresentationDatabase>> Packs = new(StringComparer.Ordinal);
    internal static MapPresentationDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<MapPresentationDatabase>(() => new(root))).Value;

    private readonly Dictionary<string, IReadOnlyList<MenuOamPart>> _mapOam =
        new(StringComparer.Ordinal);
    internal IReadOnlyList<MenuOamPart> MapOam(string layout) => _mapOam[layout];
    internal byte[] DungeonSymbols { get; }
    internal byte[] DungeonFloorNames { get; }
    public IReadOnlyList<MapIconLayout> MapIcons { get; }
    public IReadOnlyList<DungeonFloorListLayout> DungeonFloorLists { get; }
    public IReadOnlyList<DungeonBlurbLayout> DungeonBlurbs { get; }

    private MapPresentationDatabase(string assetRoot)
    {
        _game = OracleAssetPack.GameFor(assetRoot);
        AssetRoot = assetRoot;
        DungeonSymbols = OracleGraphicsData.ReadBytes(
        AssetRoot + "menu/dungeon_symbols.bin", _game == OracleGame.Ages ? 28 : 24);
        DungeonFloorNames = OracleGraphicsData.ReadBytes(
        AssetRoot + "menu/dungeon_floor_names.bin", 22);

        MapIcons = LoadMapIcons();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["arrow"] = 1, ["cursor"] = 2, ["warp"] = 1,
                ["floor-cursor"] = 1, ["boss-floor"] = 1, ["link-map"] = 1,
                ["link-floor"] = 1, ["dungeon-cursor"] = 2, ["up"] = 1, ["down"] = 1,
                ["map"] = 2, ["compass"] = 2, ["boss-key"] = 2, ["small-key"] = 1,
                ["border1"] = 1, ["border2"] = 2, ["border3"] = 8, ["border4"] = 8
            };
        counts[_game == OracleGame.Ages ? "portal" : "jewel"] = 1;
        LoadOamLayouts(AssetRoot + "menu/map_oam.tsv", "map OAM", _mapOam, counts);
        DungeonFloorLists = LoadDungeonFloorLists();
        DungeonBlurbs = LoadDungeonBlurbs();
    }

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

    private IReadOnlyList<MapIconLayout> LoadMapIcons()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/map_icons.tsv",
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
        if (_game == OracleGame.Ages && result[20].AliasOf != result[0].Label)
        {
            throw new InvalidOperationException(
                "Map icon $14 must retain its empty @mapIcon00 alias.");
        }
        return result.AsReadOnly();
    }

    private IReadOnlyList<DungeonFloorListLayout>
        LoadDungeonFloorLists()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/dungeon_floor_list.tsv",
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
        RequireCount(table, result.Count, _game == OracleGame.Ages ? 14 : 12);
        return result.AsReadOnly();
    }

    private IReadOnlyList<DungeonBlurbLayout> LoadDungeonBlurbs()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "menu/dungeon_blurbs.tsv",
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
        RequireCount(table, result.Count, _game == OracleGame.Ages ? 16 : 12);
        return result.AsReadOnly();
    }

}

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
