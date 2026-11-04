
namespace Oracles;

/// <summary>
/// Imported MENU_MAP lookup data: native world grids, area text, cursor popup types,
/// tree icons, dungeon entrance fallbacks, and resolved TX strings.
/// </summary>
public sealed class MapDataDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<MapDataDatabase>> Packs = new(StringComparer.Ordinal);
    internal static MapDataDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<MapDataDatabase>(() => new MapDataDatabase(root))).Value;

    private readonly Dictionary<(int Group, int Room), MapCell> _cells = new();
    private readonly Dictionary<int, MapText> _texts = new();
    private readonly Dictionary<(int Group, int Room), int> _treePopups = new();
    private readonly Dictionary<int, DungeonEntrance> _dungeonEntrances = new();
    private readonly MapGeometry[] _geometry = new MapGeometry[2];
    private readonly Dictionary<int, List<MapSubstitution>> _substitutions = new();
    private readonly byte[] _gashaRooms;
    private readonly GlobalFlagDatabase _flags;
    private readonly byte[] _jewelLocations;
    internal OracleGame Game { get; }
    internal IEnumerable<int> JewelRooms(OracleSaveData save)
    {
        // bank2.s:mapMenu_drawJewelLocations; native Seasons treasure IDs.
        if (Game != OracleGame.Seasons || !save.HasTreasure(SeasonsTreasureId.TreasureMap)) yield break;
        for (int jewel = 0; jewel < 4; jewel++)
            if (!save.HasTreasure(SeasonsTreasureId.RoundJewel + jewel) && (save.ReadField("wInsertedJewels") & (1 << jewel)) == 0)
                yield return _jewelLocations[jewel + (save.IsLinkedGame ? 4 : 0)];
    }
    internal MapGeometry Geometry(int group) => _geometry[group];
    internal (MapMode Mode, int Room) DisplayLocation(RoomSession rooms)
    {
        int room = rooms.MinimapRoom;
        if (Game == OracleGame.Ages)
        {
            int flags = rooms.CurrentRoom.TilesetFlags;
            if ((flags & 0x18) == 8) return (MapMode.Dungeon, room);
            if ((flags & (int)TilesetFlags.Maku) != 0) room = 0x38;
            return ((flags & (int)TilesetFlags.Past) != 0 ? MapMode.Past : MapMode.Present, room);
        }
        int group = rooms.MinimapGroup;
        // bank2.s:loadMinimapDisplayRoom's native Seasons display redirects.
        if (group == 0 && room == 0xc9) room = 0x40;
        else if (group == 2) { group = 0; room = 0xc9; }
        else if (group == 4 && rooms.ActiveGroup == 3 && rooms.CurrentRoom.Id == 0x98) { group = 0; room = 0; }
        return (group >= 4 ? MapMode.Dungeon : group == 1 ? MapMode.Past : MapMode.Present, room);
    }
    internal bool GashaSpotPlanted(OracleSaveData save, int room)
    {
        int index = Array.IndexOf(_gashaRooms, checked((byte)room));
        return index >= 0 && save.IsGashaSpotPlanted(index);
    }
    internal bool MoblinsKeepDestroyed(OracleSaveData save) =>
        save.HasGlobalFlag(_flags.GetId("GLOBALFLAG_MOBLINS_KEEP_DESTROYED"));
    internal bool PirateShipMoved(OracleSaveData save) =>
        Game == OracleGame.Seasons && save.HasGlobalFlag(_flags.GetId("GLOBALFLAG_PIRATE_SHIP_DOCKED"));
    internal bool AdvanceShopVisited(RoomSession rooms) => rooms.HasVisited(1, Game == OracleGame.Ages ? 0xfe : 0xaf);

    public MapDataDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        Game = OracleAssetPack.GameFor(assetRoot);
        _jewelLocations = Game == OracleGame.Seasons ? OracleGraphicsData.ReadBytes(assetRoot + "map/jewel_locations.bin", 8) : [];
        _flags = new(assetRoot);
        _gashaRooms = OracleGraphicsData.ReadBytes(assetRoot + "map/gasha_rooms.bin", 16);
        LoadGeometry();
        LoadCells();
        LoadTexts();
        LoadTreeWarps();
        LoadDungeonEntrances();
        LoadSubstitutions();
    }

    public int GetPopupByte(int group, int room) =>
        _cells.TryGetValue((group, room), out MapCell cell) ? cell.Popup : 0;

    internal MapText GetText(int id) => _texts[id];

    public int GetTreePopup(int group, int room) =>
        _treePopups.TryGetValue((group, room), out int popup) ? popup : 0;

    public bool TryResolveAreaText(
        RoomSession rooms,
        MapPresentationState presentation,
        int group,
        int room,
        out MapText text)
    {
        text = default;
        if (rooms.AssetRoot != AssetRoot || presentation.Game != Game)
            throw new ArgumentException("Map lookup, room session and presentation state must belong to the same game.");
        if (!_cells.TryGetValue((group, room), out MapCell cell))
            return false;

        int index = cell.Text;
        int textId;
        if ((index & 0x80) == 0)
        {
            textId = 0x0300 | index;
        }
        else
        {
            textId = ResolveSpecialTextId(rooms, presentation, group, index);
        }
        return _texts.TryGetValue(textId, out text);
    }

    private int ResolveSpecialTextId(
        RoomSession rooms,
        MapPresentationState presentation,
        int group,
        int index)
    {
        OracleSaveData save = rooms.SaveData;
        switch (index & 0x07)
        {
            case 0: // Maku Tree advice.
                return presentation.ResolveMakuAdviceTextId(group);

            case 1: // Dungeon entrance name after entering it once.
                int dungeon = (index >> 3) & 0x0f;
                if (!_dungeonEntrances.TryGetValue(dungeon, out DungeonEntrance entrance))
                    throw new NotSupportedException($"{AssetRoot} code/bank2.s:mapMenu_dungeonEntranceText: unsupported selector ${dungeon:x2}.");
                return rooms.HasVisited(entrance.Group, entrance.Room)
                    ? 0x0200 | dungeon
                    : 0x0300 | entrance.FallbackText;

            case 2: // Moblin's Keep / ruins.
                return (Game == OracleGame.Ages ? 0x0317 : 0x030b) + (MoblinsKeepDestroyed(save) ? 0 : 1);

            case 3: // Animal companion region.
                return presentation.AnimalCompanionAreaTextId;

            case 4: // Advance Shop text changes after it is visited.
                return Game == OracleGame.Ages
                    ? AdvanceShopVisited(rooms) ? 0x0325 : 0x0326
                    : AdvanceShopVisited(rooms) ? 0x032c : 0x0321;

            default:
                throw new NotSupportedException($"{AssetRoot} code/bank2.s:mapGetRoomText: unsupported special selector ${index:x2}.");
        }
    }

    private void LoadCells()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "map/cells.tsv",
            new GeneratedTableSchema(
                "overworld map cells",
                GeneratedTableKeySemantics.Unique,
                ["group", "room", "text", "popup", "source"],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int group = row.Decimal(0, 0, 1), room = row.HexByte(1);
            MapGeometry geometry = _geometry[group];
            if ((room & 15) >= geometry.Width || (room >> 4) >= geometry.Height)
                throw row.Invalid(1, "room within the native map dimensions");
            _cells.Add((group, room), new MapCell(row.HexByte(2), row.HexByte(3)));
        }
        int expected = _geometry[0].Width * _geometry[0].Height + _geometry[1].Width * _geometry[1].Height;
        if (_cells.Count != expected)
            throw new InvalidOperationException($"{AssetRoot}: expected {expected} native map cells, got {_cells.Count}.");
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "map/texts.tsv",
            new GeneratedTableSchema(
                "map text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "position", "message-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int textId = row.HexWord(0);
            _texts.Add(textId, new MapText(textId, row.Base64Utf8(2), row.UnsignedDecimal(1)));
        }
    }

    private void LoadTreeWarps()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "map/tree_warps.tsv",
            new GeneratedTableSchema(
                "map tree warps",
                GeneratedTableKeySemantics.Unique,
                ["group", "room", "popup"],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _treePopups.Add(
                (row.Decimal(0, 0, 7), row.HexByte(1)), row.HexByte(2));
        }
    }

    private void LoadDungeonEntrances()
    {
        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "map/dungeon_entrances.tsv",
            new GeneratedTableSchema(
                "map dungeon entrances",
                GeneratedTableKeySemantics.Unique,
                ["dungeon", "group", "room", "fallback-text"],
                ["dungeon"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _dungeonEntrances.Add(
                row.UnsignedDecimal(0),
                new DungeonEntrance(
                    row.Decimal(1, 0, 7), row.HexByte(2), row.HexByte(3)));
        }
        int expected = Game == OracleGame.Ages ? 16 : 10;
        if (_dungeonEntrances.Count != expected)
            throw new InvalidOperationException(
                $"{AssetRoot}: expected {expected} dungeon entrance text records, got {_dungeonEntrances.Count}.");
    }

    private void LoadGeometry()
    {
        GeneratedTable table = GeneratedTable.Load(AssetRoot + "map/geometry.tsv", new GeneratedTableSchema(
            "map geometry", GeneratedTableKeySemantics.Unique,
            ["group", "width", "height", "start-x", "start-y", "popup-shift-x", "popup-shift-y", "source"], ["group"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _geometry[row.Decimal(0, 0, 1)] = new(row.Decimal(1, 1, 16), row.Decimal(2, 1, 16),
                row.Decimal(3, 0, 19), row.Decimal(4, 0, 17), row.Decimal(5, 1, 16), row.Decimal(6, 1, 16));
        if (table.Rows.Count != 2) throw new InvalidOperationException($"{table.Path}: expected two native world maps.");
        foreach (MapGeometry geometry in _geometry)
            if (geometry.StartX + geometry.Width > 20 || geometry.StartY + geometry.Height > 18)
                throw new InvalidOperationException($"{table.Path}: native map grid extends beyond the viewport.");
    }

    private void LoadSubstitutions()
    {
        GeneratedTable table = GeneratedTable.Load(AssetRoot + "map/substitutions.tsv", new GeneratedTableSchema(
            "map substitutions", GeneratedTableKeySemantics.Unique,
            ["selector", "order", "height", "width", "destination", "source-offset", "source"], ["selector", "order"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int selector = row.Decimal(0, 0, Game == OracleGame.Ages ? 6 : 7);
            if (!_substitutions.TryGetValue(selector, out List<MapSubstitution>? entries))
                _substitutions.Add(selector, entries = new());
            if (row.UnsignedDecimal(1) != entries.Count) throw row.Invalid(1, "consecutive native copy order");
            int height = row.Decimal(2, 1, 15), width = row.Decimal(3, 1, 15);
            int to = row.HexInt(4), from = row.HexInt(5);
            if (Math.Max(to, from) + (height - 1) * 32 + width > 0x400) throw row.Invalid(4, "rectangle inside w4TileMap");
            entries.Add(new(height, width, to, from));
        }
        for (int selector = 0; selector <= (Game == OracleGame.Ages ? 6 : 7); selector++)
            if (selector != 4 && !_substitutions.ContainsKey(selector))
                throw new InvalidOperationException($"{table.Path}: missing native substitution selector ${selector:x2}.");
    }

    internal void ApplySubstitution(int selector, byte[] map, byte[] flags)
    {
        int maximum = Game == OracleGame.Ages ? 6 : 7;
        if ((uint)selector > maximum) throw new ArgumentOutOfRangeException(nameof(selector));
        if (!_substitutions.TryGetValue(selector, out List<MapSubstitution>? entries)) return; // Native empty selector $04.
        foreach (MapSubstitution entry in entries)
        for (int y = 0; y < entry.Height; y++)
        for (int x = 0; x < entry.Width; x++)
        {
            int from = entry.Source + y * 32 + x, to = entry.Destination + y * 32 + x;
            map[to] = map[from]; flags[to] = flags[from];
        }
    }
}

internal readonly record struct DungeonEntrance(int Group, int Room, int FallbackText);

internal readonly record struct MapCell(int Text, int Popup);

public readonly record struct MapText(int TextId, string Message, int Position);
internal readonly record struct MapGeometry(int Width, int Height, int StartX, int StartY, int PopupShiftX, int PopupShiftY);
internal readonly record struct MapSubstitution(int Height, int Width, int Destination, int Source);
