using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class SeasonsOpeningData
{
    internal const string Root = SeasonsGameContent.AssetRoot;
    internal OracleAnimationData Animations { get; } = new(Root);
    internal BackgroundPaletteState Palettes { get; }
    internal Color[,] SpritePalettes { get; set; } = new Color[8, 4];
    internal NewGameIntroDatabase LinkSprites { get; } = new(Root);
    internal readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> Scripts =
        CutsceneCommandCatalog.LoadPrograms(Root + "opening/commands.tsv");
    private readonly Dictionary<(int, int), GeneratedTableRow> _rooms = [];
    private readonly Dictionary<(int, int), OracleRoomData> _roomCache = [];
    private readonly Dictionary<(int, int), GeneratedTableRow> _actors = [];
    private readonly Dictionary<(int, int), string> _actorAnimations = [];
    private readonly Dictionary<string, List<SeasonsGraphicsUpload>> _graphics = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _texts = [];
    private readonly Dictionary<int, byte[]> _registers = [];
    private readonly Dictionary<int, (int Loop, int[] Parameters)> _linkLoops = [];
    private OracleRoomData? _currentRoom;
    internal (int Duration, int Palette)[] CastleLightning { get; }

    internal SeasonsOpeningData()
    {
        CastleLightning = Table("castle_lightning", ["duration", "palette", "source"]).Rows
            .Select(row => (row.Decimal(0, 1, 254), row.Decimal(1, 0, 2))).ToArray();
        var textColors = OracleGraphicsData.LoadPalette(Root + "opening/textbox_palettes.bin", 3);
        var textbox = new Color[3, 4];
        for (int palette = 0; palette < 3; palette++)
        for (int shade = 0; shade < 4; shade++) textbox[palette, shade] = textColors[palette, shade];
        Palettes = new(OracleGraphicsData.LoadPaletteColors(Root + "opening/common_palette.bin"), textbox);
        Palettes.Changed += () => _currentRoom?.RefreshPaletteTextures();
        foreach (var row in Table("rooms", ["room", "season", "tileset", "layout-group", "unique-header", "graphics-header", "palette-header", "animation", "active-collisions", "flags", "layout", "mapping"]).Rows)
            _rooms.Add((row.HexByte(0), row.Decimal(1, 0, 3)), row);
        foreach (var row in Table("actors", ["id", "subid", "sprite", "tile-base", "palette", "default-animation", "append-sprites", "source-offset", "source"]).Rows)
            _actors.Add((row.HexByte(0), row.HexByte(1)), row);
        foreach (var row in Table("animations", ["id", "index", "frames", "source"]).Rows)
            _actorAnimations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        foreach (var row in Table("text", ["id", "message-base64", "source"]).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        foreach (var row in Table("gfx_registers", ["index", "registers", "source"]).Rows)
            _registers.Add(row.HexByte(0), row.RequiredString(1).Split(',').Select(value => Convert.ToByte(value, 16)).ToArray());
        foreach (var row in Table("link_loops", ["animation", "loop-start", "parameters", "source"]).Rows)
            _linkLoops.Add(row.HexByte(0), (row.UnsignedDecimal(1), row.RequiredString(2).Split(',').Select(value => Convert.ToInt32(value, 16)).ToArray()));
        foreach (var row in Table("graphics", ["header", "order", "asset", "address", "source-offset", "tile-count"]).Rows)
        {
            string header = row.RequiredString(0);
            if (!_graphics.TryGetValue(header, out var uploads)) _graphics.Add(header, uploads = []);
            if (row.UnsignedDecimal(1) != uploads.Count) throw row.Invalid(1, "source-ordered graphics uploads");
            uploads.Add(new(Root + row.RequiredString(2), row.HexWord(3), row.HexWord(4), row.HexByte(5)));
        }
    }

    private static GeneratedTable Table(string name, string[] columns) => GeneratedTable.Load(
        Root + $"opening/{name}.tsv", new($"Seasons opening {name}", GeneratedTableKeySemantics.Ordered, columns, headerRequired: true));

    internal string Text(int id) => _texts.TryGetValue(id, out var text) ? text :
        throw new NotSupportedException($"Seasons opening: unbound TX_{id:x4}.");
    internal byte[] Registers(int index) => _registers[index];
    internal IReadOnlyList<SeasonsGraphicsUpload> Graphics(string header) => header == "UNIQUE_GFXH_NONE" ? [] : _graphics.TryGetValue(header, out var uploads)
        ? uploads : throw new NotSupportedException($"Seasons opening: unbound graphics header {header}.");
    internal IntroSpriteFrame[] LinkFrames(int animation, int direction = 0) => LinkSprites.SpriteFrames($"link-native-{animation:x2}-{direction}");
    internal int LinkLoop(int animation) => _linkLoops[animation].Loop;
    internal int LinkParameter(int animation, int frame) => _linkLoops[animation].Parameters[frame];
    // Imported graphics also contain neighboring variants outside this opening.
    internal IEnumerable<(int Id, int Subid)> ActorVariants => _actors.Keys.Where(key => key.Item1 switch {
        0xa5 => key.Item2 == 8, 0x4e => key.Item2 <= 7, 0x4f => key.Item2 <= 4,
        0x9d or 0x9f or 5 or 8 or 0x27 or 0x77 or 0x86 or 0x88 => key.Item2 == 0, _ => false });
    internal IEnumerable<string> ActorAnimations(int id, int subid)
    {
        int initial = _actors[(id, subid)].UnsignedDecimal(5);
        // Only animations reachable with this variant's loaded OBJ graphics.
        int[] extra = (id,subid) switch {
            (0x4e,6) => [8,9,10,11,12,13], (0x4f,0) => [0x0e], (0x4f,1) => [1,2],
            (0x4f,3) => [6,7,8,9,10,11,12,13], (0x4f,4) => [5],
            (0xa5,_) => [0,1,2,3], (0x77,_) or (0x88,_) => [0,1], _ => [] };
        return extra.Prepend(initial).Distinct().Select(index => Animation(id,index));
    }
    internal IEnumerable<IntroSpriteFrame> OpeningLinkFrames() => new[] { 0,7,8,9,0x0a,0x10,0x14 }
        .SelectMany(animation => Enumerable.Range(0,4).SelectMany(direction => LinkFrames(animation,direction)));
    internal string Animation(int id, int animation) => _actorAnimations.TryGetValue((id, animation), out var frames) ? frames :
        throw new NotSupportedException($"data/seasons/interactionAnimations.s: missing ${id:x2} animation ${animation:x2}.");

    internal NpcCharacter CreateActor(int id, int subid, Vector2 position)
    {
        // interactionGetData clamps beyond the terminating subid record.
        int last = _actors.Keys.Where(key => key.Item1 == id).Max(key => key.Item2);
        var row = _actors[(id, Math.Min(subid, last))];
        return CreateActor(row, id, subid, position, index => Animation(id, index), SpritePalettes);
    }
    internal static NpcCharacter CreateLoadingDin()
    {
        var row = Table("actors", ["id", "subid", "sprite", "tile-base", "palette", "default-animation", "append-sprites", "source-offset", "source"]).Rows
            .Single(value => value.HexByte(0) == 0x4e && value.HexByte(1) == 6);
        string frames = Table("animations", ["id", "index", "frames", "source"]).Rows
            .Single(value => value.HexByte(0) == 0x4e && value.HexByte(1) == row.UnsignedDecimal(5)).RequiredString(2);
        return CreateActor(row, 0x4e, 6, Vector2.Zero, _ => frames,
            OracleGraphicsData.LoadPalette(Root + "opening/rooms/room0098-0-spr.bin"));
    }
    private static NpcCharacter CreateActor(GeneratedTableRow row, int id, int subid, Vector2 position,
        Func<int,string> animationFor, Color[,] palettes)
    {
        string animation = animationFor(row.UnsignedDecimal(5));
        var record = new NpcRecord(0, 0, id, subid, (int)position.Y, (int)position.X, 0, 0,
            row.RequiredString(2), row.UnsignedDecimal(3), row.UnsignedDecimal(4), row.UnsignedDecimal(5), id == 0x9d,
            id == 0x9d ? animationFor(0) : animation, id == 0x9d ? animationFor(1) : animation,
            id == 0x9d ? animationFor(2) : animation, id == 0x9d ? animationFor(3) : animation,
            "", NpcImplementationClassification.EventOwned);
        var actor = new NpcCharacter { Name = $"Seasons_{id:x2}_{subid:x2}", ZIndex = 5 };
        actor.Initialize(record, Root);
        foreach (string sprite in row.String(6).Split(',', StringSplitOptions.RemoveEmptyEntries)) actor.AppendScriptGraphics(sprite);
        actor.SetGraphicsSourceOffset(row.HexWord(7));
        actor.SetScriptAnimation(animation);
        actor.SetScriptPaletteOverrides(Enumerable.Range(0, 8).ToDictionary(index => index,
            index => Enumerable.Range(0, 4).Select(shade => shade == 0 ? Colors.Transparent : palettes[index, shade]).ToArray()));
        actor.ShowInitializedPresentation();
        return actor;
    }

    internal IReadOnlyList<(int Id, int Subid, Vector2 Position)> TroupePlacements() =>
        Table("troupe", ["index", "id", "subid", "y", "x", "source"]).Rows.Select(row =>
            (row.HexByte(1), row.HexByte(2), new Vector2(row.HexByte(4), row.HexByte(3)))).ToArray();

    internal OracleRoomData LoadRoom(int room, int season)
    {
        var row = _rooms[(room, season)];
        if (!_roomCache.TryGetValue((room, season), out var result))
        {
            string layout = row.RequiredString(10);
            Image graphics = BuildRoomGraphics(row.RequiredString(5), row.RequiredString(4));
            result = new OracleRoomData(0, room, row.HexByte(2), row.HexByte(7), row.HexByte(8), (byte)row.HexByte(9),
                OracleGraphicsData.ReadBytes(Root + $"opening/rooms/{layout}.bin", 80),
                OracleGraphicsData.ReadBytes(Root + $"opening/rooms/collisions-{row.RequiredString(11)}.bin", 256),
                graphics, OracleGraphicsCache.LoadImage(Root + "gfx/gfx_hud.png"),
                OracleGraphicsData.ReadBytes(Root + $"opening/rooms/mappings-{row.RequiredString(11)}.bin", 2048),
                RoomPalette(layout, season), Palettes, Animations)
            {
                AssetRoot = Root, TerrainTypeResolver = SeasonsOpeningTerrain.Type,
                HazardTypeResolver = SeasonsOpeningTerrain.Hazard
            };
            _roomCache.Add((room, season), result);
        }
        _currentRoom = result;
        SpritePalettes = OracleGraphicsData.LoadPalette(Root + $"opening/rooms/{row.RequiredString(10)}-{season}-spr.bin");
        result.LoadTilesetPalette(resetLiveGraphics: true);
        return result;
    }
    private static Color[,] RoomPalette(string layout, int season)
    {
        Color[,] source = OracleGraphicsData.LoadPalette(Root + $"opening/rooms/{layout}-{season}-bg.bin");
        var colors = new Color[6, 4];
        for (int palette = 0; palette < 6; palette++)
        for (int shade = 0; shade < 4; shade++) colors[palette, shade] = source[palette + 2, shade];
        return colors;
    }

    internal Image NativeRoomGraphics(int room, int season)
    {
        var row = _rooms[(room, season)];
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

internal readonly record struct SeasonsGraphicsUpload(string Asset, int Address, int SourceOffset, int Count);
