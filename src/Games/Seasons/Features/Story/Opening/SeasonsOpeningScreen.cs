using Godot;
using System;

namespace Oracles;

internal partial class SeasonsOpeningScreen : Control
{
    private SeasonsOpeningData _data = null!;
    private readonly OracleVramTileMap _tiles = new();
    private readonly byte[][] _maps = [new byte[1024], new byte[1024]];
    private readonly byte[][] _attributes = [new byte[1024], new byte[1024]];
    private Texture2D? _background, _window;
    private GameSelectPanorama? _dance;
    private int _lcdc, _windowX, _windowY;
    internal bool CastleCleared { get; private set; }
    private int[] _animationHeaders = [];
    internal int ScrollX { get; set; }
    internal int ScrollY { get; set; }
    internal int CameraX { get; set; }
    internal int CameraY { get; set; }
    internal int WindowX { get => _windowX; set { _windowX = value; QueueRedraw(); } }
    internal int WindowY { get => _windowY; set { _windowY = value; QueueRedraw(); } }
    internal bool Dance => _dance is not null;
    internal void Initialize(SeasonsOpeningData data)
    {
        _data = data;
        data.World.Palettes.Changed += PaletteChanged;
    }
    private void PaletteChanged() { if (Visible && _dance is null) Rebuild(); }
    public override void _ExitTree() { if (_data is not null) _data.World.Palettes.Changed -= PaletteChanged; }
    internal void SetRegisters(int index)
    {
        byte[] registers = _data.Registers(index);
        _lcdc = registers[0]; ScrollY = registers[1]; ScrollX = registers[2];
        CameraY = ScrollY; CameraX = ScrollX;
        _windowY = registers[3]; _windowX = registers[4] - 7;
        QueueRedraw();
    }
    internal void ShowDance()
    {
        _dance ??= new(OracleGame.Seasons); ScrollX = ScrollY = 0;
        Show(); QueueRedraw();
    }
    internal void Load(string header, string palette, int registers, bool preserveCamera = false)
    {
        int cameraX = CameraX, cameraY = CameraY;
        _dance = null; CastleCleared = false; _animationHeaders = []; SetRegisters(registers);
        if (preserveCamera) { CameraX = cameraX; CameraY = cameraY; }
        _data.World.Palettes.Restore(OracleGraphicsData.LoadPalette(SeasonsOpeningData.Root + $"opening/{palette}-bg.bin"));
        _data.World.SpritePalettes = OracleGraphicsData.LoadPalette(SeasonsOpeningData.Root + $"opening/{palette}-spr.bin");
        foreach (var upload in _data.World.Graphics(header))
        {
            int address = upload.Address & 0xfffe, bank = upload.Address & 1;
            if (address >= 0x9800)
            {
                int page = address >= 0x9c00 ? 1 : 0;
                byte[] bytes = OracleAssetCache.ReadBytes(upload.Asset);
                int count = upload.Count == 0 ? bytes.Length - upload.SourceOffset : upload.Count * 16;
                bytes.AsSpan(upload.SourceOffset, count).CopyTo((bank == 0 ? _maps : _attributes)[page].AsSpan(address & 0x3ff));
            }
            else if (address >= 0x8800)
            {
                Image source = OracleGraphicsCache.LoadImage(upload.Asset);
                int count = upload.Count == 0 ? source.GetWidth() * source.GetHeight() / 64 : upload.Count;
                for (int tile = 0; tile < count; tile++)
                    _tiles.MapTile(source, upload.SourceOffset / 16 + tile, (((address - 0x9000) / 16) + tile) & 0xff, bank);
            }
            else if (address >= 0x8000) { /* OBJ uploads are consumed by the interaction renderer. */ }
            else throw new NotSupportedException($"Seasons {header}: VRAM address ${upload.Address:x4}.");
        }
        Rebuild(); Show();
    }
    internal void Rebuild()
    {
        if (_dance is not null) { QueueRedraw(); return; }
        _background = OracleTileRenderer.BuildTileMapTexture(_maps[0], _attributes[0], _tiles, _data.World.Palettes.Capture(), rows: 32);
        _window = OracleTileRenderer.BuildTileMapTexture(_maps[1], _attributes[1], _tiles, _data.World.Palettes.Capture(), rows: 32);
        QueueRedraw();
    }
    internal void Animate(int group, int updates)
    {
        int[] headers = _data.World.Animations.GetActiveHeadersAfterDirectLoad(group, updates);
        if (System.Linq.Enumerable.SequenceEqual(headers, _animationHeaders)) return;
        _animationHeaders = headers;
        for (int tile = 0; tile < 256; tile++)
            if (_data.World.Animations.TryGetOverride(headers, tile, out Image source, out int sourceTile))
                _tiles.MapTile(source, sourceTile, (tile + 0x80) & 0xff, 1);
        Rebuild();
    }
    internal void ClearCastleForCrystal()
    {
        CastleCleared = true;
        Array.Clear(_maps[0]); Array.Fill(_attributes[0], (byte)0x81);
        SetRegisters(0x0d);
        var colors = _data.World.Palettes.Capture();
        for (int palette = 1; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++) colors[palette, shade] = Colors.White;
        _data.World.Palettes.Restore(colors); Rebuild();
    }
    internal void RestoreCastle()
    {
        CastleCleared = false;
        // UNCMP_GFXH_0e restores the saved maps without replacing animated tiles.
        foreach (var upload in _data.World.Graphics("GFXH_SCENE_INSIDE_ONOX_CASTLE"))
        {
            if (upload.Address < 0x9800) continue;
            OracleAssetCache.ReadBytes(upload.Asset).CopyTo((upload.Address & 1) == 0 ? _maps[0] : _attributes[0], 0);
        }
        SetRegisters(0x0d);
        _data.World.Palettes.Restore(OracleGraphicsData.LoadPalette(SeasonsOpeningData.Root + "opening/PALH_SEASONS_97-bg.bin"));
        Rebuild();
    }
    internal void UploadRoom(OracleRoomData room, Image graphics, int offset)
    {
        for (int tile = 0; tile < 256; tile++) _tiles.MapTile(graphics, tile, (tile + 0x80) & 255, 1);
        byte[] mappings = room.CaptureBackgroundMappings(nativeStorage: true);
        for (int index = 0; index < 512; index++)
        {
            _maps[0][offset + index] = mappings[index * 2];
            _attributes[0][offset + index] = mappings[index * 2 + 1];
        }
    }
    public override void _Draw()
    {
        // hBgPaletteSources=$fe retains white fading palettes behind TX_1e05.
        DrawRect(new(0, 0, 160, 144), CastleCleared ? Colors.White : Colors.Black);
        if (CastleCleared) return;
        if (_dance is not null)
        {
            DrawTexture(_dance.Background, new(-ScrollX, 0));
            for (int index = _dance.Sprites.Count - 1; index >= 0; index--)
            {
                var sprite = _dance.Sprites[index]; DrawTexture(sprite.Texture, sprite.Position - new Vector2(ScrollX, 0));
            }
            return;
        }
        if (_background is null) return;
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++) DrawTexture(_background, new(x * 256 - ScrollX, y * 256 - ScrollY));
        if ((_lcdc & 0x20) != 0 && _window is not null && _windowX < 160 && _windowY < 144)
            DrawTexture(_window, new(_windowX, _windowY));
    }
}
