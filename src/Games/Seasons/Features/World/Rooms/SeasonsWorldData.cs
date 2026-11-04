
namespace Oracles;

// Native Seasons room/tileset data; story composition shares these live palettes.
internal sealed class SeasonsWorldData
{
    internal const string Root = SeasonsGameContent.AssetRoot;
    internal OracleAnimationData Animations { get; } = new(Root);
    private readonly ScreenTransitionGraphicsDatabase _transitionGraphics = new(Root);
    internal BackgroundPaletteState Palettes { get; }
    internal Color[,] SpritePalettes { get; set; } = new Color[8,4];
    private readonly Dictionary<(int Group,int Room,int Season),GeneratedTableRow> _rooms = [];
    private readonly Dictionary<(int Group,int Room,int Season),OracleRoomData> _roomCache = [];
    private readonly RoomTexturePublication _texturePublication = new();
    internal RoomTexturePublicationScope DeferTexturePublication() => _texturePublication.Defer();
    private readonly Dictionary<string,List<SeasonsGraphicsUpload>> _graphics = new(StringComparer.Ordinal);
    private OracleRoomData? _currentRoom;
    private Image? _caravanGraphics;
    private readonly byte[] _roomPacks = OracleAssetCache.ReadBytes(Root + "world/room_packs.bin");
    private readonly byte[] _packSeasons = OracleAssetCache.ReadBytes(Root + "world/pack_seasons.bin");
    private readonly GeneratedTable _scenery = Table("scenery",["room","kind","position","tile","attribute","source"]);

    internal SeasonsWorldData()
    {
        var textColors = OracleGraphicsData.LoadPalette(Root + "metadata/textboxBgPalette1.bin",3);
        var textbox = new Color[3,4];
        for (int palette = 0; palette < 3; palette++)
        for (int shade = 0; shade < 4; shade++) textbox[palette,shade] = textColors[palette,shade];
        Palettes = new(OracleGraphicsData.LoadPaletteColors(Root + "metadata/commonBgPalette0.bin"),textbox);
        Palettes.Changed += () => _currentRoom?.RefreshPaletteTextures();
        foreach (var row in Table("rooms",["room","season","tileset","layout-group","unique-header","graphics-header","palette-header","animation","active-collisions","flags","layout","mapping","post-scroll-gfx","group","dungeon","source","support"]).Rows)
            _rooms.Add((row.Decimal(13,0,7),row.HexByte(0),row.Decimal(1,0,3)),row);
        foreach (var row in Table("graphics",["header","order","asset","address","source-offset","tile-count"]).Rows)
        {
            string header = row.RequiredString(0);
            if (!_graphics.TryGetValue(header,out var uploads)) _graphics.Add(header,uploads = []);
            if (row.UnsignedDecimal(1) != uploads.Count) throw row.Invalid(1,"source-ordered graphics uploads");
            uploads.Add(new(Root + row.RequiredString(2),row.HexWord(3),row.HexWord(4),row.HexByte(5)));
        }
    }
    private static GeneratedTable Table(string name,string[] columns) => GeneratedTable.Load(
        Root + $"world/{name}.tsv",new($"Seasons world {name}",GeneratedTableKeySemantics.Ordered,columns,headerRequired:true));
    internal bool HasRoom(int group,int room) => _rooms.TryGetValue((group,room,0),out var row) && row.RequiredString(16) == "navigation";
    internal int GetDungeonIndex(int group,int room)
    {
        if (!_rooms.TryGetValue((group,room,0),out var row))
            throw new NotSupportedException($"data/seasons/tilesetAssignments.s: room ${group:x1}:{room:x2} is not imported.");
        int index = row.HexByte(14);
        return index == 0xff ? -1 : index;
    }
    internal IReadOnlyList<SeasonsGraphicsUpload> Graphics(string header) => header == "UNIQUE_GFXH_NONE" ? [] :
        _graphics.TryGetValue(header,out var uploads) ? uploads :
        throw new NotSupportedException($"data/seasons/gfxHeaders.s: unbound graphics header {header}.");

    internal OracleRoomData LoadRoom(int room, int season, int group = 0)
    {
        if (!_rooms.TryGetValue((group, room, season), out var row))
            throw new NotSupportedException($"data/seasons/roomStateModifiers.s: room ${group:x1}:{room:x2}, season ${season:x2} is not imported.");
        if (row.RequiredString(16) != "navigation") throw new NotSupportedException($"{row.RequiredString(15)}: {row.RequiredString(16)}.");
        if (!_roomCache.TryGetValue((group, room, season), out var result))
        {
            string layout = row.RequiredString(10);
            Image graphics = BuildRoomGraphics(row.RequiredString(5), row.RequiredString(4));
            result = new OracleRoomData(group, room, row.HexByte(2), row.HexByte(7), row.HexByte(8), (byte)row.HexByte(9),
                OracleGraphicsData.ReadBytes(Root + $"world/rooms/{layout}.bin", group < 4 ? 80 : 176),
                OracleGraphicsData.ReadBytes(Root + $"world/rooms/collisions-{row.RequiredString(11)}.bin", 256),
                graphics, OracleGraphicsCache.LoadImage(Root + "gfx/gfx_hud.png"),
                OracleGraphicsData.ReadBytes(Root + $"world/rooms/mappings-{row.RequiredString(11)}.bin", 2048),
                RoomPalette(layout, season), Palettes, Animations, _texturePublication)
            {
                AssetRoot = Root,
                TilesetStateModifier = season,
                TilesetPaletteId = _transitionGraphics.PaletteForTileset(row.HexByte(2), season),
                LoadsUniqueGraphicsAfterScroll = row.Decimal(12, 0, 1) != 0,
                BackgroundBank0Graphics = group == 0 && room is 0x97 or 0x98 ? CaravanGraphics() : null
            };
            _roomCache.Add((group, room, season), result);
        }
        ActivateRoom(result);
        result.LoadTilesetPalette(resetLiveGraphics: true);
        return result;
    }

    internal int RoomPack(int room) => _roomPacks[room];
    internal int DefaultSeason(int pack, int companion) => pack < 0xf0 ? _packSeasons[pack] :
        pack == 0xff ? (companion - (SpecialObjectId.Ricky - 1)) & 3 : 1;

    private Image CaravanGraphics()
    {
        if (_caravanGraphics is not null) return _caravanGraphics;
        _caravanGraphics = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
        _caravanGraphics.Fill(Colors.White);
        Image source = OracleGraphicsCache.LoadImage(Root + "gfx/spr_caravan.png");
        // TREE_GFXH_02 loads interleaved tiles at bank 0:$9200; invert:false is BG encoding.
        for (int tile = 0; tile < source.GetWidth() * source.GetHeight() / 64; tile++)
            _caravanGraphics.BlitRect(source, new(OracleTileRenderer.SourceTileOrigin(source, tile, true), new(8, 8)),
                new((0xa0 + tile) % 16 * 8, (0xa0 + tile) / 16 * 8));
        return _caravanGraphics;
    }

    internal void ApplyScenery(OracleRoomData room, OracleSaveData save, long tick)
    {
        bool troupe = room.Group == 0 && room.Id == 0x98 && !save.HasRoomFlag(0, 0x98, 0x40);
        bool wagon = room.Group == 0 && room.Id == 0x97 && !save.HasGlobalFlag(0x0a);
        bool gate = room.Group == 0 && room.Id == 0xd9 && save.HasRoomFlag(0,0xd9,0x80);
        if (!troupe && !wagon && !gate) return;
        if (troupe)
        {
            room.SetPackedLayoutWithoutGraphics(0x14, 0);
            room.SetPackedLayoutWithoutGraphics(0x24, 0);
        }
        foreach (var row in _scenery.Rows)
        {
            if (row.HexByte(0) != room.Id) continue;
            int position = row.HexWord(2);
            byte tile = (byte)row.HexByte(3), attribute = (byte)row.HexByte(4);
            if (row.RequiredString(1) == "gate-tile") room.SetPositionTileAndCollision(new((position&15)*16,(position>>4)*16),tile,null,tick);
            else if (row.RequiredString(1) == "collision") room.SetPackedTileCollision((byte)position, tile);
            else if (row.RequiredString(1) == "mapping")
                room.SetBackgroundMappingRectangle(new((position & 31) * 8, (position >> 5) * 8), 1,
                    new byte[] { tile, attribute, (byte)(tile + 1), attribute }, tick);
            else throw row.Invalid(1, "a troupe mapping or collision write");
        }
    }

    internal void ActivateRoom(OracleRoomData room)
    {
        var entry = _roomCache.Single(pair => ReferenceEquals(pair.Value, room));
        var row = _rooms[entry.Key];
        _currentRoom = room;
        SpritePalettes = OracleGraphicsData.LoadPalette(Root + $"world/rooms/{row.RequiredString(10)}-{entry.Key.Season}-spr.bin");
    }
    private static Color[,] RoomPalette(string layout, int season)
    {
        Color[,] source = OracleGraphicsData.LoadPalette(Root + $"world/rooms/{layout}-{season}-bg.bin");
        var colors = new Color[6, 4];
        for (int palette = 0; palette < 6; palette++)
        for (int shade = 0; shade < 4; shade++) colors[palette, shade] = source[palette + 2, shade];
        return colors;
    }

    internal Image NativeRoomGraphics(int room, int season, int group = 0)
    {
        var row = _rooms[(group, room, season)];
        if (row.RequiredString(16) != "navigation") throw new NotSupportedException($"{row.RequiredString(15)}: {row.RequiredString(16)}.");
        return BuildRoomGraphics(row.RequiredString(5), row.RequiredString(4));
    }
    private Image BuildRoomGraphics(params string[] headers)
    {
        Image output = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
        output.Fill(Colors.White);
        foreach (string header in headers)
        foreach (var upload in Graphics(header))
        {
            if ((upload.Address & 1) != 1 || upload.Address < 0x8800 || upload.Address >= 0x9800)
                throw new NotSupportedException($"Seasons room {header}: unexpected VRAM upload ${upload.Address:x4}.");
            Image source = OracleGraphicsCache.LoadImage(upload.Asset);
            int count = upload.Count == 0 ? source.GetWidth() / 8 * (source.GetHeight() / 8) : upload.Count;
            int first = ((upload.Address & 0xfff0) - 0x8800) / 16;
            for (int tile = 0; tile < count; tile++)
            {
                Vector2I origin = OracleTileRenderer.SourceTileOrigin(source, upload.SourceOffset / 16 + tile, false);
                output.BlitRect(source, new Rect2I(origin, new Vector2I(8, 8)), new Vector2I((first + tile) % 16 * 8, (first + tile) / 16 * 8));
            }
        }
        return output;
    }
}

internal readonly record struct SeasonsGraphicsUpload(string Asset,int Address,int SourceOffset,int Count);
